using DeliciasDaNilda.Data;
using DeliciasDaNilda.Domain.Entities;
using DeliciasDaNilda.Domain.Enums;
using DeliciasDaNilda.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeliciasDaNilda.Pages.Admin.Pedidos;

/// <summary>
/// Lista de pedidos com filtro por status e ações de confirmar/cancelar
/// (RN08).
/// </summary>
public class IndexModel : AdminPageModelBase
{
    private readonly AppDbContext _db;
    private readonly IPedidoService _pedidoService;

    public IndexModel(AppDbContext db, IPedidoService pedidoService)
    {
        _db = db;
        _pedidoService = pedidoService;
    }

    [BindProperty(SupportsGet = true)]
    public StatusPedido? FiltroStatus { get; set; }

    public List<Pedido> Pedidos { get; set; } = new();

    public string? MensagemErro { get; set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        await CarregarPedidosAsync(ct);
    }

    public async Task<IActionResult> OnPostConfirmarAsync(int id, CancellationToken ct)
    {
        // RN08: confirmação exclusiva do ADM logado.
        var resultado = await _pedidoService.ConfirmarPedidoAsync(id, AdministradorId, ct);
        if (!resultado.Sucesso)
        {
            MensagemErro = resultado.Erro;
        }

        await CarregarPedidosAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostCancelarAsync(int id, CancellationToken ct)
    {
        var resultado = await _pedidoService.CancelarPedidoAsync(id, ct);
        if (!resultado.Sucesso)
        {
            MensagemErro = resultado.Erro;
        }

        await CarregarPedidosAsync(ct);
        return Page();
    }

    private async Task CarregarPedidosAsync(CancellationToken ct)
    {
        var query = _db.Pedidos
            .Include(p => p.Cliente)
            .Include(p => p.Sabor)
            .AsQueryable();

        if (FiltroStatus is not null)
        {
            query = query.Where(p => p.StatusPedido == FiltroStatus);
        }

        Pedidos = await query
            .OrderBy(p => p.DataEntrega)
            .ThenBy(p => p.Id)
            .ToListAsync(ct);
    }
}
