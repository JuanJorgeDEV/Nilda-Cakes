using DeliciasDaNilda.Data;
using DeliciasDaNilda.Domain.Entities;
using DeliciasDaNilda.Services;
using Microsoft.EntityFrameworkCore;

namespace DeliciasDaNilda.Tests;

/// <summary>Testes de cadastro de Sabor + Ficha Técnica (RN04).</summary>
public class SaborServiceTests
{
    private static AppDbContext CriarContexto()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<Ingrediente> SeedIngredienteAsync(AppDbContext db, string nome = "Farinha")
    {
        var ingrediente = new Ingrediente { Nome = $"{nome} {Guid.NewGuid()}", UnidadeMedida = "kg", CriadoEm = DateTime.UtcNow };
        db.Ingredientes.Add(ingrediente);
        await db.SaveChangesAsync();
        return ingrediente;
    }

    [Fact]
    public async Task CriarComFichaTecnicaAsync_ComDadosValidos_CriaSaborEFicha()
    {
        await using var db = CriarContexto();
        var farinha = await SeedIngredienteAsync(db, "Farinha");
        var acucar = await SeedIngredienteAsync(db, "Açúcar");
        var service = new SaborService(db);

        var itens = new List<ItemFichaTecnicaEntrada>
        {
            new(farinha.Id, 0.5m),
            new(acucar.Id, 0.3m)
        };

        var resultado = await service.CriarComFichaTecnicaAsync("Chocolate", 90m, disponivel: true, itens);

        Assert.True(resultado.Sucesso);
        var sabor = resultado.Valor!;
        Assert.Equal("Chocolate", sabor.Nome);
        Assert.NotNull(sabor.FichaTecnica);
        Assert.Equal(2, sabor.FichaTecnica!.Itens.Count);

        var saborPersistido = await db.Sabores
            .Include(s => s.FichaTecnica!)
                .ThenInclude(f => f.Itens)
            .FirstAsync(s => s.Id == sabor.Id);
        Assert.Equal(2, saborPersistido.FichaTecnica!.Itens.Count);
    }

    [Fact]
    public async Task CriarComFichaTecnicaAsync_ComNomeDuplicado_Falha()
    {
        await using var db = CriarContexto();
        var farinha = await SeedIngredienteAsync(db);
        var service = new SaborService(db);
        var itens = new List<ItemFichaTecnicaEntrada> { new(farinha.Id, 0.5m) };

        var primeiro = await service.CriarComFichaTecnicaAsync("Baunilha", 80m, true, itens);
        Assert.True(primeiro.Sucesso);

        var outroIngrediente = await SeedIngredienteAsync(db, "Leite");
        var itensSegundo = new List<ItemFichaTecnicaEntrada> { new(outroIngrediente.Id, 0.2m) };
        var duplicado = await service.CriarComFichaTecnicaAsync("Baunilha", 85m, true, itensSegundo);

        Assert.False(duplicado.Sucesso);
        Assert.Equal(1, await db.Sabores.CountAsync());
    }

    [Fact]
    public async Task CriarComFichaTecnicaAsync_ComIngredienteRepetidoNaLista_Falha()
    {
        await using var db = CriarContexto();
        var farinha = await SeedIngredienteAsync(db);
        var service = new SaborService(db);

        var itens = new List<ItemFichaTecnicaEntrada>
        {
            new(farinha.Id, 0.5m),
            new(farinha.Id, 0.2m)
        };

        var resultado = await service.CriarComFichaTecnicaAsync("Morango", 95m, true, itens);

        Assert.False(resultado.Sucesso);
        Assert.Equal(0, await db.Sabores.CountAsync());
    }

    [Fact]
    public async Task CriarComFichaTecnicaAsync_SemItens_Falha()
    {
        await using var db = CriarContexto();
        var service = new SaborService(db);

        var resultado = await service.CriarComFichaTecnicaAsync("Limão", 70m, true, Array.Empty<ItemFichaTecnicaEntrada>());

        Assert.False(resultado.Sucesso);
    }

