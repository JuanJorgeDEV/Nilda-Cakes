using DeliciasDaNilda.Domain.Enums;

namespace DeliciasDaNilda.Domain.Entities;

/// <summary>
/// Movimento de estoque: entrada (RN11), saída automática por confirmação de
/// pedido (RN12), ou ajuste/perda manual (RN13). Log append-only — nunca
/// UPDATE/DELETE de movimentos já gravados; correções são um novo movimento
/// de estorno. Base de auditoria e projeção (RN14/RN15).
/// </summary>
public class MovimentoEstoque
{
    /// <summary>Bigint por ser log de alto volume.</summary>
    public long Id { get; set; }

    public int IngredienteId { get; set; }

    public Ingrediente Ingrediente { get; set; } = null!;

    public TipoMovimentoEstoque Tipo { get; set; }

    /// <summary>
    /// Sempre positivo; o Tipo determina o sinal efetivo no saldo
    /// (entrada soma, saída/perda subtrai).
    /// </summary>
    public decimal Quantidade { get; set; }

    /// <summary>
    /// Preenchido somente quando Tipo = SaidaConfirmacaoPedido (RN12);
    /// rastreia qual pedido originou o abate.
    /// </summary>
    public int? PedidoId { get; set; }

    public Pedido? Pedido { get; set; }

    /// <summary>
    /// Obrigatório na prática para AjustePerda (validado na camada de aplicação).
    /// </summary>
    public string? Motivo { get; set; }

    /// <summary>
    /// Quem registrou o movimento (auditoria); nulo apenas para movimentos
    /// automáticos de sistema, se decidido não atribuir a um ADM.
    /// </summary>
    public int? AdministradorId { get; set; }

    public Administrador? Administrador { get; set; }

    /// <summary>Timestamp do movimento — chave de ordenação para auditoria e projeção.</summary>
    public DateTime CriadoEm { get; set; }
}
