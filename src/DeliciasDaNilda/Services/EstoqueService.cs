using DeliciasDaNilda.Data;
using DeliciasDaNilda.Domain.Entities;
using DeliciasDaNilda.Domain.Enums;
using DeliciasDaNilda.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace DeliciasDaNilda.Services;

public interface IEstoqueService
{
    /// <summary>
    /// RN11 — registra entrada de insumo (compra), incrementando
    /// Ingrediente.SaldoAtual.
    /// </summary>
    Task<ResultadoOperacao> RegistrarEntradaAsync(int ingredienteId, decimal quantidade, int administradorId, string? motivo = null, CancellationToken ct = default);

    /// <summary>
    /// RN12 — abate automático de estoque na confirmação de um pedido:
    /// para cada item da ficha técnica do sabor do pedido, deduz
    /// QuantidadePorKg x Pedido.Peso do saldo do ingrediente. Idempotente:
    /// se o pedido já tiver movimentos de saída de confirmação registrados,
    /// não abate novamente.
    /// </summary>
    Task<ResultadoOperacao> RegistrarSaidaPorConfirmacaoAsync(int pedidoId, CancellationToken ct = default);

    /// <summary>
    /// RN13 — ajuste manual de perda/avaria/desperdício, sempre gerando um
    /// movimento (nunca sobrescrevendo o saldo diretamente). Motivo é
    /// obrigatório.
    /// </summary>
    Task<ResultadoOperacao> RegistrarAjustePerdaAsync(int ingredienteId, decimal quantidade, int administradorId, string motivo, CancellationToken ct = default);

    /// <summary>
    /// Decisão de produto (2026-09-16): cancelar um pedido que já estava
    /// Confirmado (RN12 já abateu estoque) deve estornar automaticamente os
    /// ingredientes abatidos. Para cada MovimentoEstoque
    /// SaidaConfirmacaoPedido vinculado ao pedido, gera um novo movimento
    /// AjusteCorrecao com a mesma quantidade (devolvendo ao saldo) e
    /// PedidoId preenchido para rastreabilidade. Idempotente: se o pedido já
    /// tiver estorno registrado, não gera de novo.
    /// </summary>
    Task<ResultadoOperacao> RegistrarEstornoPorCancelamentoAsync(int pedidoId, int? administradorId = null, CancellationToken ct = default);
}

/// <summary>
/// Ponto único de entrada para qualquer MovimentoEstoque (RN11, RN12, RN13).
/// Nenhum outro código deve alterar Ingrediente.SaldoAtual diretamente —
/// cada método aqui insere o movimento e atualiza o saldo cache na mesma
/// operação de SaveChanges (docs/modelo-de-dados.md, seção 5, item 3).
/// </summary>
public class EstoqueService : IEstoqueService
{
    private readonly AppDbContext _db;

    public EstoqueService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<ResultadoOperacao> RegistrarEntradaAsync(int ingredienteId, decimal quantidade, int administradorId, string? motivo = null, CancellationToken ct = default)
    {
        if (quantidade <= 0)
        {
            return ResultadoOperacao.Falha("Quantidade de entrada deve ser maior que zero.");
        }

        var ingrediente = await _db.Ingredientes.FirstOrDefaultAsync(i => i.Id == ingredienteId, ct);
        if (ingrediente is null)
        {
            return ResultadoOperacao.Falha("Ingrediente não encontrado.");
        }

        // RN11: entrada de insumo incrementa o saldo.
        var movimento = new MovimentoEstoque
        {
            IngredienteId = ingredienteId,
            Tipo = TipoMovimentoEstoque.Entrada,
            Quantidade = quantidade,
            AdministradorId = administradorId,
            Motivo = motivo,
            CriadoEm = DateTime.UtcNow
        };

        _db.MovimentosEstoque.Add(movimento);
        ingrediente.SaldoAtual += quantidade;

        await _db.SaveChangesAsync(ct);
        return ResultadoOperacao.Ok();
    }

