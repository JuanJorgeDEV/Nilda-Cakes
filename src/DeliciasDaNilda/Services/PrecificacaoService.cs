namespace DeliciasDaNilda.Services;

/// <summary>
/// Resultado do cálculo de precificação (RN03): o preço/kg efetivamente
/// utilizado (para snapshot em Pedido.PrecoPorKgUtilizado) e o valor total.
/// </summary>
public sealed record PrecificacaoResultado(decimal PrecoPorKgUtilizado, decimal ValorTotal);

public interface IPrecificacaoService
{
    /// <summary>RN03 — Valor Total = Peso (kg) x Preço por kg do Sabor.</summary>
    PrecificacaoResultado Calcular(decimal pesoKg, decimal precoPorKg);
}

/// <summary>
/// Serviço de precificação por peso e sabor (RN03).
/// </summary>
public class PrecificacaoService : IPrecificacaoService
{
    /// <summary>
    /// Precisão decimal do valor final: 2 casas (compatível com moeda e com
    /// Pedido.ValorTotal / Pedido.PrecoPorKgUtilizado, mapeados como
    /// decimal(10,2) — ver docs/modelo-de-dados.md, seção 2.8).
    /// O peso NUNCA é arredondado antes da multiplicação (RN03).
    /// </summary>
    private const int CasasDecimaisValorFinal = 2;

    public PrecificacaoResultado Calcular(decimal pesoKg, decimal precoPorKg)
    {
        if (pesoKg <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pesoKg), "Peso deve ser maior que zero.");
        }

        if (precoPorKg <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(precoPorKg), "Preço por kg deve ser maior que zero.");
        }

        // RN03: Valor Total = Peso (kg) x Preço por kg do Sabor — multiplicação
        // direta sobre o peso original, sem arredondamento prévio.
        var valorTotal = Math.Round(pesoKg * precoPorKg, CasasDecimaisValorFinal, MidpointRounding.AwayFromZero);

        return new PrecificacaoResultado(precoPorKg, valorTotal);
    }
}
