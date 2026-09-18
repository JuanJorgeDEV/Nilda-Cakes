using DeliciasDaNilda.Domain.Enums;

namespace DeliciasDaNilda.Pages.Shared;

/// <summary>
/// Mapeamento puramente visual de status (RN08/RN09/RN10) para classes CSS de badge
/// e rótulo em português. Não contém regra de negócio — apenas apresentação.
/// </summary>
public static class StatusBadgeHelper
{
    public static (string CssClass, string Label) Pedido(StatusPedido status) => status switch
    {
        StatusPedido.PendenteConfirmacao => ("dn-badge-pendente", "Pendente de Confirmação"),
        StatusPedido.Confirmado => ("dn-badge-confirmado", "Confirmado"),
        StatusPedido.Cancelado => ("dn-badge-cancelado", "Cancelado"),
        StatusPedido.Concluido => ("dn-badge-concluido", "Concluído"),
        _ => ("dn-badge-cancelado", status.ToString())
    };

    public static (string CssClass, string Label) Pagamento(StatusPagamento status) => status switch
    {
        StatusPagamento.SemPagamento => ("dn-badge-pendente", "Sem pagamento"),
        StatusPagamento.SinalPago => ("dn-badge-confirmado", "Sinal pago"),
        StatusPagamento.PagoIntegral => ("dn-badge-concluido", "Pago integral"),
        _ => ("dn-badge-cancelado", status.ToString())
    };
}
