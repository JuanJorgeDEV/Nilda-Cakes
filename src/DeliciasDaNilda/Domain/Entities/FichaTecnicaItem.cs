namespace DeliciasDaNilda.Domain.Entities;

/// <summary>
/// Item de ficha técnica: ingrediente + quantidade necessária por 1kg do sabor
/// (RN04). Base de cálculo de consumo (RN12) e projeção (RN14/RN15).
/// </summary>
public class FichaTecnicaItem
{
    public int Id { get; set; }

    public int FichaTecnicaId { get; set; }

    public FichaTecnica FichaTecnica { get; set; } = null!;

    public int IngredienteId { get; set; }

    public Ingrediente Ingrediente { get; set; } = null!;

    /// <summary>
    /// Quantidade do ingrediente necessária para 1 kg do sabor,
    /// na unidade de medida do ingrediente.
    /// </summary>
    public decimal QuantidadePorKg { get; set; }
}
