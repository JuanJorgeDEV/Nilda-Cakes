using DeliciasDaNilda.Data;
using DeliciasDaNilda.Services;
using Microsoft.EntityFrameworkCore;

namespace DeliciasDaNilda.Tests;

/// <summary>Testes de cadastro de Ingrediente (base de RN04/RN11/RN12/RN13).</summary>
public class IngredienteServiceTests
{
    private static AppDbContext CriarContexto()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task CriarAsync_ComDadosValidos_CriaIngredienteComSaldoZero()
    {
        await using var db = CriarContexto();
        var service = new IngredienteService(db);

        var resultado = await service.CriarAsync("Farinha de trigo", "kg", estoqueMinimoAlerta: 5m);

        Assert.True(resultado.Sucesso);
        Assert.Equal("Farinha de trigo", resultado.Valor!.Nome);
        Assert.Equal("kg", resultado.Valor.UnidadeMedida);
        Assert.Equal(0m, resultado.Valor.SaldoAtual);
        Assert.Equal(5m, resultado.Valor.EstoqueMinimoAlerta);
    }

    [Fact]
    public async Task CriarAsync_ComNomeDuplicado_Falha()
    {
        await using var db = CriarContexto();
        var service = new IngredienteService(db);

        var primeiro = await service.CriarAsync("Açúcar", "kg");
        Assert.True(primeiro.Sucesso);

        var duplicado = await service.CriarAsync("Açúcar", "kg");

        Assert.False(duplicado.Sucesso);
        Assert.Equal(1, await db.Ingredientes.CountAsync());
    }

    [Fact]
    public async Task CriarAsync_SemNomeOuUnidade_Falha()
    {
        await using var db = CriarContexto();
        var service = new IngredienteService(db);

        var semNome = await service.CriarAsync("", "kg");
        var semUnidade = await service.CriarAsync("Ovos", "");

        Assert.False(semNome.Sucesso);
        Assert.False(semUnidade.Sucesso);
    }
}
