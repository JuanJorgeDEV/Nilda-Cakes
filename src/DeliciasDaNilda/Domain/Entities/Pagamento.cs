using DeliciasDaNilda.Domain.Enums;

namespace DeliciasDaNilda.Domain.Entities;

/// <summary>
/// Registro de baixa financeira de um pedido (sinal, saldo, integral) — RN09/RN10.
/// </summary>
public class Pagamento
{
    public int Id { get; set; }

    public int PedidoId { get; set; }

    public Pedido Pedido { get; set; } = null!;

    public TipoPagamento Tipo { get; set; }

    public decimal Valor { get; set; }

    /// <summary>Ex.: Dinheiro, Pix, Cartao — informativo, não é regra de negócio no MVP.</summary>
    public string? FormaPagamento { get; set; }

    /// <summary>RN10: só o ADM dá baixa financeira.</summary>
    public int RegistradoPorAdministradorId { get; set; }

    public Administrador RegistradoPorAdministrador { get; set; } = null!;

    public DateTime CriadoEm { get; set; }
}
