using DeliciasDaNilda.Services;
using Microsoft.AspNetCore.Mvc;

namespace DeliciasDaNilda.Pages.Admin.WhatsAppBot;

/// <summary>
/// Liga/desliga o atendimento automático de pedidos via WhatsApp
/// (flag WhatsAppBotAtivo, ver IConfiguracaoService). Não corresponde a
/// uma RN numerada — é uma configuração operacional exclusiva do ADM.
/// </summary>
public class IndexModel : AdminPageModelBase
{
    private readonly IConfiguracaoService _configuracaoService;

    public IndexModel(IConfiguracaoService configuracaoService)
    {
        _configuracaoService = configuracaoService;
    }

    public bool BotAtivo { get; set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        BotAtivo = await _configuracaoService.BotAtivoAsync(ct);
    }

    public async Task<IActionResult> OnPostAsync(bool ativar, CancellationToken ct)
    {
        await _configuracaoService.ExibirOuAtualizarBotAtivoAsync(ativar, ct);
        return RedirectToPage();
    }
}
