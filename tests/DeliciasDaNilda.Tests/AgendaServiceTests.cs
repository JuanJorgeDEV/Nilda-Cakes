using DeliciasDaNilda.Data;
using DeliciasDaNilda.Domain.Entities;
using DeliciasDaNilda.Domain.Enums;
using DeliciasDaNilda.Services;
using Microsoft.EntityFrameworkCore;

namespace DeliciasDaNilda.Tests;

/// <summary>Testes de RN16 (agenda operacional integrada).</summary>
public class AgendaServiceTests
{
    private static AppDbContext CriarContexto()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task ObterAgendaDoDia_DoisSaboresComIngredienteComum_SomaFichaTecnicaTotal()
    {
        await using var db = CriarContexto();
        var data = new DateOnly(2026, 9, 25);

        var farinha = new Ingrediente { Nome = "Farinha", UnidadeMedida = "kg", SaldoAtual = 100m, CriadoEm = DateTime.UtcNow };
        var chocolate = new Ingrediente { Nome = "Chocolate", UnidadeMedida = "kg", SaldoAtual = 100m, CriadoEm = DateTime.UtcNow };
        var coco = new Ingrediente { Nome = "Coco", UnidadeMedida = "kg", SaldoAtual = 100m, CriadoEm = DateTime.UtcNow };
        db.Ingredientes.AddRange(farinha, chocolate, coco);

        var cliente1 = new Cliente { NomeCompleto = "Ana Silva", WhatsApp = "11999990001", CriadoEm = DateTime.UtcNow };
        var cliente2 = new Cliente { NomeCompleto = "Beatriz Souza", WhatsApp = "11999990002", CriadoEm = DateTime.UtcNow };
        db.Clientes.AddRange(cliente1, cliente2);

        var saborChocolate = new Sabor { Nome = "Chocolate Trufado", PrecoPorKg = 90m, CriadoEm = DateTime.UtcNow };
        var saborPrestigio = new Sabor { Nome = "Prestígio", PrecoPorKg = 95m, CriadoEm = DateTime.UtcNow };
        db.Sabores.AddRange(saborChocolate, saborPrestigio);
        await db.SaveChangesAsync();

        var fichaChocolate = new FichaTecnica { SaborId = saborChocolate.Id, CriadoEm = DateTime.UtcNow };
        var fichaPrestigio = new FichaTecnica { SaborId = saborPrestigio.Id, CriadoEm = DateTime.UtcNow };
        db.FichasTecnicas.AddRange(fichaChocolate, fichaPrestigio);
        await db.SaveChangesAsync();

        // Ambos sabores usam farinha (ingrediente compartilhado).
        db.FichaTecnicaItens.AddRange(
            new FichaTecnicaItem { FichaTecnicaId = fichaChocolate.Id, IngredienteId = farinha.Id, QuantidadePorKg = 0.5m },
            new FichaTecnicaItem { FichaTecnicaId = fichaChocolate.Id, IngredienteId = chocolate.Id, QuantidadePorKg = 0.3m },
            new FichaTecnicaItem { FichaTecnicaId = fichaPrestigio.Id, IngredienteId = farinha.Id, QuantidadePorKg = 0.4m },
            new FichaTecnicaItem { FichaTecnicaId = fichaPrestigio.Id, IngredienteId = coco.Id, QuantidadePorKg = 0.2m });
        await db.SaveChangesAsync();

        db.Pedidos.AddRange(
            new Pedido
            {
                ClienteId = cliente1.Id,
                SaborId = saborChocolate.Id,
                Peso = 2m,
                PrecoPorKgUtilizado = saborChocolate.PrecoPorKg,
                ValorTotal = saborChocolate.PrecoPorKg * 2m,
                DataEntrega = data,
                HorarioRetirada = new TimeOnly(10, 0),
                StatusPedido = StatusPedido.Confirmado,
                StatusPagamento = StatusPagamento.SinalPago,
                CriadoEm = DateTime.UtcNow
            },
            new Pedido
            {
                ClienteId = cliente2.Id,
                SaborId = saborPrestigio.Id,
                Peso = 3m,
                PrecoPorKgUtilizado = saborPrestigio.PrecoPorKg,
                ValorTotal = saborPrestigio.PrecoPorKg * 3m,
                DataEntrega = data,
                HorarioRetirada = new TimeOnly(14, 0),
                StatusPedido = StatusPedido.PendenteConfirmacao,
                StatusPagamento = StatusPagamento.SemPagamento,
                CriadoEm = DateTime.UtcNow
            });
        await db.SaveChangesAsync();

        var service = new AgendaService(db);

        var resultado = await service.ObterAgendaDoDiaAsync(data);

        Assert.Equal(data, resultado.Data);
        Assert.Equal(2, resultado.Pedidos.Count);
        Assert.Contains(resultado.Pedidos, p => p.ClienteNomeCompleto == "Ana Silva" && p.Sabor == "Chocolate Trufado" && p.Peso == 2m);
        Assert.Contains(resultado.Pedidos, p => p.ClienteNomeCompleto == "Beatriz Souza" && p.Sabor == "Prestígio" && p.Peso == 3m);

        // Farinha: (0.5 x 2) + (0.4 x 3) = 1.0 + 1.2 = 2.2
        var farinhaTotal = Assert.Single(resultado.FichaTecnicaTotalDoDia, i => i.IngredienteId == farinha.Id);
        Assert.Equal(2.2m, farinhaTotal.QuantidadeTotalNecessaria);

        // Chocolate: 0.3 x 2 = 0.6 (só no sabor Chocolate Trufado)
        var chocolateTotal = Assert.Single(resultado.FichaTecnicaTotalDoDia, i => i.IngredienteId == chocolate.Id);
        Assert.Equal(0.6m, chocolateTotal.QuantidadeTotalNecessaria);

        // Coco: 0.2 x 3 = 0.6 (só no sabor Prestígio)
        var cocoTotal = Assert.Single(resultado.FichaTecnicaTotalDoDia, i => i.IngredienteId == coco.Id);
        Assert.Equal(0.6m, cocoTotal.QuantidadeTotalNecessaria);
    }

