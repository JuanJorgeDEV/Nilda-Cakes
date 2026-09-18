using System.ComponentModel.DataAnnotations;
using DeliciasDaNilda.Data;
using DeliciasDaNilda.Domain.Entities;
using DeliciasDaNilda.Services.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace DeliciasDaNilda.Pages.Cliente;

/// <summary>
/// RN01 — identificação simplificada do cliente (nome completo + WhatsApp,
/// sem senha). O WhatsApp normalizado (só dígitos) é a chave única do
/// cliente: se já existir, reaproveita o cadastro; senão, cria um novo.
/// </summary>
public class IdentificacaoModel : PageModel
{
    private readonly AppDbContext _db;

    public IdentificacaoModel(AppDbContext db)
    {
        _db = db;
    }

    [BindProperty]
    public EntradaModel Entrada { get; set; } = new();

    public class EntradaModel
    {
        [Required(ErrorMessage = "Informe seu nome completo.")]
        [Display(Name = "Nome completo")]
        public string NomeCompleto { get; set; } = string.Empty;

        [Required(ErrorMessage = "Informe seu WhatsApp.")]
        [Display(Name = "WhatsApp")]
        public string WhatsApp { get; set; } = string.Empty;
    }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        // RN01: WhatsApp é a chave única — normaliza para só dígitos antes de comparar/gravar.
        var whatsAppNormalizado = WhatsAppHelper.Normalizar(Entrada.WhatsApp);

        if (string.IsNullOrWhiteSpace(whatsAppNormalizado))
        {
            ModelState.AddModelError(string.Empty, "WhatsApp inválido.");
            return Page();
        }

        var cliente = await _db.Clientes.FirstOrDefaultAsync(c => c.WhatsApp == whatsAppNormalizado, ct);

        if (cliente is null)
        {
            cliente = new Domain.Entities.Cliente
            {
                NomeCompleto = Entrada.NomeCompleto.Trim(),
                WhatsApp = whatsAppNormalizado,
                CriadoEm = DateTime.UtcNow
            };
            _db.Clientes.Add(cliente);
            await _db.SaveChangesAsync(ct);
        }

        HttpContext.Session.SetInt32("ClienteId", cliente.Id);
        HttpContext.Session.SetString("ClienteNome", cliente.NomeCompleto);

        return RedirectToPage("/Cliente/NovoPedido");
    }
}
