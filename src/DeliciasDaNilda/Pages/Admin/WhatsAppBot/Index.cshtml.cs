using DeliciasDaNilda.Services;
using DeliciasDaNilda.Services.Common;
using Microsoft.AspNetCore.Mvc;

namespace DeliciasDaNilda.Pages.Admin.WhatsAppBot;

/// <summary>
/// Liga/desliga o atendimento automático de pedidos via WhatsApp
/// (flag WhatsAppBotAtivo, ver IConfiguracaoService) e define o número da
/// loja para onde o site manda o cliente depois do pedido. Não corresponde
/// a uma RN numerada — é uma configuração operacional exclusiva do ADM.
/// </summary>
public class IndexModel : AdminPageModelBase
{
    private readonly IConfiguracaoService _configuracaoService;
    private readonly IWhatsAppBotProcess _processoBot;

    public IndexModel(IConfiguracaoService configuracaoService, IWhatsAppBotProcess processoBot)
    {
        _configuracaoService = configuracaoService;
        _processoBot = processoBot;
    }

    public bool PastaDoBotExiste => _processoBot.PastaDoBotExiste;

    /// <summary>Estado da conexão e QR code, consultado a cada poucos segundos pela página.</summary>
    public IActionResult OnGetEstado() => new JsonResult(_processoBot.Obter());

    public IActionResult OnPostIniciar()
    {
        _processoBot.Iniciar();
        return RedirectToPage();
    }

    public IActionResult OnPostParar()
    {
        _processoBot.Parar();
        return RedirectToPage();
    }

    public IActionResult OnPostTrocarNumero()
    {
        _processoBot.TrocarNumero();
        MensagemSucesso = "Sessão apagada. Escaneie o novo QR code para conectar outro número.";
        return RedirectToPage();
    }

    public bool BotAtivo { get; set; }

    public string? WhatsAppLoja { get; set; }

    [TempData]
    public string? MensagemSucesso { get; set; }

    [TempData]
    public string? MensagemErro { get; set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        BotAtivo = await _configuracaoService.BotAtivoAsync(ct);
        WhatsAppLoja = await _configuracaoService.WhatsAppLojaAsync(ct);
    }

    public async Task<IActionResult> OnPostAsync(bool ativar, CancellationToken ct)
    {
        await _configuracaoService.ExibirOuAtualizarBotAtivoAsync(ativar, ct);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostNumeroAsync(string? numero, CancellationToken ct)
    {
        var normalizado = WhatsAppHelper.Normalizar(numero ?? string.Empty);
        if (normalizado.Length is > 0 and < 12)
        {
            MensagemErro = "Digite o número com DDD, por exemplo (16) 99999-9999.";
            return RedirectToPage();
        }

        await _configuracaoService.DefinirWhatsAppLojaAsync(numero, ct);
        MensagemSucesso = normalizado.Length == 0 ? "Número removido." : "Número da loja salvo.";
        return RedirectToPage();
    }
}
