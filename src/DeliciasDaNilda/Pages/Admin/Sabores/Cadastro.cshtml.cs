using DeliciasDaNilda.Domain.Entities;
using DeliciasDaNilda.Services;
using DeliciasDaNilda.Services.Common;
using Microsoft.AspNetCore.Mvc;

namespace DeliciasDaNilda.Pages.Admin.Sabores;

/// <summary>
/// Cardápio: sabores em cards com preço (RN03), custo estimado e margem,
/// ficha técnica (RN04) e as ações pausar/ativar e excluir (lógico).
/// </summary>
public class CadastroModel : AdminPageModelBase
{
    private readonly ISaborService _saborService;

    public CadastroModel(ISaborService saborService)
    {
        _saborService = saborService;
    }

    public List<Sabor> Sabores { get; set; } = new();

    [TempData]
    public string? MensagemSucesso { get; set; }

    [TempData]
    public string? MensagemErro { get; set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        Sabores = await _saborService.ListarAsync(ct: ct);
    }

    public async Task<IActionResult> OnPostAlternarAsync(int id, CancellationToken ct)
    {
        var resultado = await _saborService.AlternarDisponibilidadeAsync(id, ct);
        return Voltar(resultado, "Disponibilidade atualizada.");
    }

    public async Task<IActionResult> OnPostExcluirAsync(int id, CancellationToken ct)
    {
        var resultado = await _saborService.ExcluirAsync(id, ct);
        return Voltar(resultado, "Sabor excluído do cardápio. Os pedidos antigos continuam com ele.");
    }

    private IActionResult Voltar(ResultadoOperacao resultado, string sucesso)
    {
        if (resultado.Sucesso)
        {
            MensagemSucesso = sucesso;
        }
        else
        {
            MensagemErro = resultado.Erro;
        }

        return RedirectToPage();
    }
}
