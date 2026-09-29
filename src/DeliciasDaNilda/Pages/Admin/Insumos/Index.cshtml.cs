using System.ComponentModel.DataAnnotations;
using DeliciasDaNilda.Domain.Entities;
using DeliciasDaNilda.Domain.Enums;
using DeliciasDaNilda.Services;
using DeliciasDaNilda.Services.Common;
using Microsoft.AspNetCore.Mvc;

namespace DeliciasDaNilda.Pages.Admin.Insumos;

/// <summary>
/// Estoque em cards por categoria: entrada rápida (RN11), perda/avaria com
/// motivo (RN13), cadastro e edição do insumo (custo, mínimo, categoria) e
/// o aviso de reposição (RN15).
/// </summary>
public class IndexModel : AdminPageModelBase
{
    private readonly IEstoqueService _estoqueService;
    private readonly IIngredienteService _ingredienteService;
    private readonly IProjecaoEstoqueService _projecaoEstoque;

    public IndexModel(IEstoqueService estoqueService, IIngredienteService ingredienteService, IProjecaoEstoqueService projecaoEstoque)
    {
        _estoqueService = estoqueService;
        _ingredienteService = ingredienteService;
        _projecaoEstoque = projecaoEstoque;
    }

    public List<Ingrediente> Ingredientes { get; set; } = new();

    public ProjecaoEstoqueResultado? Projecao { get; set; }

    [BindProperty]
    public IngredienteEntrada Novo { get; set; } = new();

    [TempData]
    public string? MensagemSucesso { get; set; }

    [TempData]
    public string? MensagemErro { get; set; }

    public class IngredienteEntrada
    {
        [Required(ErrorMessage = "Informe o nome do ingrediente.")]
        [StringLength(150)]
        public string Nome { get; set; } = string.Empty;

        public string UnidadeMedida { get; set; } = "kg";

        public CategoriaIngrediente Categoria { get; set; } = CategoriaIngrediente.Outros;

        public decimal? EstoqueMinimoAlerta { get; set; }

        public decimal? CustoPorUnidade { get; set; }
    }

    public async Task OnGetAsync(CancellationToken ct)
    {
        Ingredientes = await _ingredienteService.ListarAsync(ct);
        Projecao = await _projecaoEstoque.ProjetarAsync(ct: ct);
    }

    public async Task<IActionResult> OnPostNovoAsync(CancellationToken ct)
    {
        var resultado = await _ingredienteService.CriarAsync(new DadosIngrediente(
            Novo.Nome, Novo.UnidadeMedida, Novo.Categoria, Novo.EstoqueMinimoAlerta, Novo.CustoPorUnidade), ct);

        return Voltar(resultado, $"Insumo \"{Novo.Nome.Trim()}\" cadastrado. Registre a primeira entrada no card dele.",
            resultado is ResultadoOperacao<Ingrediente> { Valor: { } ing } ? ing.Id : null);
    }

    public async Task<IActionResult> OnPostEditarAsync(int id, IngredienteEntrada dados, CancellationToken ct)
    {
        var resultado = await _ingredienteService.AtualizarAsync(id, new DadosIngrediente(
            dados.Nome, string.Empty, dados.Categoria, dados.EstoqueMinimoAlerta, dados.CustoPorUnidade), ct);
        return Voltar(resultado, "Insumo atualizado.", id);
    }

    public async Task<IActionResult> OnPostEntradaAsync(int id, decimal? rapido, decimal? quantidade, string? unidade, CancellationToken ct)
    {
        var ingrediente = await _ingredienteService.ObterPorIdAsync(id, ct);
        if (ingrediente is null)
        {
            return Voltar(ResultadoOperacao.Falha("Ingrediente não encontrado."), "", null);
        }

        // Botão rápido já vem na unidade do ingrediente; o campo livre pode vir em g/ml.
        var valor = rapido ?? (quantidade is > 0
            ? QuantidadeFormatter.ConverterParaUnidadeBase(quantidade.Value, unidade ?? ingrediente.UnidadeMedida, ingrediente.UnidadeMedida)
            : 0m);

        if (valor <= 0)
        {
            return Voltar(ResultadoOperacao.Falha("Informe a quantidade que chegou."), "", id);
        }

        // RN11: entrada de insumo incrementa o saldo.
        var resultado = await _estoqueService.RegistrarEntradaAsync(id, valor, AdministradorId, "Entrada pelo painel", ct);
        return Voltar(resultado, $"+{QuantidadeFormatter.Formatar(valor, ingrediente.UnidadeMedida)} de {ingrediente.Nome}.", id);
    }

    public async Task<IActionResult> OnPostPerdaAsync(int id, decimal? quantidade, string? unidade, string? motivo, string? motivoOutro, CancellationToken ct)
    {
        var ingrediente = await _ingredienteService.ObterPorIdAsync(id, ct);
        if (ingrediente is null)
        {
            return Voltar(ResultadoOperacao.Falha("Ingrediente não encontrado."), "", null);
        }

        var valor = quantidade is > 0
            ? QuantidadeFormatter.ConverterParaUnidadeBase(quantidade.Value, unidade ?? ingrediente.UnidadeMedida, ingrediente.UnidadeMedida)
            : 0m;
        var motivoFinal = motivo == "Outro" || string.IsNullOrWhiteSpace(motivo) ? motivoOutro?.Trim() : motivo;

        if (valor <= 0)
        {
            return Voltar(ResultadoOperacao.Falha("Informe quanto foi perdido."), "", id);
        }

        // RN13: perda/avaria manual — motivo obrigatório (validado no serviço).
        var resultado = await _estoqueService.RegistrarAjustePerdaAsync(id, valor, AdministradorId, motivoFinal ?? string.Empty, ct);
        return Voltar(resultado, $"Perda de {QuantidadeFormatter.Formatar(valor, ingrediente.UnidadeMedida)} de {ingrediente.Nome} registrada.", id);
    }

    private IActionResult Voltar(ResultadoOperacao resultado, string sucesso, int? ingredienteId)
    {
        if (resultado.Sucesso)
        {
            MensagemSucesso = sucesso;
        }
        else
        {
            MensagemErro = resultado.Erro;
        }

        return ingredienteId is null
            ? RedirectToPage()
            : RedirectToPage(null, null, new { destaque = ingredienteId }, $"ing-{ingredienteId}");
    }
}
