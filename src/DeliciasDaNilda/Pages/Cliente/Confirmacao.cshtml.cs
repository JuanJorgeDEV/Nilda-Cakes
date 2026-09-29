using DeliciasDaNilda.Data;
using DeliciasDaNilda.Domain.Entities;
using DeliciasDaNilda.Services;
using DeliciasDaNilda.Services.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace DeliciasDaNilda.Pages.Cliente;

/// <summary>
/// Acompanhar meu pedido: situação (RN08), pagamento (RN09) e o envio do
/// pedido para o WhatsApp da loja, onde a Nilda confirma e combina o horário.
/// </summary>
public class ConfirmacaoModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IConfiguracaoService _configuracao;

    public ConfirmacaoModel(AppDbContext db, IConfiguracaoService configuracao)
    {
        _db = db;
        _configuracao = configuracao;
    }

    public Pedido? Pedido { get; set; }

    /// <summary>Abre o modal "envie pelo WhatsApp" logo depois de criar o pedido.</summary>
    [BindProperty(SupportsGet = true)]
    public bool Enviar { get; set; }

    public string? LinkWhatsApp { get; set; }

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken ct)
    {
        var clienteId = HttpContext.Session.GetInt32("ClienteId");
        if (clienteId is null)
        {
            return RedirectToPage("/Cliente/Identificacao");
        }

        Pedido = await _db.Pedidos
            .Include(p => p.Sabor)
            .Include(p => p.Cliente)
            .Include(p => p.Pagamentos)
            .FirstOrDefaultAsync(p => p.Id == id && p.ClienteId == clienteId.Value, ct);

        if (Pedido is null)
        {
            return NotFound();
        }

        var numeroLoja = await _configuracao.WhatsAppLojaAsync(ct);
        if (numeroLoja is not null)
        {
            LinkWhatsApp = MensagemPedidoWhatsApp.Link(numeroLoja, MensagemPedidoWhatsApp.Montar(Pedido));
        }

        return Page();
    }
}
