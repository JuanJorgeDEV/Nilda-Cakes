namespace DeliciasDaNilda.Domain.Enums;

/// <summary>
/// Status financeiro do pedido, denormalizado em Pedido.StatusPagamento
/// e sincronizado a partir dos Pagamentos (RN09/RN10).
/// </summary>
public enum StatusPagamento
{
    SemPagamento,
    SinalPago,
    PagoIntegral
}