    public async Task<ResultadoOperacao> RegistrarSaidaPorConfirmacaoAsync(int pedidoId, CancellationToken ct = default)
    {
        // Idempotência (RN12): se já existe movimento de saída para este
        // pedido, não abater de novo.
        var jaAbatido = await _db.MovimentosEstoque
            .AnyAsync(m => m.PedidoId == pedidoId && m.Tipo == TipoMovimentoEstoque.SaidaConfirmacaoPedido, ct);
        if (jaAbatido)
        {
            return ResultadoOperacao.Ok();
        }

        var pedido = await _db.Pedidos
            .Include(p => p.Sabor)
                .ThenInclude(s => s.FichaTecnica!)
                    .ThenInclude(f => f.Itens)
                        .ThenInclude(i => i.Ingrediente)
            .FirstOrDefaultAsync(p => p.Id == pedidoId, ct);

        if (pedido is null)
        {
            return ResultadoOperacao.Falha("Pedido não encontrado.");
        }

        var fichaTecnica = pedido.Sabor.FichaTecnica;
        if (fichaTecnica is null || fichaTecnica.Itens.Count == 0)
        {
            return ResultadoOperacao.Falha($"Sabor '{pedido.Sabor.Nome}' não possui ficha técnica cadastrada (RN04) — não é possível abater estoque.");
        }

        var agora = DateTime.UtcNow;

        // RN12: QuantidadeAbatida = QuantidadePorKg x Pedido.Peso, para cada
        // ingrediente da ficha técnica do sabor.
        foreach (var item in fichaTecnica.Itens)
        {
            var quantidadeAbatida = item.QuantidadePorKg * pedido.Peso;

            _db.MovimentosEstoque.Add(new MovimentoEstoque
            {
                IngredienteId = item.IngredienteId,
                Tipo = TipoMovimentoEstoque.SaidaConfirmacaoPedido,
                Quantidade = quantidadeAbatida,
                PedidoId = pedido.Id,
                CriadoEm = agora
            });

            item.Ingrediente.SaldoAtual -= quantidadeAbatida;
        }

        await _db.SaveChangesAsync(ct);
        return ResultadoOperacao.Ok();
    }

    public async Task<ResultadoOperacao> RegistrarAjustePerdaAsync(int ingredienteId, decimal quantidade, int administradorId, string motivo, CancellationToken ct = default)
    {
        if (quantidade <= 0)
        {
            return ResultadoOperacao.Falha("Quantidade de ajuste deve ser maior que zero.");
        }

        if (string.IsNullOrWhiteSpace(motivo))
        {
            return ResultadoOperacao.Falha("Motivo é obrigatório para lançar uma perda/avaria (RN13).");
        }

        var ingrediente = await _db.Ingredientes.FirstOrDefaultAsync(i => i.Id == ingredienteId, ct);
        if (ingrediente is null)
        {
            return ResultadoOperacao.Falha("Ingrediente não encontrado.");
        }

        // RN13: ajuste manual de perda/avaria/desperdício subtrai do saldo,
        // mantendo o saldo lógico fiel ao físico.
        var movimento = new MovimentoEstoque
        {
            IngredienteId = ingredienteId,
            Tipo = TipoMovimentoEstoque.AjustePerda,
            Quantidade = quantidade,
            AdministradorId = administradorId,
            Motivo = motivo,
            CriadoEm = DateTime.UtcNow
        };

        _db.MovimentosEstoque.Add(movimento);
        ingrediente.SaldoAtual -= quantidade;

        await _db.SaveChangesAsync(ct);
        return ResultadoOperacao.Ok();
    }

    public async Task<ResultadoOperacao> RegistrarEstornoPorCancelamentoAsync(int pedidoId, int? administradorId = null, CancellationToken ct = default)
    {
        // Idempotência: se já existe um estorno (AjusteCorrecao) para este
        // pedido, não gerar de novo.
        var jaEstornado = await _db.MovimentosEstoque
            .AnyAsync(m => m.PedidoId == pedidoId && m.Tipo == TipoMovimentoEstoque.AjusteCorrecao, ct);
        if (jaEstornado)
        {
            return ResultadoOperacao.Ok();
        }

        var movimentosSaida = await _db.MovimentosEstoque
            .Include(m => m.Ingrediente)
            .Where(m => m.PedidoId == pedidoId && m.Tipo == TipoMovimentoEstoque.SaidaConfirmacaoPedido)
            .ToListAsync(ct);

        if (movimentosSaida.Count == 0)
        {
            // Pedido nunca teve abate de estoque (RN12) — nada a estornar.
            return ResultadoOperacao.Ok();
        }

        var agora = DateTime.UtcNow;

        foreach (var saida in movimentosSaida)
        {
            _db.MovimentosEstoque.Add(new MovimentoEstoque
            {
                IngredienteId = saida.IngredienteId,
                Tipo = TipoMovimentoEstoque.AjusteCorrecao,
                Quantidade = saida.Quantidade,
                PedidoId = pedidoId,
                AdministradorId = administradorId,
                Motivo = $"Estorno por cancelamento do pedido {pedidoId}",
                CriadoEm = agora
            });

            saida.Ingrediente.SaldoAtual += saida.Quantidade;
        }

        await _db.SaveChangesAsync(ct);
        return ResultadoOperacao.Ok();
    }
}
