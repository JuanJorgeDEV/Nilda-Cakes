using DeliciasDaNilda.Data;
using DeliciasDaNilda.Domain.Entities;
using DeliciasDaNilda.Domain.Enums;
using DeliciasDaNilda.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace DeliciasDaNilda.Services;

public interface IPagamentoService
{
    /// <summary>
    /// RN09/RN10 — registra uma baixa financeira (sinal/saldo/integral)
    /// vinculada a um pedido e a um administrador, e atualiza
    /// Pedido.StatusPagamento com base na soma dos pagamentos existentes
    /// versus o ValorTotal do pedido.
    /// </summary>
    Task<ResultadoOperacao> RegistrarPagamentoAsync(
        int pedidoId,
        TipoPagamento tipo,
        decimal valor,
        int administradorId,
        string? formaPagamento = null,
        CancellationToken ct = default);

    /// <summary>
    /// RN09/RN10 — atalho usado pela tela de pedidos: deduz tipo e valor do
    /// estado do pedido. <see cref="ModoRecebimento.Tudo"/> recebe o que falta
    /// (Integral se nada foi pago, SaldoRestante se já houve sinal).
    /// <see cref="ModoRecebimento.Sinal"/> só vale sem pagamento anterior;
    /// sem <paramref name="valorSinal"/> usa 50% do total.
    /// </summary>
    Task<ResultadoOperacao> RegistrarRecebimentoAsync(
        int pedidoId,
        ModoRecebimento modo,
        decimal? valorSinal,
        int administradorId,
        string? formaPagamento = null,
        CancellationToken ct = default);
}

public enum ModoRecebimento
{
    Tudo,
    Sinal
}

/// <summary>
/// Controle financeiro do pedido (RN09, RN10).
/// </summary>
public class PagamentoService : IPagamentoService
{
    private readonly AppDbContext _db;

    public PagamentoService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<ResultadoOperacao> RegistrarPagamentoAsync(
        int pedidoId,
        TipoPagamento tipo,
        decimal valor,
        int administradorId,
        string? formaPagamento = null,
        CancellationToken ct = default)
    {
        if (valor <= 0)
        {
            return ResultadoOperacao.Falha("Valor do pagamento deve ser maior que zero.");
        }

        var administradorExiste = await _db.Administradores.AnyAsync(a => a.Id == administradorId, ct);
        if (!administradorExiste)
        {
            return ResultadoOperacao.Falha("Administrador não encontrado.");
        }

        var pedido = await _db.Pedidos
            .Include(p => p.Pagamentos)
            .FirstOrDefaultAsync(p => p.Id == pedidoId, ct);

        if (pedido is null)
        {
            return ResultadoOperacao.Falha("Pedido não encontrado.");
        }

        var somaExistente = pedido.Pagamentos.Sum(p => p.Valor);

        // Regra de aplicação (docs/modelo-de-dados.md, seção 2.9): a soma dos
        // pagamentos de um pedido não deve ultrapassar o ValorTotal.
        if (somaExistente + valor > pedido.ValorTotal)
        {
            var restante = pedido.ValorTotal - somaExistente;
            return ResultadoOperacao.Falha(
                $"Pagamento excede o valor total do pedido. Valor restante: {restante:C}.");
        }

        var pagamento = new Pagamento
        {
            PedidoId = pedidoId,
            Tipo = tipo,
            Valor = valor,
            FormaPagamento = formaPagamento,
            RegistradoPorAdministradorId = administradorId,
            CriadoEm = DateTime.UtcNow
        };

        _db.Pagamentos.Add(pagamento);

        var novaSoma = somaExistente + valor;

        // RN09/RN10: StatusPagamento denormalizado, sincronizado aqui na
        // mesma transação (SaveChanges) do insert do pagamento.
        pedido.StatusPagamento = novaSoma >= pedido.ValorTotal
            ? StatusPagamento.PagoIntegral
            : novaSoma > 0
                ? StatusPagamento.SinalPago
                : StatusPagamento.SemPagamento;

        await _db.SaveChangesAsync(ct);
        return ResultadoOperacao.Ok();
    }

    public async Task<ResultadoOperacao> RegistrarRecebimentoAsync(
        int pedidoId,
        ModoRecebimento modo,
        decimal? valorSinal,
        int administradorId,
        string? formaPagamento = null,
        CancellationToken ct = default)
    {
        var pedido = await _db.Pedidos
            .Include(p => p.Pagamentos)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == pedidoId, ct);

        if (pedido is null)
        {
            return ResultadoOperacao.Falha("Pedido não encontrado.");
        }

        if (pedido.StatusPedido == StatusPedido.Cancelado)
        {
            return ResultadoOperacao.Falha("Pedido cancelado não recebe pagamento.");
        }

        var jaPago = pedido.Pagamentos.Sum(p => p.Valor);
        var restante = pedido.ValorTotal - jaPago;

        if (restante <= 0)
        {
            return ResultadoOperacao.Falha("Este pedido já está quitado.");
        }

        if (modo == ModoRecebimento.Tudo)
        {
            var tipo = jaPago > 0 ? TipoPagamento.SaldoRestante : TipoPagamento.Integral;
            return await RegistrarPagamentoAsync(pedidoId, tipo, restante, administradorId, formaPagamento, ct);
        }

        if (jaPago > 0)
        {
            return ResultadoOperacao.Falha("O sinal já foi recebido. Registre o saldo restante.");
        }

        var valor = valorSinal ?? Math.Round(pedido.ValorTotal / 2m, 2);
        if (valor <= 0 || valor >= pedido.ValorTotal)
        {
            return ResultadoOperacao.Falha(
                $"O sinal deve ser maior que zero e menor que o total ({pedido.ValorTotal:C}). Para pagar tudo, use \"Recebi tudo\".");
        }

        return await RegistrarPagamentoAsync(pedidoId, TipoPagamento.Sinal, valor, administradorId, formaPagamento, ct);
    }
}
