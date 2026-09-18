using DeliciasDaNilda.Services;
using Microsoft.AspNetCore.Mvc;

namespace DeliciasDaNilda.Pages.Admin.Agenda;

/// <summary>Agenda operacional consolidada por dia (RN16).</summary>
public class IndexModel : AdminPageModelBase
{
    private readonly IAgendaService _agendaService;

    public IndexModel(IAgendaService agendaService)
    {
        _agendaService = agendaService;
    }

    [BindProperty(SupportsGet = true)]
    public DateOnly Data { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    public AgendaDoDiaResultado? Agenda { get; set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        Agenda = await _agendaService.ObterAgendaDoDiaAsync(Data, ct);
    }
}
