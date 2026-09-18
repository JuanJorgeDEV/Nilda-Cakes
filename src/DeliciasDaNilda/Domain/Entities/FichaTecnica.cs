namespace DeliciasDaNilda.Domain.Entities;

/// <summary>
/// Ficha técnica de um sabor (RN04). Relação 1-1 com Sabor no MVP —
/// não versionada (ver docs/modelo-de-dados.md, seção 5, item 1).
/// </summary>
public class FichaTecnica
{
    public int Id { get; set; }

    public int SaborId { get; set; }

    public Sabor Sabor { get; set; } = null!;

    public DateTime CriadoEm { get; set; }

    /// <summary>Última alteração de composição.</summary>
    public DateTime? AtualizadoEm { get; set; }

    public ICollection<FichaTecnicaItem> Itens { get; set; } = new List<FichaTecnicaItem>();
}
