using DeliciasDaNilda.Data;
using DeliciasDaNilda.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DeliciasDaNilda.Services;

/// <summary>
/// Implementação de <see cref="IAdministradorAuthService"/> (RN02) usando
/// <see cref="PasswordHasher{TUser}"/> do ASP.NET (apenas o utilitário de
/// hashing — não usamos o framework Identity completo).
/// </summary>
public class AdministradorAuthService : IAdministradorAuthService
{
    private readonly AppDbContext _db;
    private readonly PasswordHasher<Administrador> _passwordHasher = new();

    public AdministradorAuthService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Administrador> CriarAdministradorAsync(
        string nomeCompleto, string email, string senha, CancellationToken ct = default)
    {
        var administrador = new Administrador
        {
            NomeCompleto = nomeCompleto,
            Email = email,
            Ativo = true,
            CriadoEm = DateTime.UtcNow
        };

        // RN02: senha nunca é gravada em texto plano.
        administrador.SenhaHash = _passwordHasher.HashPassword(administrador, senha);

        _db.Administradores.Add(administrador);
        await _db.SaveChangesAsync(ct);

        return administrador;
    }

    public async Task<(bool Sucesso, Administrador? Administrador)> VerificarLoginAsync(
        string email, string senha, CancellationToken ct = default)
    {
        var administrador = await _db.Administradores
            .FirstOrDefaultAsync(a => a.Email == email && a.Ativo, ct);

        if (administrador is null)
        {
            return (false, null);
        }

        // RN02: verificação real de senha contra o hash armazenado.
        var resultado = _passwordHasher.VerifyHashedPassword(administrador, administrador.SenhaHash, senha);
        if (resultado == PasswordVerificationResult.Failed)
        {
            return (false, null);
        }

        return (true, administrador);
    }
}
