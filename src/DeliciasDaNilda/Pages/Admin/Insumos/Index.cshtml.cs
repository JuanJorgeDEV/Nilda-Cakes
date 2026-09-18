using System.ComponentModel.DataAnnotations;
using DeliciasDaNilda.Domain.Entities;
using DeliciasDaNilda.Services;
using Microsoft.AspNetCore.Mvc;

namespace DeliciasDaNilda.Pages.Admin.Insumos;

/// <summary>
/// Entrada de insumos (RN11) e lançamento de perdas/ajustes (RN13), via
/// IEstoqueService. Cadastro básico de Ingrediente via IIngredienteService
/// (nome único).
/// </summary>
public class IndexModel : AdminPageModelBase
{
    private readonly IEstoqueService _estoqueService;
    private readonly IIngredienteService _ingredienteService;

    public IndexModel(IEstoqueService estoqueService, IIngredienteService ingredienteService)
    {
        _estoqueService = estoqueService;
        _ingredienteService = ingredienteService;
    }

    [BindProperty]
    public NovoIngredienteEntrada NovoIngrediente { get; set; } = new();

    [BindProperty]
    public MovimentoEntrada Entrada { get; set; } = new();

    [BindProperty]
    public MovimentoEntrada Ajuste { get; set; } = new();

    public List<Ingrediente> Ingredientes { get; set; } = new();

    public string? MensagemSucesso { get; set; }

    public string? MensagemErro { get; set; }

    public class NovoIngredienteEntrada
    {
        [Required(ErrorMessage = "Informe o nome do ingrediente.")]
        public string Nome { get; set; } = string.Empty;

        [Required(ErrorMessage = "Informe a unidade de medida.")]
        [Display(Name = "Unidade de medida (kg, g, L, ml, un)")]
        public string UnidadeMedida { get; set; } = string.Empty;

        [Display(Name = "Estoque mínimo de alerta (opcional)")]
        public decimal? EstoqueMinimoAlerta { get; set; }
    }

    public class MovimentoEntrada
    {
        [Required(ErrorMessage = "Selecione o ingrediente.")]
        public int IngredienteId { get; set; }

        [Required(ErrorMessage = "Informe a quantidade.")]
        [Range(0.0001, 1000000, ErrorMessage = "Quantidade deve ser maior que zero.")]
        public decimal Quantidade { get; set; }

        public string? Motivo { get; set; }
    }

    public async Task OnGetAsync(CancellationToken ct)
    {
        await CarregarIngredientesAsync(ct);
    }

    public async Task<IActionResult> OnPostNovoIngredienteAsync(CancellationToken ct)
    {
        var resultado = await _ingredienteService.CriarAsync(
            NovoIngrediente.Nome, NovoIngrediente.UnidadeMedida, NovoIngrediente.EstoqueMinimoAlerta, ct);

        if (!resultado.Sucesso)
        {
            MensagemErro = resultado.Erro;
        }
        else
        {
            MensagemSucesso = "Ingrediente cadastrado.";
        }

        await CarregarIngredientesAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostEntradaAsync(CancellationToken ct)
    {
        // RN11: entrada de insumo incrementa o saldo.
        var resultado = await _estoqueService.RegistrarEntradaAsync(
            Entrada.IngredienteId, Entrada.Quantidade, AdministradorId, Entrada.Motivo, ct);

        if (!resultado.Sucesso)
        {
            MensagemErro = resultado.Erro;
        }
        else
        {
            MensagemSucesso = "Entrada de insumo registrada.";
        }

        await CarregarIngredientesAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostAjusteAsync(CancellationToken ct)
    {
        // RN13: ajuste manual de perda/avaria — motivo obrigatório.
        var resultado = await _estoqueService.RegistrarAjustePerdaAsync(
            Ajuste.IngredienteId, Ajuste.Quantidade, AdministradorId, Ajuste.Motivo ?? string.Empty, ct);

        if (!resultado.Sucesso)
        {
            MensagemErro = resultado.Erro;
        }
        else
        {
            MensagemSucesso = "Perda/ajuste registrado.";
        }

        await CarregarIngredientesAsync(ct);
        return Page();
    }

    private async Task CarregarIngredientesAsync(CancellationToken ct)
    {
        Ingredientes = await _ingredienteService.ListarAsync(ct);
    }
}
