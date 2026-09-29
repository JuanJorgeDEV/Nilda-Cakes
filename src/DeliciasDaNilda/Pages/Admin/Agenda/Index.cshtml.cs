using DeliciasDaNilda.Data;
using DeliciasDaNilda.Domain.Entities;
using DeliciasDaNilda.Domain.Enums;
using DeliciasDaNilda.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeliciasDaNilda.Pages.Admin.Agenda;

/// <summary>
/// Agenda operacional (RN16): semana em grade (estilo calendário), com a
/// ocupação de cada dia (RN06/RN07), e o dia escolhido embaixo — pedidos em
/// cards e a ficha técnica total comparada com o estoque.
/// </summary>
public class IndexModel : AdminPageModelBase
{
    /// <summary>Faixa de horas da grade: 7h–20h, ampliada se algum pedido cair fora dela.</summary>
    public int HoraInicio { get; private set; } = 7;

    public int HoraFim { get; private set; } = 20;

    private readonly AppDbContext _db;
    private readonly IAgendaService _agendaService;
    private readonly IProjecaoEstoqueService _projecaoEstoque;

    public IndexModel(AppDbContext db, IAgendaService agendaService, IProjecaoEstoqueService projecaoEstoque)
    {
        _db = db;
        _agendaService = agendaService;
        _projecaoEstoque = projecaoEstoque;
    }

    /// <summary>Dia selecionado (padrão: hoje). A semana exibida é a que contém esse dia.</summary>
    [BindProperty(SupportsGet = true)]
    public DateOnly? Dia { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? Aberto { get; set; }

    public DateOnly Hoje { get; } = DateOnly.FromDateTime(DateTime.Today);

    /// <summary>Resultado das ações do card (postadas em Pedidos, que volta para cá).</summary>
    [TempData]
    public string? MensagemSucesso { get; set; }

    [TempData]
    public string? MensagemErro { get; set; }

    public DateOnly DiaSelecionado { get; private set; }

    /// <summary>Domingo da semana exibida.</summary>
    public DateOnly InicioSemana { get; private set; }

    public IReadOnlyList<DateOnly> DiasDaSemana { get; private set; } = Array.Empty<DateOnly>();

    /// <summary>Pedidos da semana (menos cancelados), por data.</summary>
    public ILookup<DateOnly, Pedido> PedidosDaSemana { get; private set; } = Array.Empty<Pedido>().ToLookup(p => p.DataEntrega);

    public AgendaDoDiaResultado? AgendaDoDia { get; private set; }

    public IReadOnlyDictionary<int, IReadOnlyList<FaltaIngrediente>> Faltas { get; private set; }
        = new Dictionary<int, IReadOnlyList<FaltaIngrediente>>();

    /// <summary>
    /// Pedidos do dia na ordem em que acontecem: os com horário de retirada
    /// por hora, e por último os "a combinar".
    /// </summary>
    public List<Pedido> PedidosDoDia(DateOnly dia) => PedidosDaSemana[dia]
        .OrderBy(p => p.HorarioRetirada is null ? 1 : 0)
        .ThenBy(p => p.HorarioRetirada)
        .ThenBy(p => p.Id)
        .ToList();

    /// <summary>
    /// Posição de cada pedido com horário na grade do dia: pedidos que
    /// acontecem com menos de 1 hora de diferença ficam lado a lado (coluna
    /// e total de colunas), em vez de um cobrir o outro.
    /// </summary>
    public List<(Pedido Pedido, int Coluna, int Colunas)> PosicionarNaGrade(DateOnly dia)
    {
        var itens = PedidosDoDia(dia).Where(p => p.HorarioRetirada is not null).ToList();
        var resultado = new List<(Pedido, int, int)>();
        var grupo = new List<Pedido>();

        void FecharGrupo()
        {
            for (var i = 0; i < grupo.Count; i++)
            {
                resultado.Add((grupo[i], i, grupo.Count));
            }

            grupo.Clear();
        }

        foreach (var p in itens)
        {
            if (grupo.Count > 0 && (p.HorarioRetirada!.Value - grupo[^1].HorarioRetirada!.Value).TotalMinutes >= 60)
            {
                FecharGrupo();
            }

            grupo.Add(p);
        }

        FecharGrupo();
        return resultado;
    }

    /// <summary>RN06/RN07: pedidos que ocupam a capacidade do dia.</summary>
    public int Ocupacao(DateOnly dia) => PedidosDaSemana[dia].Count(p => AgendamentoService.StatusQueOcupamCapacidade.Contains(p.StatusPedido));

    public async Task OnGetAsync(CancellationToken ct)
    {
        DiaSelecionado = Dia ?? Hoje;
        InicioSemana = DiaSelecionado.AddDays(-(int)DiaSelecionado.DayOfWeek);
        DiasDaSemana = Enumerable.Range(0, 7).Select(i => InicioSemana.AddDays(i)).ToList();
        var fimSemana = InicioSemana.AddDays(6);

        var pedidos = await _db.Pedidos
            .Include(p => p.Cliente)
            .Include(p => p.Sabor)
            .Include(p => p.Pagamentos)
            .Where(p => p.DataEntrega >= InicioSemana && p.DataEntrega <= fimSemana
                        && p.StatusPedido != StatusPedido.Cancelado)
            .OrderBy(p => p.HorarioRetirada)
            .ThenBy(p => p.Id)
            .ToListAsync(ct);
        PedidosDaSemana = pedidos.ToLookup(p => p.DataEntrega);

        var horas = pedidos.Where(p => p.HorarioRetirada is not null).Select(p => p.HorarioRetirada!.Value.Hour).ToList();
        if (horas.Count > 0)
        {
            HoraInicio = Math.Min(7, horas.Min());
            HoraFim = Math.Min(24, Math.Max(20, horas.Max() + 1));
        }

        // RN16: ficha técnica total do dia escolhido, com saldo em estoque.
        AgendaDoDia = await _agendaService.ObterAgendaDoDiaAsync(DiaSelecionado, ct);
        Faltas = await _projecaoEstoque.ProjetarPorPedidoAsync(ct);
    }
}
