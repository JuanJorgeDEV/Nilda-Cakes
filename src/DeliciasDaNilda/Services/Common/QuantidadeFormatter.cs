using System.Globalization;

namespace DeliciasDaNilda.Services.Common;

/// <summary>
/// Formata quantidades de ingrediente de forma legível: abaixo de 1 kg/L
/// mostra em g/ml ("120 g"), a partir disso em kg/L com até 2 casas e sem
/// zeros sobrando ("1,5 kg"). O banco continua guardando na unidade do
/// ingrediente — isto é só apresentação.
/// </summary>
public static class QuantidadeFormatter
{
    private static readonly CultureInfo PtBr = new("pt-BR");

    public static string Formatar(decimal valor, string? unidade)
    {
        var u = (unidade ?? string.Empty).Trim().ToLowerInvariant();
        var abs = Math.Abs(valor);

        return u switch
        {
            "kg" when abs < 1m => $"{Numero(valor * 1000m, 0)} g",
            "kg" => $"{Numero(valor, 2)} kg",
            "l" when abs < 1m => $"{Numero(valor * 1000m, 0)} ml",
            "l" => $"{Numero(valor, 2)} L",
            "g" when abs >= 1000m => $"{Numero(valor / 1000m, 2)} kg",
            "ml" when abs >= 1000m => $"{Numero(valor / 1000m, 2)} L",
            "g" or "ml" => $"{Numero(valor, 0)} {u}",
            "un" => $"{Numero(valor, 1)} un",
            _ => $"{Numero(valor, 2)} {unidade}".TrimEnd()
        };
    }

    /// <summary>
    /// Converte um valor digitado numa unidade "de tela" (g, kg, ml, L, un)
    /// para a unidade em que o ingrediente é armazenado.
    /// </summary>
    public static decimal ConverterParaUnidadeBase(decimal valor, string unidadeDigitada, string unidadeBase)
    {
        var de = unidadeDigitada.Trim().ToLowerInvariant();
        var para = unidadeBase.Trim().ToLowerInvariant();
        return (de, para) switch
        {
            ("g", "kg") or ("ml", "l") => valor / 1000m,
            ("kg", "g") or ("l", "ml") => valor * 1000m,
            _ => valor
        };
    }

    /// <summary>Unidade menor usada para digitar (g para kg, ml para L).</summary>
    public static string? UnidadeMenor(string unidadeBase) => unidadeBase.Trim().ToLowerInvariant() switch
    {
        "kg" => "g",
        "l" => "ml",
        _ => null
    };

    private static string Numero(decimal valor, int casas)
        => Math.Round(valor, casas, MidpointRounding.AwayFromZero).ToString("#,0.##", PtBr);
}
