using DeliciasDaNilda.Data;
using DeliciasDaNilda.Domain.Entities;
using DeliciasDaNilda.Domain.Enums;
using DeliciasDaNilda.Services;
using Microsoft.EntityFrameworkCore;

namespace DeliciasDaNilda.Tests;

/// <summary>RN14/RN15: projeção sobre pedidos PendenteConfirmacao contra o SaldoAtual (já abatido pelos confirmados, RN12).</summary>
public class ProjecaoEstoqueServiceTests
{
    private static AppDbContext CriarContexto() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    // Sabor com ficha de 1 ingrediente a 0,5 por kg; saldo informado.
    private static async Task<(Sabor sabor, Ingrediente ing, Cliente cliente)> SeedAsync(AppDbContext db, decimal saldo)
    {
        var ing = new Ingrediente { Nome = "Morango", UnidadeMedida = "kg", SaldoAtual = saldo, CriadoEm = DateTime.UtcNow };
        var cliente = new Cliente { NomeCompleto = "Cliente", WhatsApp = "5511999999999", CriadoEm = DateTime.UtcNow };
        var sabor = new Sabor { Nome = "Sensação", PrecoPorKg = 70m, CriadoEm = DateTime.UtcNow };
        db.AddRange(ing, cliente, sabor);
        await db.SaveChangesAsync();
        var ficha = new FichaTecnica { SaborId = sabor.Id, CriadoEm = DateTime.UtcNow };
        db.FichasTecnicas.Add(ficha);
        await db.SaveChangesAsync();
        db.FichaTecnicaItens.Add(new FichaTecnicaItem { FichaTecnicaId = ficha.Id, IngredienteId = ing.Id, QuantidadePorKg = 0.5m });
        await db.SaveChangesAsync();
        return (sabor, ing, cliente);
    }

    private static async Task<Pedido> PedidoAsync(AppDbContext db, Sabor s, Cliente c, decimal kg, int dias, StatusPedido status)
    {
        var p = new Pedido
        {
            ClienteId = c.Id, SaborId = s.Id, Peso = kg, PrecoPorKgUtilizado = s.PrecoPorKg, ValorTotal = kg * s.PrecoPorKg,
            DataEntrega = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(dias)), StatusPedido = status, CriadoEm = DateTime.UtcNow
        };
        db.Pedidos.Add(p);
        await db.SaveChangesAsync();
        return p;
    }

    [Fact]
    public async Task PedidoConfirmado_NaoEntraNaProjecao_SemContagemEmDobro()
    {
        await using var db = CriarContexto();
        var (sabor, _, cliente) = await SeedAsync(db, saldo: 1m); // saldo já abatido pelos confirmados
        await PedidoAsync(db, sabor, cliente, 10m, 10, StatusPedido.Confirmado); // demandaria 5 kg se contado
        await PedidoAsync(db, sabor, cliente, 10m, 11, StatusPedido.Cancelado);
        await PedidoAsync(db, sabor, cliente, 10m, 12, StatusPedido.Concluido);

        var r = await new ProjecaoEstoqueService(db).ProjetarAsync();

        Assert.True(r.EstoqueSuficiente);
        Assert.Null(r.Alerta);
        Assert.Null(r.UltimoPedidoCoberto);
    }

    [Fact]
    public async Task PendentesEmOrdemCronologica_PrimeiroQueEstouraGeraAlerta()
    {
        await using var db = CriarContexto();
        var (sabor, ing, cliente) = await SeedAsync(db, saldo: 2m);
        // Inseridos fora de ordem de data; demandas: 1 kg (dia 5), 1 kg (dia 10), 1,5 kg (dia 20).
        var tardio = await PedidoAsync(db, sabor, cliente, 3m, 20, StatusPedido.PendenteConfirmacao);
        var primeiro = await PedidoAsync(db, sabor, cliente, 2m, 5, StatusPedido.PendenteConfirmacao);
        var segundo = await PedidoAsync(db, sabor, cliente, 2m, 10, StatusPedido.PendenteConfirmacao);

        var r = await new ProjecaoEstoqueService(db).ProjetarAsync(diasAntecedenciaCompra: 3);

        Assert.False(r.EstoqueSuficiente);
        var a = r.Alerta!;
        Assert.Equal(ing.Id, a.IngredienteId);
        Assert.Equal(tardio.Id, a.PedidoAfetadoId);
        Assert.Equal(1.5m, a.QuantidadeFaltante); // 2 - 1 - 1 - 1,5 = -1,5
        Assert.Equal(tardio.DataEntrega.AddDays(-3), a.DataLimiteCompra);
        Assert.Equal(segundo.Id, r.UltimoPedidoCoberto!.PedidoId);
        Assert.Equal(segundo.DataEntrega, r.UltimoPedidoCoberto.DataEntrega);
        Assert.NotEqual(primeiro.Id, r.UltimoPedidoCoberto.PedidoId);
    }

    [Fact]
    public async Task EstoqueSuficienteParaTodosOsPendentes_RetornaSuficiente()
    {
        await using var db = CriarContexto();
        var (sabor, _, cliente) = await SeedAsync(db, saldo: 3m);
        await PedidoAsync(db, sabor, cliente, 2m, 5, StatusPedido.PendenteConfirmacao);
        var ultimo = await PedidoAsync(db, sabor, cliente, 4m, 8, StatusPedido.PendenteConfirmacao); // total 3 kg = saldo

        var r = await new ProjecaoEstoqueService(db).ProjetarAsync();

        Assert.True(r.EstoqueSuficiente);
        Assert.Null(r.Alerta);
        Assert.Equal(ultimo.Id, r.UltimoPedidoCoberto!.PedidoId);
    }
}
