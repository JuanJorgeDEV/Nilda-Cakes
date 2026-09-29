using DeliciasDaNilda.Data;
using DeliciasDaNilda.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeliciasDaNilda.Pages.Admin.Insumos;

/// <summary>
/// Últimos movimentos de um insumo (RN11 entradas, RN12 abates por pedido,
/// RN13 perdas/ajustes) — leitura do log append-only.
/// </summary>
public class HistoricoModel : AdminPageModelBase
{
    private readonly AppDbContext _db;

    public HistoricoModel(AppDbContext db)
    {
        _db = db;
    }

    public Ingrediente? Ingrediente { get; set; }

    public List<MovimentoEstoque> Movimentos { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken ct)
    {
        Ingrediente = await _db.Ingredientes.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id, ct);
        if (Ingrediente is null)
        {
            return RedirectToPage("/Admin/Insumos/Index");
        }

        Movimentos = await _db.MovimentosEstoque
            .AsNoTracking()
            .Include(m => m.Pedido!)
                .ThenInclude(p => p.Cliente)
            .Where(m => m.IngredienteId == id)
            .OrderByDescending(m => m.CriadoEm)
            .ThenByDescending(m => m.Id)
            .Take(100)
            .ToListAsync(ct);

        return Page();
    }
}
