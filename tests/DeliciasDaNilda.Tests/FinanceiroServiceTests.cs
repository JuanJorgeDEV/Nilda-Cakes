using DeliciasDaNilda.Data;
using DeliciasDaNilda.Domain.Entities;
using DeliciasDaNilda.Domain.Enums;
using DeliciasDaNilda.Services;
using Microsoft.EntityFrameworkCore;

namespace DeliciasDaNilda.Tests;

/// <summary>RN10 — painel financeiro: vendas na data da confirmação, recebido por caixa, custo estimado pela ficha.</summary>
public class FinanceiroServiceTests
{
    private static AppDbContext CriarContexto() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static readonly DateOnly Hoje = DateOnly.FromDateTime(DateTime.Today);

    // Sabor a R$ 100/kg com ficha: 0,5 kg de chocolate (R$ 40/kg) = R$ 20/kg + R$ 5 extra = custo R$ 25/kg.
    private static async Task<(Sabor sabor, Cliente cliente, Ingrediente choc, Administrador adm)> SeedAsync(AppDbContext db)
    {
        var choc = new Ingrediente { Nome = "Chocolate", UnidadeMedida = "kg", SaldoAtual = 100m, CustoPorUnidade = 40m, CriadoEm = DateTime.UtcNow };
        var cliente = new Cliente { NomeCompleto = "Cliente", WhatsApp = "5516999999999", CriadoEm = DateTime.UtcNow };
        var adm = new Administrador { NomeCompleto = "ADM", Email = "a@a.com", SenhaHash = "x", CriadoEm = DateTime.UtcNow };
        var sabor = new Sabor { Nome = "Trufado", PrecoPorKg = 100m, CustoExtraPorKg = 5m, CriadoEm = DateTime.UtcNow };
        db.AddRange(choc, cliente, adm, sabor);
        await db.SaveChangesAsync();
        db.FichasTecnicas.Add(new FichaTecnica
        {
            SaborId = sabor.Id, CriadoEm = DateTime.UtcNow,
            Itens = { new FichaTecnicaItem { IngredienteId = choc.Id, QuantidadePorKg = 0.5m } }
        });
        await db.SaveChangesAsync();
        return (sabor, cliente, choc, adm);
    }

    // confirmadoHaDias: null = nunca confirmado.
    private static Pedido NovoPedido(Sabor s, Cliente c, decimal kg, DateOnly entrega, StatusPedido status, int? confirmadoHaDias = null) => new()
    {
        ClienteId = c.Id, SaborId = s.Id, Peso = kg, PrecoPorKgUtilizado = s.PrecoPorKg, ValorTotal = kg * s.PrecoPorKg,
        DataEntrega = entrega, StatusPedido = status, CriadoEm = DateTime.UtcNow,
        ConfirmadoEm = confirmadoHaDias is { } dias ? DateTime.UtcNow.AddDays(-dias) : null
    };

    [Fact]
    public async Task Resumo_ContaSoConfirmadosEEntregues_ECalculaCustoLucroMargemRoi()
    {
        await using var db = CriarContexto();
        var (sabor, cliente, _, _) = await SeedAsync(db);
        db.Pedidos.AddRange(
            NovoPedido(sabor, cliente, 2m, Hoje, StatusPedido.Concluido, confirmadoHaDias: 0),            // R$ 200, custo 50
            NovoPedido(sabor, cliente, 1m, Hoje.AddDays(-3), StatusPedido.Confirmado, confirmadoHaDias: 3), // R$ 100, custo 25
            NovoPedido(sabor, cliente, 5m, Hoje, StatusPedido.PendenteConfirmacao),                       // fora: não vendido
            NovoPedido(sabor, cliente, 5m, Hoje, StatusPedido.Cancelado, confirmadoHaDias: 1),            // fora
            NovoPedido(sabor, cliente, 5m, Hoje, StatusPedido.Concluido, confirmadoHaDias: 60));          // fora: confirmado antes do período
        await db.SaveChangesAsync();

        var r = await new FinanceiroService(db).ResumirAsync(Hoje.AddDays(-6), Hoje);

        Assert.Equal(300m, r.Vendas);
        Assert.Equal(75m, r.CustoBolos);
        Assert.Equal(225m, r.LucroBruto);
        Assert.Equal(0.75m, r.Margem);
        Assert.Equal(3m, r.Roi);
        Assert.Equal(2, r.PedidosVendidos);
        Assert.Equal(150m, r.TicketMedio);
        Assert.False(r.AgrupadoPorSemana);
        Assert.Equal(7, r.Serie.Count);
        Assert.Equal(200m, r.Serie.Single(p => p.Inicio == Hoje).Vendas);
        var desempenho = Assert.Single(r.Sabores);
        Assert.Equal(3m, desempenho.Kg);
        Assert.False(r.CustoIncompleto);
    }

