using DeliciasDaNilda.Data;
using DeliciasDaNilda.Domain.Entities;
using DeliciasDaNilda.Domain.Enums;
using DeliciasDaNilda.Services;
using DeliciasDaNilda.Services.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeliciasDaNilda.Pages.Admin.Pedidos;

/// <summary>
/// Lista de pedidos em cards, com filtros rápidos e as ações do pedido:
/// confirmar/cancelar (RN08), entregar, horário, pagamento (RN09/RN10) e
/// aviso de falta de ingrediente (RN15). Os handlers também atendem a Agenda
/// e voltam para a URL de origem (PRG).
/// </summary>
public class IndexModel : AdminPageModelBase
{
    private readonly AppDbContext _db;
    private readonly IPedidoService _pedidoService;
    private readonly IPagamentoService _pagamentoService;
    private readonly IProjecaoEstoqueService _projecaoEstoque;

    public IndexModel(
        AppDbContext db,
        IPedidoService pedidoService,
        IPagamentoService pagamentoService,
        IProjecaoEstoqueService projecaoEstoque)
    {
        _db = db;
        _pedidoService = pedidoService;
        _pagamentoService = pagamentoService;
        _projecaoEstoque = projecaoEstoque;
    }

    /// <summary>ativos (padrão), pendentes, semana, entregues, cancelados, todos.</summary>
    [BindProperty(SupportsGet = true, Name = "f")]
    public string Filtro { get; set; } = "ativos";

    /// <summary>Pedido que deve abrir já expandido (após uma ação).</summary>
    [BindProperty(SupportsGet = true)]
    public int? Aberto { get; set; }

    public List<Pedido> Pedidos { get; set; } = new();

    public IReadOnlyDictionary<int, IReadOnlyList<FaltaIngrediente>> Faltas { get; set; }
        = new Dictionary<int, IReadOnlyList<FaltaIngrediente>>();

    public Dictionary<string, int> Contagens { get; set; } = new();

    [TempData]
    public string? MensagemSucesso { get; set; }

    [TempData]
    public string? MensagemErro { get; set; }

    public DateOnly Hoje { get; } = DateOnly.FromDateTime(DateTime.Today);

    public async Task OnGetAsync(CancellationToken ct)
    {
        var query = _db.Pedidos
            .Include(p => p.Cliente)
            .Include(p => p.Sabor)
            .Include(p => p.Pagamentos)
            .AsQueryable();

        var fimDaSemana = Hoje.AddDays(7);
        query = Filtro switch
        {
            "pendentes" => query.Where(p => p.StatusPedido == StatusPedido.PendenteConfirmacao),
            "semana" => query.Where(p => p.DataEntrega >= Hoje && p.DataEntrega <= fimDaSemana
                                         && p.StatusPedido != StatusPedido.Cancelado),
            "entregues" => query.Where(p => p.StatusPedido == StatusPedido.Concluido),
            "cancelados" => query.Where(p => p.StatusPedido == StatusPedido.Cancelado),
            "todos" => query,
            _ => query.Where(p => p.StatusPedido == StatusPedido.PendenteConfirmacao
                                  || p.StatusPedido == StatusPedido.Confirmado)
        };

        // Entregues/cancelados/todos: mais recentes primeiro. Ativos: por data
        // de entrega, então atrasados e os de hoje ficam no topo.
        Pedidos = Filtro is "entregues" or "cancelados" or "todos"
            ? await query.OrderByDescending(p => p.DataEntrega).ThenByDescending(p => p.Id).ToListAsync(ct)
            : await query.OrderBy(p => p.DataEntrega).ThenBy(p => p.HorarioRetirada).ThenBy(p => p.Id).ToListAsync(ct);

        Faltas = await _projecaoEstoque.ProjetarPorPedidoAsync(ct);

        Contagens["pendentes"] = await _db.Pedidos.CountAsync(p => p.StatusPedido == StatusPedido.PendenteConfirmacao, ct);
    }

    public async Task<IActionResult> OnPostConfirmarAsync(int id, string? voltar, CancellationToken ct)
    {
        // RN08: confirmação exclusiva do ADM logado; dispara o abate de estoque (RN12).
        var resultado = await _pedidoService.ConfirmarPedidoAsync(id, AdministradorId, ct);
        return Voltar(resultado, "Pedido confirmado.", voltar, id);
    }

    public async Task<IActionResult> OnPostCancelarAsync(int id, string? voltar, CancellationToken ct)
    {
        var resultado = await _pedidoService.CancelarPedidoAsync(id, ct);
        return Voltar(resultado, "Pedido cancelado.", voltar, id);
    }

    public async Task<IActionResult> OnPostConcluirAsync(int id, string? voltar, CancellationToken ct)
    {
        var resultado = await _pedidoService.ConcluirPedidoAsync(id, ct);
        return Voltar(resultado, "Pedido marcado como entregue.", voltar, id);
    }

    public async Task<IActionResult> OnPostHorarioAsync(int id, TimeOnly? horario, string? voltar, CancellationToken ct)
    {
        var resultado = await _pedidoService.DefinirHorarioAsync(id, horario, ct);
        return Voltar(resultado, horario is null ? "Horário removido." : $"Horário definido: {horario:HH\\:mm}.", voltar, id);
    }

    public async Task<IActionResult> OnPostPagamentoAsync(
        int id, string? modo, decimal? valorSinal, string? forma, string? voltar, CancellationToken ct)
    {
        // RN09/RN10: tipo e valor deduzidos do estado do pedido pelo serviço.
        var resultado = modo switch
        {
            "tudo" => await _pagamentoService.RegistrarRecebimentoAsync(id, ModoRecebimento.Tudo, null, AdministradorId, forma, ct),
            "sinal50" => await _pagamentoService.RegistrarRecebimentoAsync(id, ModoRecebimento.Sinal, null, AdministradorId, forma, ct),
            "sinal" when valorSinal is > 0 => await _pagamentoService.RegistrarRecebimentoAsync(id, ModoRecebimento.Sinal, valorSinal, AdministradorId, forma, ct),
            "sinal" => ResultadoOperacao.Falha("Informe o valor do sinal."),
            _ => ResultadoOperacao.Falha("Escolha como o pagamento foi recebido.")
        };
        return Voltar(resultado, "Pagamento registrado.", voltar, id);
    }

    private IActionResult Voltar(ResultadoOperacao resultado, string sucesso, string? voltar, int pedidoId)
    {
        if (resultado.Sucesso)
        {
            MensagemSucesso = sucesso;
        }
        else
        {
            MensagemErro = resultado.Erro;
        }

        // Só volta para URLs locais; mantém o card do pedido aberto.
        var destino = !string.IsNullOrWhiteSpace(voltar) && Url.IsLocalUrl(voltar) ? voltar : Url.Page("/Admin/Pedidos/Index")!;
        destino = RemoverParametro(destino, "aberto");
        var separador = destino.Contains('?') ? '&' : '?';
        return LocalRedirect($"{destino}{separador}aberto={pedidoId}#card-pedido-{pedidoId}");
    }

    private static string RemoverParametro(string url, string nome)
    {
        var partes = url.Split('#')[0].Split('?', 2);
        if (partes.Length < 2)
        {
            return partes[0];
        }

        var mantidos = partes[1].Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Where(p => !p.StartsWith(nome + "=", StringComparison.OrdinalIgnoreCase));
        var query = string.Join('&', mantidos);
        return query.Length > 0 ? $"{partes[0]}?{query}" : partes[0];
    }
}
