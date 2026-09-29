using System.ComponentModel.DataAnnotations;
using DeliciasDaNilda.Domain.Entities;
using DeliciasDaNilda.Services;
using DeliciasDaNilda.Services.Common;
using Microsoft.AspNetCore.Mvc;

namespace DeliciasDaNilda.Pages.Admin.Sabores;

/// <summary>
/// Cadastro e edição de sabor: preço por kg (RN03), ficha técnica (RN04)
/// com quantidade em g/kg, custo extra opcional e foto.
/// </summary>
public class EditarModel : AdminPageModelBase
{
    private const long TamanhoMaximoFoto = 3 * 1024 * 1024;
    private static readonly string[] ExtensoesFoto = { ".jpg", ".jpeg", ".png", ".webp" };

    private readonly ISaborService _saborService;
    private readonly IIngredienteService _ingredienteService;
    private readonly IWebHostEnvironment _ambiente;

    public EditarModel(ISaborService saborService, IIngredienteService ingredienteService, IWebHostEnvironment ambiente)
    {
        _saborService = saborService;
        _ingredienteService = ingredienteService;
        _ambiente = ambiente;
    }

    [BindProperty(SupportsGet = true)]
    public int? Id { get; set; }

    [BindProperty]
    public EntradaModel Entrada { get; set; } = new();

    [BindProperty]
    public IFormFile? Foto { get; set; }

    [BindProperty]
    public bool RemoverFoto { get; set; }

    public string? ImagemAtual { get; set; }

    public List<Ingrediente> Ingredientes { get; set; } = new();

    public class EntradaModel
    {
        [Required(ErrorMessage = "Informe o nome do sabor.")]
        [StringLength(150)]
        [Display(Name = "Nome do sabor")]
        public string Nome { get; set; } = string.Empty;

        [StringLength(300)]
        [Display(Name = "Descrição para o cliente (opcional)")]
        public string? Descricao { get; set; }

        [Required(ErrorMessage = "Informe o preço por kg.")]
        [Range(0.01, 100000, ErrorMessage = "Preço por kg deve ser maior que zero.")]
        [Display(Name = "Preço por kg (R$)")]
        public decimal PrecoPorKg { get; set; }

        [Range(0, 100000, ErrorMessage = "O custo extra não pode ser negativo.")]
        [Display(Name = "Custo extra por kg (R$, opcional)")]
        public decimal? CustoExtraPorKg { get; set; }

        [Display(Name = "Disponível para pedidos")]
        public bool Disponivel { get; set; } = true;

        public List<ItemEntrada> Itens { get; set; } = new();
    }

    public class ItemEntrada
    {
        public int IngredienteId { get; set; }

        public decimal? Quantidade { get; set; }

