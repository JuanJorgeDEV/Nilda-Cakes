using DeliciasDaNilda.Services.Common;

namespace DeliciasDaNilda.Tests;

public class QuantidadeFormatterTests
{
    [Theory]
    [InlineData(0.12, "kg", "120 g")]
    [InlineData(0.5, "kg", "500 g")]
    [InlineData(0.1234, "kg", "123 g")]
    [InlineData(1, "kg", "1 kg")]
    [InlineData(1.5, "kg", "1,5 kg")]
    [InlineData(18.95, "kg", "18,95 kg")]
    [InlineData(0.25, "L", "250 ml")]
    [InlineData(2, "L", "2 L")]
    [InlineData(1500, "g", "1,5 kg")]
    [InlineData(2, "un", "2 un")]
    public void Formata(double valor, string unidade, string esperado)
    {
        Assert.Equal(esperado, QuantidadeFormatter.Formatar((decimal)valor, unidade));
    }

    [Theory]
    [InlineData(120, "g", "kg", 0.12)]
    [InlineData(1.5, "kg", "kg", 1.5)]
    [InlineData(250, "ml", "L", 0.25)]
    [InlineData(3, "un", "un", 3)]
    public void ConverteParaUnidadeBase(double valor, string de, string para, double esperado)
    {
        Assert.Equal((decimal)esperado, QuantidadeFormatter.ConverterParaUnidadeBase((decimal)valor, de, para));
    }
}
