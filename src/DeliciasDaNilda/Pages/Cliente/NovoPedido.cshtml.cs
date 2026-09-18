using System.ComponentModel.DataAnnotations;
using DeliciasDaNilda.Data;
using DeliciasDaNilda.Domain.Entities;
using DeliciasDaNilda.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace DeliciasDaNilda.Pages.Cliente;

/// <summary>
/// Criação de pedido: escolha de sabor + peso (RN03), com data de entrega
/// restrita pela antecedência mínima (RN05) e capacidade diária (RN06/RN07).
/// </summary>
public class NovoPedidoModel : PageModel
{
    private const int DiasDeJanelaParaSelecaoDeData = 45;

    private readonly AppDbContext _db;
    private readonly IPedidoService _pedidoService;
    private readonly IAgendamentoService _agendamentoService;

    public NovoPedidoModel(AppDbContext db, IPedidoService pedidoService, IAgendamentoService agendamentoService)
    {
        _db = db;
        _pedidoService = pedidoService;
        _agendamentoService = agendamentoService;
    }

    [BindProperty]
    public EntradaModel Entrada { get; set; } = new();

    public List<Sabor> SaboresDisponiveis { get; set; } = new();

    public List<OpcaoData> OpcoesDeData { get; set; } = new();

    public string? ClienteNome { get; set; }

    public class EntradaModel
    {
        [Required(ErrorMessage = "Escolha um sabor.")]
        [Display(Name = "Sabor")]
        public int SaborId { get; set; }

        [Required(ErrorMessage = "Informe o peso desejado.")]
        [Range(0.01, 100, ErrorMessage = "Peso deve ser maior que zero.")]
        [Display(Name = "Peso (kg)")]
        public decimal PesoKg { get; set; }

        [Required(ErrorMessage = "Escolha a data de entrega.")]
        [Display(Name = "Data de entrega/retirada")]
        public DateOnly DataEntrega { get; set; }

        [Display(Name = "Observações")]
        [StringLength(500)]
        public string? Observacoes { get; set; }
    }

    public record OpcaoData(DateOnly Data, bool Bloqueada);

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var clienteId = HttpContext.Session.GetInt32("ClienteId");
        if (clienteId is null)
        {
            return RedirectToPage("/Cliente/Identificacao");
        }

        ClienteNome = HttpContext.Session.GetString("ClienteNome");
        await CarregarDadosDeApoioAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        var clienteId = HttpContext.Session.GetInt32("ClienteId");
        if (clienteId is null)
        {
            return RedirectToPage("/Cliente/Identificacao");
        }

        ClienteNome = HttpContext.Session.GetString("ClienteNome");

        if (!ModelState.IsValid)
        {
            await CarregarDadosDeApoioAsync(ct);
            return Page();
        }

        // RN03/RN05/RN06/RN07/RN08 — toda a validação de negócio acontece no service.
        var resultado = await _pedidoService.CriarPedidoAsync(
            clienteId.Value,
            Entrada.SaborId,
            Entrada.PesoKg,
            Entrada.DataEntrega,
            Entrada.Observacoes,
            ct);

        if (!resultado.Sucesso)
        {
            ModelState.AddModelError(string.Empty, resultado.Erro!);
            await CarregarDadosDeApoioAsync(ct);
            return Page();
        }

        return RedirectToPage("/Cliente/Confirmacao", new { id = resultado.Valor!.Id });
    }

    private async Task CarregarDadosDeApoioAsync(CancellationToken ct)
    {
        SaboresDisponiveis = await _db.Sabores
            .Where(s => s.Disponivel)
            .OrderBy(s => s.Nome)
            .ToListAsync(ct);

        // RN05: data mínima de entrega (3 dias úteis a partir de agora).
        var dataMinima = _agendamentoService.CalcularDataMinimaPermitida(DateTime.UtcNow);
        var dataFinal = dataMinima.AddDays(DiasDeJanelaParaSelecaoDeData);

        // RN06/RN07: mapa de bloqueio por data (>= 3 pedidos ativos no dia).
        var bloqueios = await _agendamentoService.ObterBloqueiosNoIntervaloAsync(dataMinima, dataFinal, ct);

        OpcoesDeData = bloqueios
            .OrderBy(kv => kv.Key)
            .Select(kv => new OpcaoData(kv.Key, kv.Value))
            .ToList();
    }
}
