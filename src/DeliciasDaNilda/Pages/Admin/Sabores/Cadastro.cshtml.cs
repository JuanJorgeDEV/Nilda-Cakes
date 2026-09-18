using System.ComponentModel.DataAnnotations;
using DeliciasDaNilda.Domain.Entities;
using DeliciasDaNilda.Services;
using Microsoft.AspNetCore.Mvc;

namespace DeliciasDaNilda.Pages.Admin.Sabores;

/// <summary>
/// Cadastro de Sabor + Ficha Técnica (RN04), via ISaborService (que garante
/// nome único e ausência de ingrediente repetido na ficha técnica).
/// </summary>
public class CadastroModel : AdminPageModelBase
{
    private readonly ISaborService _saborService;
    private readonly IIngredienteService _ingredienteService;

    public CadastroModel(ISaborService saborService, IIngredienteService ingredienteService)
    {
        _saborService = saborService;
        _ingredienteService = ingredienteService;
    }

    [BindProperty]
    public EntradaModel Entrada { get; set; } = new();

    public List<Ingrediente> Ingredientes { get; set; } = new();

    public List<Sabor> SaboresCadastrados { get; set; } = new();

    public class EntradaModel
    {
        [Required(ErrorMessage = "Informe o nome do sabor.")]
        [Display(Name = "Nome do sabor")]
        public string Nome { get; set; } = string.Empty;

        [Required(ErrorMessage = "Informe o preço por kg.")]
        [Range(0.01, 100000, ErrorMessage = "Preço por kg deve ser maior que zero.")]
        [Display(Name = "Preço por kg (R$)")]
        public decimal PrecoPorKg { get; set; }

        [Display(Name = "Disponível para pedidos")]
        public bool Disponivel { get; set; } = true;

        public List<ItemFichaTecnicaEntrada> Itens { get; set; } = new();
    }

    public class ItemFichaTecnicaEntrada
    {
        public int IngredienteId { get; set; }

        public decimal QuantidadePorKg { get; set; }
    }

    public async Task OnGetAsync(CancellationToken ct)
    {
        await CarregarDadosDeApoioAsync(ct);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        // Itens sem ingrediente selecionado ou com quantidade zerada são
        // descartados (linhas em branco do formulário dinâmico).
        var itensValidos = Entrada.Itens
            .Where(i => i.IngredienteId > 0 && i.QuantidadePorKg > 0)
            .Select(i => new Services.ItemFichaTecnicaEntrada(i.IngredienteId, i.QuantidadePorKg))
            .ToList();

        if (!ModelState.IsValid)
        {
            await CarregarDadosDeApoioAsync(ct);
            return Page();
        }

        var resultado = await _saborService.CriarComFichaTecnicaAsync(
            Entrada.Nome, Entrada.PrecoPorKg, Entrada.Disponivel, itensValidos, ct);

        if (!resultado.Sucesso)
        {
            ModelState.AddModelError(string.Empty, resultado.Erro!);
            await CarregarDadosDeApoioAsync(ct);
            return Page();
        }

        return RedirectToPage("/Admin/Sabores/Cadastro");
    }

    private async Task CarregarDadosDeApoioAsync(CancellationToken ct)
    {
        Ingredientes = await _ingredienteService.ListarAsync(ct);
        SaboresCadastrados = await _saborService.ListarAsync(ct: ct);

        if (Entrada.Itens.Count == 0)
        {
            // Começa com 3 linhas em branco no formulário dinâmico.
            Entrada.Itens.Add(new ItemFichaTecnicaEntrada());
            Entrada.Itens.Add(new ItemFichaTecnicaEntrada());
            Entrada.Itens.Add(new ItemFichaTecnicaEntrada());
        }
    }
}
