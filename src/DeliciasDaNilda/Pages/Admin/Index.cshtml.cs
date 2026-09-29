using DeliciasDaNilda.Data;
using DeliciasDaNilda.Domain.Enums;
using DeliciasDaNilda.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeliciasDaNilda.Pages.Admin;

/// <summary>
/// Painel inicial do ADM: atalhos para os módulos de gestão, cada um com um
/// número-resumo (pendências RN08, agenda RN16, alerta de estoque RN15,
/// valores a receber RN10).
/// </summary>
public class IndexModel : AdminPageModelBase
{
    private readonly AppDbContext _db;
    private readonly IProjecaoEstoqueService _projecaoEstoque;
    private readonly IConfiguracaoService _configuracao;

    public IndexModel(AppDbContext db, IProjecaoEstoqueService projecaoEstoque, IConfiguracaoService configuracao)
    {
        _db = db;
        _projecaoEstoque = projecaoEstoque;
        _configuracao = configuracao;
    }

    public int PedidosPendentes { get; private set; }

    public int PedidosHoje { get; private set; }

    public int SaboresDisponiveis { get; private set; }

    public int Ingredientes { get; private set; }

    public string? IngredienteEmFalta { get; private set; }

    public decimal ValorAReceber { get; private set; }

    public bool BotAtivo { get; private set; }

    /// <summary>Próximas entregas ativas a partir de hoje (RN16).</summary>
    public List<Domain.Entities.Pedido> ProximasEntregas { get; private set; } = new();

    public async Task OnGetAsync(CancellationToken ct)
    {
        var hoje = DateOnly.FromDateTime(DateTime.Today);

        PedidosPendentes = await _db.Pedidos.CountAsync(p => p.StatusPedido == StatusPedido.PendenteConfirmacao, ct);
        PedidosHoje = await _db.Pedidos.CountAsync(p => p.DataEntrega == hoje && p.StatusPedido != StatusPedido.Cancelado, ct);
        SaboresDisponiveis = await _db.Sabores.CountAsync(s => s.Disponivel, ct);
        Ingredientes = await _db.Ingredientes.CountAsync(ct);

        var emAberto = await _db.Pedidos
            .Where(p => p.StatusPedido != StatusPedido.Cancelado && p.StatusPagamento != StatusPagamento.PagoIntegral)
            .Select(p => new { p.ValorTotal, Pago = p.Pagamentos.Sum(x => (decimal?)x.Valor) ?? 0m })
            .ToListAsync(ct);
        ValorAReceber = emAberto.Sum(p => p.ValorTotal - p.Pago);

        var projecao = await _projecaoEstoque.ProjetarAsync(ct: ct);
        IngredienteEmFalta = projecao.EstoqueSuficiente ? null : projecao.Alerta?.IngredienteNome;

        BotAtivo = await _configuracao.BotAtivoAsync(ct);

        ProximasEntregas = await _db.Pedidos
            .Include(p => p.Cliente)
            .Include(p => p.Sabor)
            .Where(p => p.DataEntrega >= hoje
                        && (p.StatusPedido == StatusPedido.PendenteConfirmacao || p.StatusPedido == StatusPedido.Confirmado))
            .OrderBy(p => p.DataEntrega)
            .ThenBy(p => p.HorarioRetirada)
            .Take(4)
            .ToListAsync(ct);
    }

    public async Task<IActionResult> OnPostLogoutAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToPage("/Admin/Login");
    }
}
