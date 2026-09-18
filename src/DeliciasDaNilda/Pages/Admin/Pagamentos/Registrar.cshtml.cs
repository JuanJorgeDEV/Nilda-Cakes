using System.ComponentModel.DataAnnotations;
using DeliciasDaNilda.Data;
using DeliciasDaNilda.Domain.Entities;
using DeliciasDaNilda.Domain.Enums;
using DeliciasDaNilda.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeliciasDaNilda.Pages.Admin.Pagamentos;

/// <summary>Registro de baixa financeira por pedido (RN09/RN10).</summary>
public class RegistrarModel : AdminPageModelBase
{
    private readonly AppDbContext _db;
    private readonly IPagamentoService _pagamentoService;

    public RegistrarModel(AppDbContext db, IPagamentoService pagamentoService)
    {
        _db = db;
        _pagamentoService = pagamentoService;
    }

    [BindProperty(SupportsGet = true)]
    public int? PedidoId { get; set; }

    [BindProperty]
    public EntradaModel Entrada { get; set; } = new();

    public List<Pedido> PedidosEmAberto { get; set; } = new();

    public Pedido? PedidoSelecionado { get; set; }

    public string? MensagemSucesso { get; set; }

    public string? MensagemErro { get; set; }

    public class EntradaModel
    {
        [Required(ErrorMessage = "Selecione o pedido.")]
        public int PedidoId { get; set; }

        [Required(ErrorMessage = "Selecione o tipo de pagamento.")]
        public TipoPagamento Tipo { get; set; }

        [Required(ErrorMessage = "Informe o valor.")]
        [Range(0.01, 1000000, ErrorMessage = "Valor deve ser maior que zero.")]
        public decimal Valor { get; set; }

        [Display(Name = "Forma de pagamento (opcional)")]
        public string? FormaPagamento { get; set; }
    }

    public async Task OnGetAsync(CancellationToken ct)
    {
        Entrada.PedidoId = PedidoId ?? 0;
        await CarregarDadosDeApoioAsync(ct);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            PedidoId = Entrada.PedidoId;
            await CarregarDadosDeApoioAsync(ct);
            return Page();
        }

        // RN09/RN10: baixa financeira registrada pelo ADM logado.
        var resultado = await _pagamentoService.RegistrarPagamentoAsync(
            Entrada.PedidoId, Entrada.Tipo, Entrada.Valor, AdministradorId, Entrada.FormaPagamento, ct);

        PedidoId = Entrada.PedidoId;

        if (!resultado.Sucesso)
        {
            MensagemErro = resultado.Erro;
        }
        else
        {
            MensagemSucesso = "Pagamento registrado.";
        }

        await CarregarDadosDeApoioAsync(ct);
        return Page();
    }

    private async Task CarregarDadosDeApoioAsync(CancellationToken ct)
    {
        PedidosEmAberto = await _db.Pedidos
            .Include(p => p.Cliente)
            .Include(p => p.Sabor)
            .Where(p => p.StatusPagamento != StatusPagamento.PagoIntegral
                        && p.StatusPedido != StatusPedido.Cancelado)
            .OrderBy(p => p.DataEntrega)
            .ToListAsync(ct);

        if (PedidoId is not null)
        {
            PedidoSelecionado = await _db.Pedidos
                .Include(p => p.Pagamentos)
                .FirstOrDefaultAsync(p => p.Id == PedidoId, ct);
        }
    }
}
