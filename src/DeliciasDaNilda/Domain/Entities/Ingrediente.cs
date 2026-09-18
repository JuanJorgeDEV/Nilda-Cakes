namespace DeliciasDaNilda.Domain.Entities;

/// <summary>
/// Ingrediente de estoque (RN11, RN12, RN13, RN14, RN15).
/// </summary>
public class Ingrediente
{
    public int Id { get; set; }

    public string Nome { get; set; } = string.Empty;

    /// <summary>Ex.: kg, g, L, ml, un.</summary>
    public string UnidadeMedida { get; set; } = string.Empty;

    /// <summary>
    /// Campo derivado/cache: deve ser sempre igual à soma de
    /// MovimentosEstoque.Quantidade (com sinal) para o ingrediente.
    /// Mantido em sincronia via transação na camada de aplicação
    /// (docs/modelo-de-dados.md, seção 5, item 3).
    /// </summary>
    public decimal SaldoAtual { get; set; }

    /// <summary>Limiar opcional de alerta preventivo (RN15).</summary>
    public decimal? EstoqueMinimoAlerta { get; set; }

    public DateTime CriadoEm { get; set; }

    public ICollection<FichaTecnicaItem> FichaTecnicaItens { get; set; } = new List<FichaTecnicaItem>();

    public ICollection<MovimentoEstoque> MovimentosEstoque { get; set; } = new List<MovimentoEstoque>();
}
