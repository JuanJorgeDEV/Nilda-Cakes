using DeliciasDaNilda.Data;
using DeliciasDaNilda.Domain.Entities;
using DeliciasDaNilda.Domain.Enums;
using DeliciasDaNilda.Services;
using Microsoft.EntityFrameworkCore;

namespace DeliciasDaNilda.Tests;

/// <summary>RN09/RN10: recebimento rápido pela tela de pedidos (tudo, sinal 50%, sinal com valor, saldo).</summary>
public class PagamentoServiceTests
{
    private static AppDbContext CriarContexto() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<(Pedido pedido, int admId)> SeedAsync(AppDbContext db, decimal total = 150m)
    {
        var adm = new Administrador { NomeCompleto = "ADM", Email = $"{Guid.NewGuid()}@teste.com", SenhaHash = "hash", CriadoEm = DateTime.UtcNow };
        var cliente = new Cliente { NomeCompleto = "Cliente", WhatsApp = "5516999999999", CriadoEm = DateTime.UtcNow };
        var sabor = new Sabor { Nome = "Ninho", PrecoPorKg = 75m, CriadoEm = DateTime.UtcNow };
        db.AddRange(adm, cliente, sabor);
        await db.SaveChangesAsync();
        var pedido = new Pedido
        {
            ClienteId = cliente.Id, SaborId = sabor.Id, Peso = total / 75m, PrecoPorKgUtilizado = 75m, ValorTotal = total,
            DataEntrega = DateOnly.FromDateTime(DateTime.Today.AddDays(5)), CriadoEm = DateTime.UtcNow
        };
        db.Pedidos.Add(pedido);
        await db.SaveChangesAsync();
        return (pedido, adm.Id);
    }

    private static async Task<Pedido> RecarregarAsync(AppDbContext db, int id)
    {
        db.ChangeTracker.Clear();
        return await db.Pedidos.Include(p => p.Pagamentos).SingleAsync(p => p.Id == id);
    }

    [Fact]
    public async Task Tudo_SemPagamentoAnterior_RegistraIntegral()
    {
        await using var db = CriarContexto();
        var (pedido, adm) = await SeedAsync(db);

        var r = await new PagamentoService(db).RegistrarRecebimentoAsync(pedido.Id, ModoRecebimento.Tudo, null, adm, "Pix");

        Assert.True(r.Sucesso);
        var p = await RecarregarAsync(db, pedido.Id);
        var pg = Assert.Single(p.Pagamentos);
        Assert.Equal(TipoPagamento.Integral, pg.Tipo);
        Assert.Equal(150m, pg.Valor);
        Assert.Equal("Pix", pg.FormaPagamento);
        Assert.Equal(StatusPagamento.PagoIntegral, p.StatusPagamento);
    }

    [Fact]
    public async Task SinalSemValor_Usa50PorCento_DepoisTudoRecebeSaldo()
    {
        await using var db = CriarContexto();
        var (pedido, adm) = await SeedAsync(db);
        var servico = new PagamentoService(db);

        Assert.True((await servico.RegistrarRecebimentoAsync(pedido.Id, ModoRecebimento.Sinal, null, adm)).Sucesso);
        Assert.Equal(StatusPagamento.SinalPago, (await RecarregarAsync(db, pedido.Id)).StatusPagamento);

        Assert.True((await servico.RegistrarRecebimentoAsync(pedido.Id, ModoRecebimento.Tudo, null, adm)).Sucesso);

        var p = await RecarregarAsync(db, pedido.Id);
        Assert.Equal(new[] { 75m, 75m }, p.Pagamentos.OrderBy(x => x.Id).Select(x => x.Valor));
        Assert.Equal(TipoPagamento.SaldoRestante, p.Pagamentos.OrderBy(x => x.Id).Last().Tipo);
        Assert.Equal(StatusPagamento.PagoIntegral, p.StatusPagamento);
    }

    [Fact]
    public async Task SinalComValor_Respeitado()
    {
        await using var db = CriarContexto();
        var (pedido, adm) = await SeedAsync(db);

        var r = await new PagamentoService(db).RegistrarRecebimentoAsync(pedido.Id, ModoRecebimento.Sinal, 40m, adm);

        Assert.True(r.Sucesso);
        Assert.Equal(40m, Assert.Single((await RecarregarAsync(db, pedido.Id)).Pagamentos).Valor);
    }

    [Theory]
    [InlineData(150)]
    [InlineData(200)]
    [InlineData(0)]
    public async Task SinalIgualOuMaiorQueTotal_OuZero_Recusado(decimal valor)
    {
        await using var db = CriarContexto();
        var (pedido, adm) = await SeedAsync(db);

        var r = await new PagamentoService(db).RegistrarRecebimentoAsync(pedido.Id, ModoRecebimento.Sinal, valor, adm);

        Assert.False(r.Sucesso);
        Assert.Empty((await RecarregarAsync(db, pedido.Id)).Pagamentos);
    }

    [Fact]
    public async Task SegundoSinal_Recusado()
    {
        await using var db = CriarContexto();
        var (pedido, adm) = await SeedAsync(db);
        var servico = new PagamentoService(db);
        await servico.RegistrarRecebimentoAsync(pedido.Id, ModoRecebimento.Sinal, 50m, adm);

        var r = await servico.RegistrarRecebimentoAsync(pedido.Id, ModoRecebimento.Sinal, 20m, adm);

        Assert.False(r.Sucesso);
    }

    [Fact]
    public async Task PedidoQuitado_NaoRecebeMais()
    {
        await using var db = CriarContexto();
        var (pedido, adm) = await SeedAsync(db);
        var servico = new PagamentoService(db);
        await servico.RegistrarRecebimentoAsync(pedido.Id, ModoRecebimento.Tudo, null, adm);

        var r = await servico.RegistrarRecebimentoAsync(pedido.Id, ModoRecebimento.Tudo, null, adm);

        Assert.False(r.Sucesso);
        Assert.Single((await RecarregarAsync(db, pedido.Id)).Pagamentos);
    }
}
