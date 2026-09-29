using DeliciasDaNilda.Data;
using DeliciasDaNilda.Domain.Entities;
using DeliciasDaNilda.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DeliciasDaNilda.Services;

/// <summary>Um ponto da série temporal (dia ou semana) do painel financeiro.</summary>
public sealed record PontoFinanceiro(DateOnly Inicio, decimal Vendas, decimal Custo, decimal Recebido, int Pedidos)
{
    public decimal Lucro => Vendas - Custo;
}

/// <summary>Desempenho de um sabor no período.</summary>
public sealed record DesempenhoSabor(string Sabor, int Pedidos, decimal Kg, decimal Vendas, decimal Custo)
{
    public decimal Lucro => Vendas - Custo;

    public decimal Margem => Vendas > 0 ? Lucro / Vendas : 0m;
}

/// <summary>
/// Painel financeiro de um período. "Vendas" usa os pedidos confirmados ou
/// entregues com entrega no período (competência); "Recebido" usa os
/// pagamentos registrados no período (caixa, RN10). Custos são ESTIMADOS com
/// o custo atual dos insumos — não há histórico de preço de compra.
/// </summary>
public sealed record ResumoFinanceiro(
    DateOnly Inicio,
    DateOnly Fim,
    decimal Vendas,
    decimal Recebido,
    decimal CustoBolos,
    decimal Perdas,
    decimal AReceber,
    int PedidosVendidos,
    decimal KgVendidos,
    bool AgrupadoPorSemana,
    IReadOnlyList<PontoFinanceiro> Serie,
    IReadOnlyList<DesempenhoSabor> Sabores,
    IReadOnlyDictionary<DayOfWeek, int> PedidosPorDiaDaSemana,
    IReadOnlyDictionary<string, decimal> RecebidoPorForma,
    bool CustoIncompleto)
{
    public decimal LucroBruto => Vendas - CustoBolos;

    /// <summary>Lucro depois de descontar o que se perdeu de estoque no período.</summary>
    public decimal Resultado => LucroBruto - Perdas;

    public decimal Margem => Vendas > 0 ? LucroBruto / Vendas : 0m;

    /// <summary>Retorno sobre o custo: quanto de lucro cada R$ 1 gasto em insumo trouxe.</summary>
    public decimal Roi => CustoBolos > 0 ? LucroBruto / CustoBolos : 0m;

    public decimal TicketMedio => PedidosVendidos > 0 ? Vendas / PedidosVendidos : 0m;
}

public interface IFinanceiroService
{
    Task<ResumoFinanceiro> ResumirAsync(DateOnly inicio, DateOnly fim, CancellationToken ct = default);
}

/// <summary>Métricas de receita, custo, lucro e ROI para o ADM (RN10).</summary>
public class FinanceiroService : IFinanceiroService
{
    private static readonly StatusPedido[] StatusVendidos = { StatusPedido.Confirmado, StatusPedido.Concluido };

    private readonly AppDbContext _db;

    public FinanceiroService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<ResumoFinanceiro> ResumirAsync(DateOnly inicio, DateOnly fim, CancellationToken ct = default)
    {
        if (fim < inicio)
        {
            (inicio, fim) = (fim, inicio);
        }

        // Pagamentos e perdas têm CriadoEm em UTC; o período é em dias locais.
        var inicioUtc = inicio.ToDateTime(TimeOnly.MinValue, DateTimeKind.Local).ToUniversalTime();
        var fimUtc = fim.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Local).ToUniversalTime();

        var pedidos = await _db.Pedidos
            .AsNoTracking()
            .Where(p => p.DataEntrega >= inicio && p.DataEntrega <= fim && StatusVendidos.Contains(p.StatusPedido))
            .Include(p => p.Sabor)
                .ThenInclude(s => s.FichaTecnica!)
                    .ThenInclude(f => f.Itens)
                        .ThenInclude(i => i.Ingrediente)
            .ToListAsync(ct);

        var pagamentos = await _db.Pagamentos
            .AsNoTracking()
            .Where(p => p.CriadoEm >= inicioUtc && p.CriadoEm < fimUtc)
            .Select(p => new { p.Valor, p.FormaPagamento, p.CriadoEm })
            .ToListAsync(ct);

