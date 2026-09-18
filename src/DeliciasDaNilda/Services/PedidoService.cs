using DeliciasDaNilda.Data;
using DeliciasDaNilda.Domain.Entities;
using DeliciasDaNilda.Domain.Enums;
using DeliciasDaNilda.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace DeliciasDaNilda.Services;

public interface IPedidoService
{
    /// <summary>
    /// Cria um novo pedido. Valida antecedência mínima e capacidade diária
    /// (RN05/RN06/RN07), calcula o preço (RN03) e persiste sempre com status
    /// PendenteConfirmacao (RN08 — a criação nunca define outro status).
    /// </summary>
    Task<ResultadoOperacao<Pedido>> CriarPedidoAsync(
        int clienteId,
        int saborId,
        decimal pesoKg,
        DateOnly dataEntrega,
        string? observacoes = null,
        CancellationToken ct = default);

    /// <summary>
    /// RN08 — confirmação exclusiva do ADM: transição
    /// PendenteConfirmacao -> Confirmado. Dispara o abate de estoque (RN12)
    /// dentro da mesma transação. Idempotente: confirmar um pedido já
    /// confirmado não gera efeitos colaterais duplicados.
    /// </summary>
    Task<ResultadoOperacao> ConfirmarPedidoAsync(int pedidoId, int administradorId, CancellationToken ct = default);

    /// <summary>
    /// Cancela um pedido (-> Cancelado). Decisão de produto (2026-09-16): se
    /// o pedido estava Confirmado (RN12 já abateu estoque), estorna
    /// automaticamente os ingredientes abatidos, na mesma transação que
    /// muda o status. Se estava PendenteConfirmacao, só muda o status.
    /// </summary>
    Task<ResultadoOperacao> CancelarPedidoAsync(int pedidoId, CancellationToken ct = default);
}

/// <summary>
/// Orquestra o ciclo de vida do pedido (RN03, RN05-RN09, RN12).
/// </summary>
public class PedidoService : IPedidoService
{
    private readonly AppDbContext _db;
    private readonly IPrecificacaoService _precificacaoService;
    private readonly IAgendamentoService _agendamentoService;
    private readonly IEstoqueService _estoqueService;

    public PedidoService(
        AppDbContext db,
        IPrecificacaoService precificacaoService,
        IAgendamentoService agendamentoService,
        IEstoqueService estoqueService)
    {
        _db = db;
        _precificacaoService = precificacaoService;
        _agendamentoService = agendamentoService;
        _estoqueService = estoqueService;
    }

    public async Task<ResultadoOperacao<Pedido>> CriarPedidoAsync(
        int clienteId,
        int saborId,
        decimal pesoKg,
        DateOnly dataEntrega,
        string? observacoes = null,
        CancellationToken ct = default)
    {
        if (pesoKg <= 0)
        {
            return ResultadoOperacao<Pedido>.Falha("Peso deve ser maior que zero.");
        }

        var clienteExiste = await _db.Clientes.AnyAsync(c => c.Id == clienteId, ct);
        if (!clienteExiste)
        {
            return ResultadoOperacao<Pedido>.Falha("Cliente não encontrado.");
        }

        var sabor = await _db.Sabores.FirstOrDefaultAsync(s => s.Id == saborId, ct);
        if (sabor is null)
        {
            return ResultadoOperacao<Pedido>.Falha("Sabor não encontrado.");
        }

        if (!sabor.Disponivel)
        {
            return ResultadoOperacao<Pedido>.Falha($"Sabor '{sabor.Nome}' não está disponível no momento.");
        }

        // RN05 + RN06/RN07.
        var validacaoData = await _agendamentoService.ValidarDataParaNovoPedidoAsync(dataEntrega, DateTime.UtcNow, ct);
        if (!validacaoData.Sucesso)
        {
            return ResultadoOperacao<Pedido>.Falha(validacaoData.Erro!);
        }

        // RN03.
        var precificacao = _precificacaoService.Calcular(pesoKg, sabor.PrecoPorKg);

        var pedido = new Pedido
        {
            ClienteId = clienteId,
            SaborId = saborId,
            Peso = pesoKg,
            PrecoPorKgUtilizado = precificacao.PrecoPorKgUtilizado,
            ValorTotal = precificacao.ValorTotal,
            DataEntrega = dataEntrega,
            // RN08: todo pedido novo entra como Pendente de Confirmação — nunca outro status.
            StatusPedido = StatusPedido.PendenteConfirmacao,
            StatusPagamento = StatusPagamento.SemPagamento,
            Observacoes = observacoes,
            CriadoEm = DateTime.UtcNow
        };

        _db.Pedidos.Add(pedido);
        await _db.SaveChangesAsync(ct);

        return ResultadoOperacao<Pedido>.Ok(pedido);
    }