    [Fact]
    public async Task Resumo_RecebidoPorCaixa_PerdasEmReais_EAReceber()
    {
        await using var db = CriarContexto();
        var (sabor, cliente, choc, adm) = await SeedAsync(db);
        var pedido = NovoPedido(sabor, cliente, 2m, Hoje.AddDays(10), StatusPedido.PendenteConfirmacao); // ainda a confirmar
        db.Pedidos.Add(pedido);
        await db.SaveChangesAsync();
        db.Pagamentos.AddRange(
            new Pagamento { PedidoId = pedido.Id, Tipo = TipoPagamento.Sinal, Valor = 80m, FormaPagamento = "Pix", RegistradoPorAdministradorId = adm.Id, CriadoEm = DateTime.UtcNow },
            new Pagamento { PedidoId = pedido.Id, Tipo = TipoPagamento.SaldoRestante, Valor = 20m, FormaPagamento = "Dinheiro", RegistradoPorAdministradorId = adm.Id, CriadoEm = DateTime.UtcNow.AddDays(-90) });
        db.MovimentosEstoque.Add(new MovimentoEstoque { IngredienteId = choc.Id, Tipo = TipoMovimentoEstoque.AjustePerda, Quantidade = 0.5m, Motivo = "Venceu", CriadoEm = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var r = await new FinanceiroService(db).ResumirAsync(Hoje.AddDays(-29), Hoje);

        Assert.Equal(0m, r.Vendas);                   // pendente ainda não é venda
        Assert.Equal(80m, r.Recebido);                // o de 90 dias atrás não entra
        Assert.Equal(80m, r.RecebidoDePedidosNaoConfirmados);
        Assert.Equal(80m, r.RecebidoPorForma["Pix"]);
        Assert.Equal(20m, r.Perdas);                  // 0,5 kg × R$ 40
        Assert.Equal(100m, r.AReceber);               // 200 − 80 − 20
    }

    [Fact]
    public async Task VendaConfirmadaHoje_ComEntregaFutura_ContaNoDiaComOCustoDoBolo()
    {
        await using var db = CriarContexto();
        var (sabor, cliente, _, _) = await SeedAsync(db);
        db.Pedidos.Add(NovoPedido(sabor, cliente, 10m, Hoje.AddDays(20), StatusPedido.Confirmado, confirmadoHaDias: 0)); // R$ 1000, custo 250

        await db.SaveChangesAsync();

        var r = await new FinanceiroService(db).ResumirAsync(Hoje, Hoje);

        Assert.Equal(1000m, r.Vendas);
        Assert.Equal(250m, r.CustoBolos);
        Assert.Equal(750m, r.LucroBruto);
        Assert.Single(r.Serie);
    }

    [Fact]
    public async Task Sabores_VemOrdenadosPeloMaisPedido()
    {
        await using var db = CriarContexto();
        var (sabor, cliente, _, _) = await SeedAsync(db);
        var outro = new Sabor { Nome = "Oreo", PrecoPorKg = 90m, CriadoEm = DateTime.UtcNow };
        db.Sabores.Add(outro);
        await db.SaveChangesAsync();
        db.Pedidos.AddRange(
            NovoPedido(outro, cliente, 1m, Hoje, StatusPedido.Confirmado, confirmadoHaDias: 0),
            NovoPedido(sabor, cliente, 5m, Hoje, StatusPedido.Confirmado, confirmadoHaDias: 0),
            NovoPedido(sabor, cliente, 5m, Hoje, StatusPedido.Confirmado, confirmadoHaDias: 0));
        await db.SaveChangesAsync();

        var r = await new FinanceiroService(db).ResumirAsync(Hoje, Hoje);

        Assert.Equal(new[] { "Trufado", "Oreo" }, r.Sabores.Select(x => x.Sabor));
        Assert.Equal(2, r.Sabores[0].Pedidos);
    }

    [Fact]
    public async Task PeriodoLongo_AgrupaPorSemana()
    {
        await using var db = CriarContexto();
        await SeedAsync(db);

        var r = await new FinanceiroService(db).ResumirAsync(Hoje.AddDays(-89), Hoje);

        Assert.True(r.AgrupadoPorSemana);
        Assert.All(r.Serie, p => Assert.Equal(DayOfWeek.Sunday, p.Inicio.DayOfWeek));
    }
}
