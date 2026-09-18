using DeliciasDaNilda.Domain.Entities;

namespace DeliciasDaNilda.Services;

/// <summary>
/// Autenticação de Administrador (RN02): login usuário/senha com hash real
/// (via <see cref="Microsoft.AspNetCore.Identity.PasswordHasher{TUser}"/>).
/// </summary>
public interface IAdministradorAuthService
{
    /// <summary>
    /// Cria um Administrador com a senha já hasheada (RN02). Não expõe
    /// nenhum endpoint público — uso restrito a seed/scripts internos.
    /// </summary>
    Task<Administrador> CriarAdministradorAsync(
        string nomeCompleto, string email, string senha, CancellationToken ct = default);

    /// <summary>
    /// Verifica e-mail + senha em texto puro contra o hash armazenado (RN02).
    /// Retorna o Administrador autenticado apenas se ativo e a senha confere.
    /// </summary>
    Task<(bool Sucesso, Administrador? Administrador)> VerificarLoginAsync(
        string email, string senha, CancellationToken ct = default);
}
