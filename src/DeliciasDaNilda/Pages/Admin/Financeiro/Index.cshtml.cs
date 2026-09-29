using DeliciasDaNilda.Services;
using Microsoft.AspNetCore.Mvc;

namespace DeliciasDaNilda.Pages.Admin.Financeiro;

/// <summary>Dashboard financeiro do ADM (RN10): vendas, recebido, custo, lucro, ROI e rankings.</summary>
public class IndexModel : AdminPageModelBase
{
    private readonly IFinanceiroService _financeiro;

    public IndexModel(IFinanceiroService financeiro)
    {
        _financeiro = financeiro;
    }

    /// <summary>7d, 30d (padrão), mes, mespassado, ano ou custom (usa De/Ate).</summary>
    [BindProperty(SupportsGet = true)]
    public string Periodo { get; set; } = "30d";

    [BindProperty(SupportsGet = true)]
    public DateOnly? De { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateOnly? Ate { get; set; }

    public ResumoFinanceiro Resumo { get; private set; } = null!;

    public async Task OnGetAsync(CancellationToken ct)
    {
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var inicioMes = new DateOnly(hoje.Year, hoje.Month, 1);

        var (inicio, fim) = Periodo switch
        {
            "7d" => (hoje.AddDays(-6), hoje),
            "mes" => (inicioMes, inicioMes.AddMonths(1).AddDays(-1)),
            "mespassado" => (inicioMes.AddMonths(-1), inicioMes.AddDays(-1)),
            "ano" => (new DateOnly(hoje.Year, 1, 1), new DateOnly(hoje.Year, 12, 31)),
            "custom" when De is not null && Ate is not null => (De.Value, Ate.Value),
            _ => (hoje.AddDays(-29), hoje)
        };

        if (Periodo == "custom" && (De is null || Ate is null))
        {
            Periodo = "30d";
        }

        De = inicio;
        Ate = fim;
        Resumo = await _financeiro.ResumirAsync(inicio, fim, ct);
    }
}
