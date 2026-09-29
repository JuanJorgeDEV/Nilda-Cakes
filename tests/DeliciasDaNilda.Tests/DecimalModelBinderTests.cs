using DeliciasDaNilda.Common;

namespace DeliciasDaNilda.Tests;

/// <summary>Formulários mandam "0.5" (input number) ou "0,5" (digitado em pt-BR): os dois valem 0,5.</summary>
public class DecimalModelBinderTests
{
    [Theory]
    [InlineData("0.5", 0.5)]
    [InlineData("0,5", 0.5)]
    [InlineData("85.50", 85.5)]
    [InlineData("85,50", 85.5)]
    [InlineData("1.234,56", 1234.56)]
    [InlineData("12", 12)]
    [InlineData(" 3,25 ", 3.25)]
    public void AceitaPontoEVirgula(string texto, double esperado)
    {
        Assert.True(DecimalModelBinder.TryParse(texto.Trim(), out var valor));
        Assert.Equal((decimal)esperado, valor);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("1,2,3")]
    public void RecusaTextoInvalido(string texto)
    {
        Assert.False(DecimalModelBinder.TryParse(texto, out _));
    }
}
