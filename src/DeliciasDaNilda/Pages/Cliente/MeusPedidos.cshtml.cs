using DeliciasDaNilda.Data;
using DeliciasDaNilda.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace DeliciasDaNilda.Pages.Cliente;

/// <summary>Pedidos do cliente identificado na sessão (RN01), do mais recente ao mais antigo.</summary>
public class MeusPedidosModel : PageModel
{
    private readonly AppDbContext _db;

    public MeusPedidosModel(AppDbContext db)
    {
        _db = db;
    }

    public string? ClienteNome { get; set; }

    public List<Pedido> Pedidos { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var clienteId = HttpContext.Session.GetInt32("ClienteId");
        if (clienteId is null)
        {
            return RedirectToPage("/Cliente/Identificacao");
        }

        ClienteNome = HttpContext.Session.GetString("ClienteNome");
        Pedidos = await _db.Pedidos
            .AsNoTracking()
            .Include(p => p.Sabor)
            .Where(p => p.ClienteId == clienteId.Value)
            .OrderByDescending(p => p.DataEntrega)
            .ThenByDescending(p => p.Id)
            .Take(30)
            .ToListAsync(ct);

        return Page();
    }
}