    public async Task<ResultadoOperacao> ConfirmarPedidoAsync(int pedidoId, int administradorId, CancellationToken ct = default)
    {
        var administradorExiste = await _db.Administradores.AnyAsync(a => a.Id == administradorId, ct);
        if (!administradorExiste)
        {
            return ResultadoOperacao.Falha("Administrador não encontrado.");
        }

        var pedido = await _db.Pedidos.FirstOrDefaultAsync(p => p.Id == pedidoId, ct);
        if (pedido is null)
        {
            return ResultadoOperacao.Falha("Pedido não encontrado.");
        }

        // RN08: idempotente — confirmar duas vezes não deve reabater estoque.
        if (pedido.StatusPedido == StatusPedido.Confirmado)
        {
            return ResultadoOperacao.Ok();
        }

        if (pedido.StatusPedido != StatusPedido.PendenteConfirmacao)
        {
            return ResultadoOperacao.Falha($"Pedido no status '{pedido.StatusPedido}' não pode ser confirmado.");
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        // RN12: abate de estoque disparado exclusivamente nesta transição.
        var resultadoAbate = await _estoqueService.RegistrarSaidaPorConfirmacaoAsync(pedido.Id, ct);
        if (!resultadoAbate.Sucesso)
        {
            await transaction.RollbackAsync(ct);
            return ResultadoOperacao.Falha(resultadoAbate.Erro!);
        }

        pedido.StatusPedido = StatusPedido.Confirmado;
        pedido.ConfirmadoEm = DateTime.UtcNow;
        pedido.ConfirmadoPorAdministradorId = administradorId;

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return ResultadoOperacao.Ok();
    }

    public async Task<ResultadoOperacao> CancelarPedidoAsync(int pedidoId, CancellationToken ct = default)
    {
        var pedido = await _db.Pedidos.FirstOrDefaultAsync(p => p.Id == pedidoId, ct);
        if (pedido is null)
        {
            return ResultadoOperacao.Falha("Pedido não encontrado.");
        }

        if (pedido.StatusPedido == StatusPedido.Cancelado)
        {
            return ResultadoOperacao.Ok();
        }

        if (pedido.StatusPedido == StatusPedido.Concluido)
        {
            return ResultadoOperacao.Falha("Pedido já concluído não pode ser cancelado.");
        }

        var estavaConfirmado = pedido.StatusPedido == StatusPedido.Confirmado;

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        if (estavaConfirmado)
        {
            // Decisão de produto (2026-09-16): cancelar um pedido Confirmado
            // estorna automaticamente o estoque abatido pela RN12.
            var resultadoEstorno = await _estoqueService.RegistrarEstornoPorCancelamentoAsync(pedido.Id, ct: ct);
            if (!resultadoEstorno.Sucesso)
            {
                await transaction.RollbackAsync(ct);
                return ResultadoOperacao.Falha(resultadoEstorno.Erro!);
            }
        }

        pedido.StatusPedido = StatusPedido.Cancelado;

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ResultadoOperacao.Ok();
    }
}
