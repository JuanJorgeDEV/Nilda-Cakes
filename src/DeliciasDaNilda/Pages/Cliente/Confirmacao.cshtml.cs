using DeliciasDaNilda.Data;
using DeliciasDaNilda.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace DeliciasDaNilda.Pages.Cliente;

/// <summary>
/// Tela de confirmação exibida logo após a criação do pedido — mostra o
/// status inicial "Pendente de Confirmação" (RN08).
/// </summary>
public class ConfirmacaoModel : PageModel
{
    private readonly AppDbContext _db;

    public ConfirmacaoModel(AppDbContext db)
    {
        _db = db;
    }

    public Pedido? Pedido { get; set; }

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken ct)
    {
        var clienteId = HttpContext.Session.GetInt32("ClienteId");
        if (clienteId is null)
        {
            return RedirectToPage("/Cliente/Identificacao");
        }

        Pedido = await _db.Pedidos
            .Include(p => p.Sabor)
            .FirstOrDefaultAsync(p => p.Id == id && p.ClienteId == clienteId.Value, ct);

        if (Pedido is null)
        {
            return NotFound();
        }

        return Page();
    }
}
