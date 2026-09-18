using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DeliciasDaNilda.Pages.Admin;

/// <summary>
/// Base para páginas do painel ADM. A proteção real de acesso (RN02) é feita
/// pelo middleware de autenticação/autorização (cookie auth + convenção
/// <c>AuthorizeFolder("/Admin")</c> configurada em Program.cs) — esta classe
/// apenas expõe os dados do Administrador autenticado (claims do cookie),
/// sem repetir nenhuma checagem manual de sessão.
/// </summary>
public abstract class AdminPageModelBase : PageModel
{
    public int AdministradorId { get; private set; }

    public string? AdministradorNome { get; private set; }

    public override void OnPageHandlerExecuting(PageHandlerExecutingContext context)
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (idClaim is not null && int.TryParse(idClaim, out var id))
        {
            AdministradorId = id;
        }

        AdministradorNome = User.FindFirstValue(ClaimTypes.Name);

        base.OnPageHandlerExecuting(context);
    }
}