        /// <summary>Unidade em que a quantidade foi digitada (g, kg, ml, L, un).</summary>
        public string? Unidade { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        Ingredientes = await _ingredienteService.ListarAsync(ct);

        if (Id is not null)
        {
            var sabor = await _saborService.ObterPorIdAsync(Id.Value, ct);
            if (sabor is null)
            {
                return RedirectToPage("/Admin/Sabores/Cadastro");
            }

            ImagemAtual = sabor.ImagemUrl;
            Entrada = new EntradaModel
            {
                Nome = sabor.Nome,
                Descricao = sabor.Descricao,
                PrecoPorKg = sabor.PrecoPorKg,
                CustoExtraPorKg = sabor.CustoExtraPorKg,
                Disponivel = sabor.Disponivel,
                Itens = sabor.FichaTecnica?.Itens
                    .OrderByDescending(i => i.QuantidadePorKg)
                    .Select(i => ParaTela(i.IngredienteId, i.QuantidadePorKg, i.Ingrediente.UnidadeMedida))
                    .ToList() ?? new()
            };
        }

        CompletarLinhas();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        Ingredientes = await _ingredienteService.ListarAsync(ct);
        var unidadesPorId = Ingredientes.ToDictionary(i => i.Id, i => i.UnidadeMedida);

        // Linhas em branco do formulário dinâmico são ignoradas; a quantidade
        // digitada em g/ml é convertida para a unidade do ingrediente.
        var itens = Entrada.Itens
            .Where(i => i.IngredienteId > 0 && i.Quantidade is > 0 && unidadesPorId.ContainsKey(i.IngredienteId))
            .Select(i => new ItemFichaTecnicaEntrada(
                i.IngredienteId,
                QuantidadeFormatter.ConverterParaUnidadeBase(i.Quantidade!.Value, i.Unidade ?? unidadesPorId[i.IngredienteId], unidadesPorId[i.IngredienteId])))
            .ToList();

        var erroFoto = ValidarFoto();
        if (erroFoto is not null)
        {
            ModelState.AddModelError(nameof(Foto), erroFoto);
        }

        if (!ModelState.IsValid)
        {
            await RecarregarImagemAsync(ct);
            CompletarLinhas();
            return Page();
        }

        var dados = new DadosSabor(Entrada.Nome, Entrada.PrecoPorKg, Entrada.Disponivel, itens, Entrada.Descricao, Entrada.CustoExtraPorKg);
        var resultado = Id is null
            ? await _saborService.CriarAsync(dados, ct)
            : await _saborService.AtualizarAsync(Id.Value, dados, ct);

        if (!resultado.Sucesso)
        {
            ModelState.AddModelError(string.Empty, resultado.Erro!);
            await RecarregarImagemAsync(ct);
            CompletarLinhas();
            return Page();
        }

        var sabor = resultado.Valor!;
        if (Foto is { Length: > 0 })
        {
            var novaUrl = await SalvarFotoAsync(sabor.Id, Foto, ct);
            ApagarFoto(sabor.ImagemUrl);
            await _saborService.DefinirImagemAsync(sabor.Id, novaUrl, ct);
        }
        else if (RemoverFoto && sabor.ImagemUrl is not null)
        {
            ApagarFoto(sabor.ImagemUrl);
            await _saborService.DefinirImagemAsync(sabor.Id, null, ct);
        }

        TempData["MensagemSucesso"] = Id is null ? $"Sabor \"{sabor.Nome}\" cadastrado." : $"Sabor \"{sabor.Nome}\" atualizado.";
        return RedirectToPage("/Admin/Sabores/Cadastro");
    }

    private static ItemEntrada ParaTela(int ingredienteId, decimal quantidade, string unidadeBase)
    {
        // Ingredientes em kg/L aparecem em g/ml (0,12 kg → 120 g).
        var menor = QuantidadeFormatter.UnidadeMenor(unidadeBase);
        return menor is null
            ? new ItemEntrada { IngredienteId = ingredienteId, Quantidade = decimal.Round(quantidade, 3), Unidade = unidadeBase }
            : new ItemEntrada { IngredienteId = ingredienteId, Quantidade = decimal.Round(quantidade * 1000m, 1), Unidade = menor };
    }

    private void CompletarLinhas()
    {
        while (Entrada.Itens.Count < 3)
        {
            Entrada.Itens.Add(new ItemEntrada());
        }
    }

    private async Task RecarregarImagemAsync(CancellationToken ct)
    {
        if (Id is not null)
        {
            ImagemAtual = (await _saborService.ObterPorIdAsync(Id.Value, ct))?.ImagemUrl;
        }
    }

    private string? ValidarFoto()
    {
        if (Foto is null || Foto.Length == 0)
        {
            return null;
        }

        if (Foto.Length > TamanhoMaximoFoto)
        {
            return "A foto deve ter no máximo 3 MB.";
        }

        var extensao = Path.GetExtension(Foto.FileName).ToLowerInvariant();
        if (!ExtensoesFoto.Contains(extensao) || !Foto.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return "Envie uma foto em JPG, PNG ou WEBP.";
        }

        return null;
    }

    private async Task<string> SalvarFotoAsync(int saborId, IFormFile foto, CancellationToken ct)
    {
        var pasta = Path.Combine(_ambiente.WebRootPath, "uploads", "sabores");
        Directory.CreateDirectory(pasta);

        // Nome gerado pelo servidor — nunca o nome enviado pelo navegador.
        var extensao = Path.GetExtension(foto.FileName).ToLowerInvariant();
        var nomeArquivo = $"{saborId}-{Guid.NewGuid():N}"[..20] + extensao;
        await using var destino = System.IO.File.Create(Path.Combine(pasta, nomeArquivo));
        await foto.CopyToAsync(destino, ct);
        return $"/uploads/sabores/{nomeArquivo}";
    }

    private void ApagarFoto(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || !url.StartsWith("/uploads/sabores/", StringComparison.Ordinal))
        {
            return;
        }

        var caminho = Path.Combine(_ambiente.WebRootPath, "uploads", "sabores", Path.GetFileName(url));
        if (System.IO.File.Exists(caminho))
        {
            System.IO.File.Delete(caminho);
        }
    }
}
