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
        [Range(1, int.MaxValue, ErrorMessage = "Selecione o pedido.")]
        public int PedidoId { get; set; }

        /// <summary>Só é escolhido quando o pedido ainda não tem pagamento (Sinal ou Integral).</summary>
        public TipoPagamento? Tipo { get; set; }

        /// <summary>Só é digitado quando o tipo é Sinal; nos demais o valor é o restante do pedido.</summary>
        public decimal? Valor { get; set; }

        [Display(Name = "Forma de pagamento (opcional)")]
        public string? FormaPagamento { get; set; }
    }

    public decimal JaPago => PedidoSelecionado?.Pagamentos.Sum(p => p.Valor) ?? 0m;

    public decimal Restante => (PedidoSelecionado?.ValorTotal ?? 0m) - JaPago;

    /// <summary>Com pagamento anterior (sinal), o único passo possível é acertar o saldo.</summary>
    public bool ApenasSaldo => JaPago > 0;

    public async Task OnGetAsync(CancellationToken ct)
    {
        Entrada.PedidoId = PedidoId ?? 0;
        await CarregarDadosDeApoioAsync(ct);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        PedidoId = Entrada.PedidoId;
        await CarregarDadosDeApoioAsync(ct);

        if (!ModelState.IsValid || PedidoSelecionado is null)
        {
            return Page();
        }

        // RN09/RN10: tipo e valor são deduzidos do estado do pedido; só o
        // sinal tem valor digitado. O serviço ainda valida o teto do total.
        TipoPagamento tipo;
        decimal valor;
        if (ApenasSaldo)
        {
            tipo = TipoPagamento.SaldoRestante;
            valor = Restante;
        }
        else if (Entrada.Tipo == TipoPagamento.Sinal)
        {
            tipo = TipoPagamento.Sinal;
            valor = Entrada.Valor ?? 0m;
            if (valor <= 0 || valor >= Restante)
            {
                MensagemErro = $"O sinal deve ser maior que zero e menor que o total ({Restante:C}). Para pagar tudo, use pagamento integral.";
                return Page();
            }
        }
        else
        {
            tipo = TipoPagamento.Integral;
            valor = Restante;
        }

        var resultado = await _pagamentoService.RegistrarPagamentoAsync(
            Entrada.PedidoId, tipo, valor, AdministradorId, Entrada.FormaPagamento, ct);

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
