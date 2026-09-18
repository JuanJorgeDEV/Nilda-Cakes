namespace DeliciasDaNilda.Domain.Enums;

/// <summary>
/// Status do ciclo de vida do pedido (RN08). Conjunto fechado conforme
/// docs/modelo-de-dados.md, seção 5, item 5 — não incluir "EmProducao" no MVP.
/// </summary>
public enum StatusPedido
{
    PendenteConfirmacao,
    Confirmado,
    Cancelado,
    Concluido
}
