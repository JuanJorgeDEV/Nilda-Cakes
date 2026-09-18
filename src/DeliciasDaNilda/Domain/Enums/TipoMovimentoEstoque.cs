namespace DeliciasDaNilda.Domain.Enums;

/// <summary>
/// Tipo de um movimento de estoque, log append-only (RN11, RN12, RN13).
/// </summary>
public enum TipoMovimentoEstoque
{
    Entrada,
    SaidaConfirmacaoPedido,
    AjustePerda,
    AjusteCorrecao
}
