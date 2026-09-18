using DeliciasDaNilda.Data;
using Microsoft.EntityFrameworkCore;

namespace DeliciasDaNilda.Services;

/// <summary>
/// Acesso à tabela singleton Configuracoes. Não corresponde a uma RN
/// numerada (ver Domain/Entities/Configuracao.cs) — hoje expõe apenas o
/// flag WhatsAppBotAtivo, usado pelo bot de atendimento automático via
/// WhatsApp para saber se deve responder.
/// </summary>
public interface IConfiguracaoService
{
    /// <summary>True se o atendimento automático via bot de WhatsApp está ligado.</summary>
    Task<bool> BotAtivoAsync(CancellationToken ct = default);

    /// <summary>Liga/desliga o bot de WhatsApp (ação exclusiva do ADM pelo painel).</summary>
    Task ExibirOuAtualizarBotAtivoAsync(bool ativo, CancellationToken ct = default);
}

public class ConfiguracaoService : IConfiguracaoService
{
    private readonly AppDbContext _db;

    public ConfiguracaoService(AppDbContext db)
    {
        _db = db;
    }

    private async Task<Domain.Entities.Configuracao> ObterOuCriarSingletonAsync(CancellationToken ct)
    {
        var configuracao = await _db.Configuracoes.FirstOrDefaultAsync(ct);
        if (configuracao is null)
        {
            // Defensivo: o seed em Program.cs já garante a linha única, mas
            // não deixamos o serviço quebrar se, por algum motivo, ela não existir.
            configuracao = new Domain.Entities.Configuracao
            {
                WhatsAppBotAtivo = false,
                AtualizadoEm = DateTime.UtcNow
            };
            _db.Configuracoes.Add(configuracao);
            await _db.SaveChangesAsync(ct);
        }

        return configuracao;
    }

    public async Task<bool> BotAtivoAsync(CancellationToken ct = default)
    {
        var configuracao = await ObterOuCriarSingletonAsync(ct);
        return configuracao.WhatsAppBotAtivo;
    }

    public async Task ExibirOuAtualizarBotAtivoAsync(bool ativo, CancellationToken ct = default)
    {
        var configuracao = await ObterOuCriarSingletonAsync(ct);
        configuracao.WhatsAppBotAtivo = ativo;
        configuracao.AtualizadoEm = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }
}
