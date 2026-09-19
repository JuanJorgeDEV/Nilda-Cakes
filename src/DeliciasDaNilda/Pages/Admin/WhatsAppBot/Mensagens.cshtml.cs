using DeliciasDaNilda.Services;
using Microsoft.AspNetCore.Mvc;

namespace DeliciasDaNilda.Pages.Admin.WhatsAppBot;

/// <summary>
/// Edição dos textos que o atendimento automático envia. A ordem das
/// perguntas é fixa; só os textos mudam (ver IMensagemBotService).
/// Não corresponde a uma RN numerada.
/// </summary>
public class MensagensModel : AdminPageModelBase
{
    private readonly IMensagemBotService _service;

    public MensagensModel(IMensagemBotService service)
    {
        _service = service;
    }

    public IReadOnlyList<MensagemBotItem> Mensagens { get; private set; } = Array.Empty<MensagemBotItem>();

    [TempData]
    public string? Sucesso { get; set; }

    /// <summary>Mensagem com erro de validação e o texto digitado (mantido no campo).</summary>
    public string? ChaveComErro { get; private set; }
    public string? Erro { get; private set; }
    public string? TextoDigitado { get; private set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        Mensagens = await _service.ListarAsync(ct);
    }

    public async Task<IActionResult> OnPostSalvarAsync(string chave, string? texto, CancellationToken ct)
    {
        var resultado = await _service.SalvarAsync(chave, texto, ct);
        if (!resultado.Sucesso)
        {
            ChaveComErro = chave;
            Erro = resultado.Erro;
            TextoDigitado = texto;
            Mensagens = await _service.ListarAsync(ct);
            return Page();
        }

        Sucesso = "Mensagem salva. Ela já vale para as próximas conversas (pode levar cerca de 1 minuto).";
        return RedirectToPage(null, null, null, chave);
    }

    public async Task<IActionResult> OnPostRestaurarAsync(string chave, CancellationToken ct)
    {
        var resultado = await _service.RestaurarPadraoAsync(chave, ct);
        if (!resultado.Sucesso)
        {
            ChaveComErro = chave;
            Erro = resultado.Erro;
            Mensagens = await _service.ListarAsync(ct);
            return Page();
        }

        Sucesso = "Texto original restaurado.";
        return RedirectToPage(null, null, null, chave);
    }
}
