using System.ComponentModel.DataAnnotations;

namespace DeliciasDaNilda.Domain.Enums;

/// <summary>Categoria de insumo, usada só para organizar o estoque em abas.</summary>
public enum CategoriaIngrediente
{
    [Display(Name = "Laticínios")]
    Laticinios = 1,

    [Display(Name = "Farinhas e secos")]
    FarinhasESecos = 2,

    [Display(Name = "Açúcares")]
    Acucares = 3,

    [Display(Name = "Chocolates")]
    Chocolates = 4,

    [Display(Name = "Frutas")]
    Frutas = 5,

    [Display(Name = "Biscoitos")]
    Biscoitos = 6,

    [Display(Name = "Recheios e cremes")]
    RecheiosECremes = 7,

    [Display(Name = "Embalagens")]
    Embalagens = 8,

    [Display(Name = "Outros")]
    Outros = 99
}
