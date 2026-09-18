namespace DeliciasDaNilda.Domain.Entities;

/// <summary>
/// Sabor/produto do cardápio. Base do cálculo de preço (RN03) e ponto de
/// entrada para a ficha técnica de consumo de ingredientes (RN04).
/// </summary>
public class Sabor
{
    public int Id { get; set; }

    public string Nome { get; set; } = string.Empty;

    /// <summary>Base do cálculo de RN03 (ValorTotal = Peso x PrecoPorKg).</summary>
    public decimal PrecoPorKg { get; set; }

    /// <summary>Permite pausar o sabor sem apagar histórico.</summary>
    public bool Disponivel { get; set; } = true;

    public DateTime CriadoEm { get; set; }

    public DateTime? AtualizadoEm { get; set; }

    public FichaTecnica? FichaTecnica { get; set; }

    public ICollection<Pedido> Pedidos { get; set; } = new List<Pedido>();
}
