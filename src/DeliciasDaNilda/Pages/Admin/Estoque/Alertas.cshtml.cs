using DeliciasDaNilda.Services;
using Microsoft.AspNetCore.Mvc;

namespace DeliciasDaNilda.Pages.Admin.Estoque;

/// <summary>Painel de alerta preventivo de reposição de estoque (RN14/RN15).</summary>
public class AlertasModel : AdminPageModelBase
{
    private readonly IProjecaoEstoqueService _projecaoEstoqueService;

    public AlertasModel(IProjecaoEstoqueService projecaoEstoqueService)
    {
        _projecaoEstoqueService = projecaoEstoqueService;
    }

    public ProjecaoEstoqueResultado? Resultado { get; set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        Resultado = await _projecaoEstoqueService.ProjetarAsync(ct: ct);
    }
}
