using DeliciasDaNilda.Data;
using DeliciasDaNilda.Domain.Entities;
using DeliciasDaNilda.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace DeliciasDaNilda.Services;

public interface IClienteService
{
    /// <summary>
    /// RN01 — busca o cliente pelo WhatsApp normalizado (chave única); se
    /// não existir, cria um novo com o nome informado. Se já existir com um
    /// nome diferente do informado, o cadastro existente é reaproveitado
    /// sem sobrescrever o nome (decisão de produto: evita que uma variação
    /// de digitação/apelido no bot sobrescreva o nome já cadastrado).
    /// </summary>
    Task<Cliente> ObterOuCriarAsync(string nomeCompleto, string whatsApp, CancellationToken ct = default);
}

/// <summary>
/// Identificação simplificada do cliente (RN01), reutilizável tanto pelo
/// boundary Razor Pages quanto pelo endpoint interno do bot de WhatsApp.
/// </summary>
public class ClienteService : IClienteService
{
    private readonly AppDbContext _db;

    public ClienteService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Cliente> ObterOuCriarAsync(string nomeCompleto, string whatsApp, CancellationToken ct = default)
    {
        // RN01: WhatsApp é a chave única — normaliza para só dígitos antes de comparar/gravar.
        var whatsAppNormalizado = WhatsAppHelper.Normalizar(whatsApp);

        if (string.IsNullOrWhiteSpace(whatsAppNormalizado))
        {
            throw new ArgumentException("WhatsApp inválido.", nameof(whatsApp));
        }

        var cliente = await _db.Clientes.FirstOrDefaultAsync(c => c.WhatsApp == whatsAppNormalizado, ct);

        if (cliente is not null)
        {
            // Decisão de produto: não sobrescrevemos o nome já cadastrado
            // mesmo que o nome informado agora seja diferente — evita que
            // uma digitação diferente no bot ("Zé" vs "José") substitua o
            // cadastro original sem intenção. Se a Nilda quiser permitir
            // atualização de nome, isso precisa ser uma ação explícita
            // (não implícita na identificação).
            return cliente;
        }

        cliente = new Cliente
        {
            NomeCompleto = nomeCompleto.Trim(),
            WhatsApp = whatsAppNormalizado,
            CriadoEm = DateTime.UtcNow
        };
        _db.Clientes.Add(cliente);
        await _db.SaveChangesAsync(ct);

        return cliente;
    }
}
