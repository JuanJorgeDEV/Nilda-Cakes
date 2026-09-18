using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using DeliciasDaNilda.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DeliciasDaNilda.Pages.Admin;

/// <summary>
/// Login do ADM (RN02): valida e-mail + senha contra o hash armazenado
/// (<see cref="IAdministradorAuthService"/>) e autentica via cookie
/// (ASP.NET Cookie Authentication). Página pública dentro da pasta /Admin
/// (ver <c>AllowAnonymousToPage</c> em Program.cs), pois o restante do
/// painel exige autenticação via <c>[Authorize]</c>/convention.
/// </summary>
[AllowAnonymous]
public class LoginModel : PageModel
{
    private readonly IAdministradorAuthService _authService;

    public LoginModel(IAdministradorAuthService authService)
    {
        _authService = authService;
    }

    [BindProperty]
    public EntradaModel Entrada { get; set; } = new();

    public class EntradaModel
    {
        [Required(ErrorMessage = "Informe o e-mail.")]
        [Display(Name = "E-mail")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Informe a senha.")]
        [DataType(DataType.Password)]
        [Display(Name = "Senha")]
        public string Senha { get; set; } = string.Empty;
    }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        // RN02: e-mail + senha verificados contra o hash armazenado.
        var (sucesso, administrador) = await _authService.VerificarLoginAsync(Entrada.Email, Entrada.Senha, ct);
        if (!sucesso || administrador is null)
        {
            ModelState.AddModelError(string.Empty, "E-mail ou senha inválidos.");
            return Page();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, administrador.Id.ToString()),
            new(ClaimTypes.Name, administrador.NomeCompleto)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        return RedirectToPage("/Admin/Index");
    }
}
