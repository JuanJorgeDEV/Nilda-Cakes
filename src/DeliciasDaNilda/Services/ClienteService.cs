using DeliciasDaNilda.Data;
using DeliciasDaNilda.Domain.Entities;
using DeliciasDaNilda.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace DeliciasDaNilda.Services;

/// <summary>Dados do primeiro cadastro do cliente (RN01 + região de entrega).</summary>
public record DadosCadastroCliente(string NomeCompleto, string WhatsApp, string? Cep, string? Bairro, string? Cidade);

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

    /// <summary>
    /// RN01 — o número é a identificação: devolve o cadastro existente (aceitando
    /// o número com ou sem o nono dígito) ou null se for um número novo.
    /// </summary>
    Task<Cliente?> ObterPorWhatsAppAsync(string whatsApp, CancellationToken ct = default);

    /// <summary>
    /// Primeiro cadastro (nome + CEP). Se o número já existe, só completa CEP,
    /// bairro e cidade que estiverem vazios — o nome não é sobrescrito.
    /// </summary>
    Task<ResultadoOperacao<Cliente>> CadastrarAsync(DadosCadastroCliente dados, CancellationToken ct = default);
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

    public async Task<Cliente?> ObterPorWhatsAppAsync(string whatsApp, CancellationToken ct = default)
    {
        var variantes = WhatsAppHelper.Variantes(whatsApp);
        if (variantes[0].Length == 0)
        {
            return null;
        }

        return await _db.Clientes.FirstOrDefaultAsync(c => variantes.Contains(c.WhatsApp), ct);
    }

    public async Task<Cliente> ObterOuCriarAsync(string nomeCompleto, string whatsApp, CancellationToken ct = default)
    {
        // RN01: WhatsApp é a chave única — normaliza (só dígitos, com DDI) antes de comparar/gravar.
        var whatsAppNormalizado = WhatsAppHelper.Normalizar(whatsApp);

        if (string.IsNullOrWhiteSpace(whatsAppNormalizado))
        {
            throw new ArgumentException("WhatsApp inválido.", nameof(whatsApp));
        }

        var cliente = await ObterPorWhatsAppAsync(whatsAppNormalizado, ct);

        if (cliente is not null)
        {
            // Decisão de produto: não sobrescrevemos o nome já cadastrado
            // mesmo que o nome informado agora seja diferente — evita que
            // uma digitação diferente no bot ("Zé" vs "José") substitua o
            // cadastro original sem intenção.
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

    public async Task<ResultadoOperacao<Cliente>> CadastrarAsync(DadosCadastroCliente dados, CancellationToken ct = default)
    {
        var numero = WhatsAppHelper.Normalizar(dados.WhatsApp);
        if (numero.Length < 12)
        {
            return ResultadoOperacao<Cliente>.Falha("Informe o WhatsApp com DDD, por exemplo (16) 99999-9999.");
        }

        if (string.IsNullOrWhiteSpace(dados.NomeCompleto) || dados.NomeCompleto.Trim().Length < 2)
        {
            return ResultadoOperacao<Cliente>.Falha("Informe seu nome.");
        }

        var cep = new string((dados.Cep ?? string.Empty).Where(char.IsDigit).ToArray());
        if (cep.Length != 8)
        {
            return ResultadoOperacao<Cliente>.Falha("Informe um CEP com 8 números.");
        }

        var cliente = await ObterPorWhatsAppAsync(numero, ct);
        if (cliente is null)
        {
            cliente = new Cliente
            {
                NomeCompleto = dados.NomeCompleto.Trim(),
                WhatsApp = numero,
                CriadoEm = DateTime.UtcNow
            };
            _db.Clientes.Add(cliente);
        }

        cliente.Cep ??= cep;
        cliente.Bairro ??= Limpar(dados.Bairro);
        cliente.Cidade ??= Limpar(dados.Cidade);

        await _db.SaveChangesAsync(ct);
        return ResultadoOperacao<Cliente>.Ok(cliente);
    }

    private static string? Limpar(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
}
