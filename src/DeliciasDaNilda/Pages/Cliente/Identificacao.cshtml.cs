using DeliciasDaNilda.Services;
using DeliciasDaNilda.Services.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DeliciasDaNilda.Pages.Cliente;

/// <summary>
/// RN01 — identificação simplificada, sem senha, pelo número de WhatsApp.
/// Número já cadastrado entra direto; número novo faz o primeiro cadastro
/// com nome e CEP (para a Nilda saber a região de entrega).
/// </summary>
public class IdentificacaoModel : PageModel
{
    private readonly IClienteService _clienteService;

    public IdentificacaoModel(IClienteService clienteService)
    {
        _clienteService = clienteService;
    }

    [BindProperty]
    public string WhatsApp { get; set; } = string.Empty;

    [BindProperty]
    public string? Nome { get; set; }

    [BindProperty]
    public string? Cep { get; set; }

    [BindProperty]
    public string? Bairro { get; set; }

    [BindProperty]
    public string? Cidade { get; set; }

    /// <summary>"numero" (1ª etapa) ou "cadastro" (número novo ou sem CEP).</summary>
    public string Etapa { get; set; } = "numero";

    /// <summary>Número já conhecido, só faltando o CEP: o nome não é pedido de novo.</summary>
    public bool CompletandoCadastro { get; set; }

    public string? Erro { get; set; }

    public IActionResult OnGet(bool trocar = false)
    {
        if (trocar)
        {
            HttpContext.Session.Remove("ClienteId");
            HttpContext.Session.Remove("ClienteNome");
            return Page();
        }

        return HttpContext.Session.GetInt32("ClienteId") is not null
            ? RedirectToPage("/Cliente/NovoPedido")
            : Page();
    }

    public async Task<IActionResult> OnPostNumeroAsync(CancellationToken ct)
    {
        if (WhatsAppHelper.Normalizar(WhatsApp).Length < 12)
        {
            Erro = "Digite o WhatsApp com DDD, por exemplo (16) 99999-9999.";
            return Page();
        }

        var cliente = await _clienteService.ObterPorWhatsAppAsync(WhatsApp, ct);
        if (cliente is not null && !string.IsNullOrWhiteSpace(cliente.Cep))
        {
            return Entrar(cliente);
        }

        Etapa = "cadastro";
        CompletandoCadastro = cliente is not null;
        Nome = cliente?.NomeCompleto;
        return Page();
    }

    public async Task<IActionResult> OnPostCadastroAsync(CancellationToken ct)
    {
        var resultado = await _clienteService.CadastrarAsync(
            new DadosCadastroCliente(Nome ?? string.Empty, WhatsApp, Cep, Bairro, Cidade), ct);

        if (!resultado.Sucesso)
        {
            Erro = resultado.Erro;
            Etapa = "cadastro";
            CompletandoCadastro = (await _clienteService.ObterPorWhatsAppAsync(WhatsApp, ct)) is not null;
            return Page();
        }

        return Entrar(resultado.Valor!);
    }

    private IActionResult Entrar(Domain.Entities.Cliente cliente)
    {
        HttpContext.Session.SetInt32("ClienteId", cliente.Id);
        HttpContext.Session.SetString("ClienteNome", cliente.NomeCompleto);
        return RedirectToPage("/Cliente/NovoPedido");
    }
}
