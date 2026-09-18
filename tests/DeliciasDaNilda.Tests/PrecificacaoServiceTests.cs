using DeliciasDaNilda.Services;

namespace DeliciasDaNilda.Tests;

/// <summary>Testes de RN03 — Valor Total = Peso x Preço por kg.</summary>
public class PrecificacaoServiceTests
{
    private readonly PrecificacaoService _service = new();

    [Fact]
    public void Calcular_MultiplicaPesoPorPrecoPorKg_SemArredondarPesoAntes()
    {
        var resultado = _service.Calcular(pesoKg: 1.5m, precoPorKg: 85.90m);

        Assert.Equal(85.90m, resultado.PrecoPorKgUtilizado);
        Assert.Equal(128.85m, resultado.ValorTotal);
    }

    [Theory]
    [InlineData(0.333, 100, 33.30)]
    [InlineData(2.5, 79.99, 199.98)]
    public void Calcular_ArredondaApenasOValorFinalParaDuasCasas(decimal peso, decimal precoPorKg, decimal valorEsperado)
    {
        var resultado = _service.Calcular(peso, precoPorKg);

        Assert.Equal(valorEsperado, resultado.ValorTotal);
    }

    [Fact]
    public void Calcular_PesoZeroOuNegativo_Lanca()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _service.Calcular(0, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => _service.Calcular(-1, 10));
    }

    [Fact]
    public void Calcular_PrecoZeroOuNegativo_Lanca()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _service.Calcular(1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => _service.Calcular(1, -10));
    }
}
