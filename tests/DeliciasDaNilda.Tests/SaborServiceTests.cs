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
}