    [Fact]
    public async Task ObterAgendaDoDia_PedidoCancelado_NaoEntraNaAgendaNemNaFichaTecnica()
    {
        await using var db = CriarContexto();
        var data = new DateOnly(2026, 9, 26);

        var farinha = new Ingrediente { Nome = "Farinha", UnidadeMedida = "kg", SaldoAtual = 100m, CriadoEm = DateTime.UtcNow };
        db.Ingredientes.Add(farinha);

        var cliente = new Cliente { NomeCompleto = "Carlos Lima", WhatsApp = "11999990003", CriadoEm = DateTime.UtcNow };
        db.Clientes.Add(cliente);

        var sabor = new Sabor { Nome = "Baunilha", PrecoPorKg = 70m, CriadoEm = DateTime.UtcNow };
        db.Sabores.Add(sabor);
        await db.SaveChangesAsync();

        var ficha = new FichaTecnica { SaborId = sabor.Id, CriadoEm = DateTime.UtcNow };
        db.FichasTecnicas.Add(ficha);
        await db.SaveChangesAsync();

        db.FichaTecnicaItens.Add(new FichaTecnicaItem { FichaTecnicaId = ficha.Id, IngredienteId = farinha.Id, QuantidadePorKg = 0.5m });
        await db.SaveChangesAsync();

        db.Pedidos.Add(new Pedido
        {
            ClienteId = cliente.Id,
            SaborId = sabor.Id,
            Peso = 1m,
            PrecoPorKgUtilizado = sabor.PrecoPorKg,
            ValorTotal = sabor.PrecoPorKg,
            DataEntrega = data,
            StatusPedido = StatusPedido.Cancelado,
            CriadoEm = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var service = new AgendaService(db);

        var resultado = await service.ObterAgendaDoDiaAsync(data);

        Assert.Empty(resultado.Pedidos);
        Assert.Empty(resultado.FichaTecnicaTotalDoDia);
    }
}