    [Fact]
    public async Task AtualizarAsync_SincronizaItensDaFicha_EAlteraDados()
    {
        await using var db = CriarContexto();
        var farinha = await SeedIngredienteAsync(db, "Farinha");
        var acucar = await SeedIngredienteAsync(db, "Açúcar");
        var ovo = await SeedIngredienteAsync(db, "Ovo");
        var service = new SaborService(db);
        var criado = (await service.CriarComFichaTecnicaAsync("Chocolate", 90m, true,
            new List<ItemFichaTecnicaEntrada> { new(farinha.Id, 0.5m), new(acucar.Id, 0.3m) })).Valor!;

        var resultado = await service.AtualizarAsync(criado.Id, new DadosSabor(
            "Chocolate belga", 110m, false,
            new List<ItemFichaTecnicaEntrada> { new(farinha.Id, 0.4m), new(ovo.Id, 0.2m) },
            "Massa fofinha", 5m));

        Assert.True(resultado.Sucesso);
        db.ChangeTracker.Clear();
        var sabor = await service.ObterPorIdAsync(criado.Id);
        Assert.Equal("Chocolate belga", sabor!.Nome);
        Assert.Equal(110m, sabor.PrecoPorKg);
        Assert.False(sabor.Disponivel);
        Assert.Equal("Massa fofinha", sabor.Descricao);
        Assert.Equal(5m, sabor.CustoExtraPorKg);
        var itens = sabor.FichaTecnica!.Itens.ToDictionary(i => i.IngredienteId, i => i.QuantidadePorKg);
        Assert.Equal(2, itens.Count);
        Assert.Equal(0.4m, itens[farinha.Id]);
        Assert.Equal(0.2m, itens[ovo.Id]);
        Assert.False(itens.ContainsKey(acucar.Id));
    }

    [Fact]
    public async Task ExcluirAsync_SomeDaListagem_MasPedidoAntigoContinuaComOSabor_ENomePodeSerReusado()
    {
        await using var db = CriarContexto();
        var farinha = await SeedIngredienteAsync(db);
        var service = new SaborService(db);
        var sabor = (await service.CriarComFichaTecnicaAsync("Limão", 80m, true,
            new List<ItemFichaTecnicaEntrada> { new(farinha.Id, 0.5m) })).Valor!;
        var cliente = new Cliente { NomeCompleto = "Cliente", WhatsApp = "5516999990000", CriadoEm = DateTime.UtcNow };
        db.Clientes.Add(cliente);
        await db.SaveChangesAsync();
        db.Pedidos.Add(new Pedido
        {
            ClienteId = cliente.Id, SaborId = sabor.Id, Peso = 1m, PrecoPorKgUtilizado = 80m, ValorTotal = 80m,
            DataEntrega = DateOnly.FromDateTime(DateTime.Today), CriadoEm = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        Assert.True((await service.ExcluirAsync(sabor.Id)).Sucesso);

        Assert.Empty(await service.ListarAsync());
        Assert.Null(await service.ObterPorIdAsync(sabor.Id));
        db.ChangeTracker.Clear();
        var pedido = await db.Pedidos.Include(p => p.Sabor).SingleAsync();
        Assert.Equal("Limão", pedido.Sabor.Nome);
        Assert.True((await service.CriarComFichaTecnicaAsync("Limão", 85m, true,
            new List<ItemFichaTecnicaEntrada> { new(farinha.Id, 0.5m) })).Sucesso);
    }

    [Fact]
    public async Task AlternarDisponibilidadeAsync_InverteOStatus()
    {
        await using var db = CriarContexto();
        var farinha = await SeedIngredienteAsync(db);
        var service = new SaborService(db);
        var sabor = (await service.CriarComFichaTecnicaAsync("Coco", 70m, true,
            new List<ItemFichaTecnicaEntrada> { new(farinha.Id, 0.5m) })).Valor!;

        await service.AlternarDisponibilidadeAsync(sabor.Id);
        Assert.False((await service.ObterPorIdAsync(sabor.Id))!.Disponivel);
        await service.AlternarDisponibilidadeAsync(sabor.Id);
        Assert.True((await service.ObterPorIdAsync(sabor.Id))!.Disponivel);
    }

    [Fact]
    public void CalcularCustoPorKg_SomaIngredientesComCusto_MaisExtra()
    {
        var sabor = new Sabor
        {
            CustoExtraPorKg = 3m,
            FichaTecnica = new FichaTecnica
            {
                Itens =
                {
                    new FichaTecnicaItem { QuantidadePorKg = 0.5m, Ingrediente = new Ingrediente { CustoPorUnidade = 10m } },
                    new FichaTecnicaItem { QuantidadePorKg = 0.2m, Ingrediente = new Ingrediente { CustoPorUnidade = 25m } },
                    new FichaTecnicaItem { QuantidadePorKg = 1m, Ingrediente = new Ingrediente { CustoPorUnidade = null } }
                }
            }
        };

        Assert.Equal(13m, SaborService.CalcularCustoPorKg(sabor)); // 5 + 5 + 0 + 3
        Assert.True(SaborService.CustoIncompleto(sabor));
    }
}