        var perdas = await _db.MovimentosEstoque
            .AsNoTracking()
            .Where(m => m.Tipo == TipoMovimentoEstoque.AjustePerda && m.CriadoEm >= inicioUtc && m.CriadoEm < fimUtc)
            .Select(m => new { m.Quantidade, Custo = m.Ingrediente.CustoPorUnidade })
            .ToListAsync(ct);

        var emAberto = await _db.Pedidos
            .AsNoTracking()
            .Where(p => p.StatusPedido != StatusPedido.Cancelado && p.StatusPagamento != StatusPagamento.PagoIntegral)
            .Select(p => new { p.ValorTotal, Pago = p.Pagamentos.Sum(x => (decimal?)x.Valor) ?? 0m })
            .ToListAsync(ct);

        decimal CustoDoPedido(Pedido p) => SaborService.CalcularCustoPorKg(p.Sabor) * p.Peso;

        var porSemana = fim.DayNumber - inicio.DayNumber + 1 > 31;
        DateOnly Balde(DateOnly d) => porSemana ? d.AddDays(-(int)d.DayOfWeek) : d;

        var baldes = new SortedDictionary<DateOnly, (decimal Vendas, decimal Custo, decimal Recebido, int Pedidos)>();
        for (var d = Balde(inicio); d <= fim; d = d.AddDays(porSemana ? 7 : 1))
        {
            baldes[d] = default;
        }

        foreach (var p in pedidos)
        {
            var b = baldes.GetValueOrDefault(Balde(p.DataEntrega));
            baldes[Balde(p.DataEntrega)] = (b.Vendas + p.ValorTotal, b.Custo + CustoDoPedido(p), b.Recebido, b.Pedidos + 1);
        }

        foreach (var pg in pagamentos)
        {
            var dia = Balde(DateOnly.FromDateTime(pg.CriadoEm.ToLocalTime()));
            var b = baldes.GetValueOrDefault(dia);
            baldes[dia] = (b.Vendas, b.Custo, b.Recebido + pg.Valor, b.Pedidos);
        }

        var serie = baldes.Select(kv => new PontoFinanceiro(kv.Key, kv.Value.Vendas, kv.Value.Custo, kv.Value.Recebido, kv.Value.Pedidos)).ToList();

        var sabores = pedidos
            .GroupBy(p => p.Sabor.Nome)
            .Select(g => new DesempenhoSabor(g.Key, g.Count(), g.Sum(p => p.Peso), g.Sum(p => p.ValorTotal), g.Sum(CustoDoPedido)))
            .OrderByDescending(s => s.Lucro)
            .ToList();

        var porDiaSemana = Enum.GetValues<DayOfWeek>().ToDictionary(d => d, d => pedidos.Count(p => p.DataEntrega.DayOfWeek == d));

        var porForma = pagamentos
            .GroupBy(p => string.IsNullOrWhiteSpace(p.FormaPagamento) ? "Não informado" : p.FormaPagamento!.Trim())
            .ToDictionary(g => g.Key, g => g.Sum(p => p.Valor));

        return new ResumoFinanceiro(
            Inicio: inicio,
            Fim: fim,
            Vendas: pedidos.Sum(p => p.ValorTotal),
            Recebido: pagamentos.Sum(p => p.Valor),
            CustoBolos: pedidos.Sum(CustoDoPedido),
            Perdas: perdas.Sum(p => p.Quantidade * (p.Custo ?? 0m)),
            AReceber: emAberto.Sum(p => p.ValorTotal - p.Pago),
            PedidosVendidos: pedidos.Count,
            KgVendidos: pedidos.Sum(p => p.Peso),
            AgrupadoPorSemana: porSemana,
            Serie: serie,
            Sabores: sabores,
            PedidosPorDiaDaSemana: porDiaSemana,
            RecebidoPorForma: porForma,
            CustoIncompleto: pedidos.Any(p => SaborService.CustoIncompleto(p.Sabor)));
    }
}
