using System.Text.RegularExpressions;
using DeliciasDaNilda.Data;
using DeliciasDaNilda.Domain.Entities;
using DeliciasDaNilda.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace DeliciasDaNilda.Services;

/// <summary>Uma mensagem como exibida na tela de edição.</summary>
public sealed record MensagemBotItem(
    MensagemBotDefinicao Definicao,
    string Atual,
    bool Personalizada);

/// <summary>
/// Textos configuráveis do bot de WhatsApp (padrão + overrides em
/// MensagensBot). Não corresponde a uma RN numerada.
/// </summary>
public interface IMensagemBotService
{
    /// <summary>Todas as mensagens efetivas (padrão, ou override quando existe) por chave.</summary>
    Task<IReadOnlyDictionary<string, string>> ObterEfetivasAsync(CancellationToken ct = default);

    /// <summary>Lista para a tela de edição, na ordem de MensagensBotPadrao.Todas.</summary>
    Task<IReadOnlyList<MensagemBotItem>> ListarAsync(CancellationToken ct = default);

    /// <summary>Valida e salva o texto de uma mensagem. Texto igual ao padrão remove a personalização.</summary>
    Task<ResultadoOperacao> SalvarAsync(string chave, string? texto, CancellationToken ct = default);

    /// <summary>Remove a personalização (volta ao texto padrão).</summary>
    Task<ResultadoOperacao> RestaurarPadraoAsync(string chave, CancellationToken ct = default);
}

public partial class MensagemBotService : IMensagemBotService
{
    private readonly AppDbContext _db;

    public MensagemBotService(AppDbContext db)
    {
        _db = db;
    }

    [GeneratedRegex(@"\{([A-Za-z_]+)\}")]
    private static partial Regex PlaceholderRegex();

    public async Task<IReadOnlyDictionary<string, string>> ObterEfetivasAsync(CancellationToken ct = default)
    {
        var overrides = await _db.MensagensBot.AsNoTracking().ToDictionaryAsync(m => m.Chave, m => m.Texto, ct);
        return MensagensBotPadrao.Todas.ToDictionary(
            d => d.Chave,
            d => overrides.TryGetValue(d.Chave, out var texto) ? texto : d.Padrao);
    }

    public async Task<IReadOnlyList<MensagemBotItem>> ListarAsync(CancellationToken ct = default)
    {
        var overrides = await _db.MensagensBot.AsNoTracking().ToDictionaryAsync(m => m.Chave, m => m.Texto, ct);
        return MensagensBotPadrao.Todas
            .Select(d => overrides.TryGetValue(d.Chave, out var texto)
                ? new MensagemBotItem(d, texto, true)
                : new MensagemBotItem(d, d.Padrao, false))
            .ToList();
    }

    public async Task<ResultadoOperacao> SalvarAsync(string chave, string? texto, CancellationToken ct = default)
    {
        var definicao = MensagensBotPadrao.Buscar(chave);
        if (definicao is null)
        {
            return ResultadoOperacao.Falha("Mensagem não encontrada.");
        }

        // Textareas enviam \r\n; guardamos só \n.
        var normalizado = (texto ?? string.Empty).Replace("\r\n", "\n").Replace("\r", "\n").Trim();

        if (normalizado.Length == 0)
        {
            return ResultadoOperacao.Falha("O texto da mensagem não pode ficar vazio. Se quiser voltar ao original, use \"Restaurar texto original\".");
        }

        if (normalizado.Length > MensagensBotPadrao.TamanhoMaximoTexto)
        {
            return ResultadoOperacao.Falha(
                $"O texto é grande demais ({normalizado.Length} caracteres). O máximo é {MensagensBotPadrao.TamanhoMaximoTexto}.");
        }

        var usados = PlaceholderRegex().Matches(normalizado).Select(m => m.Groups[1].Value).ToHashSet();

        foreach (var obrigatorio in definicao.Obrigatorios)
        {
            if (!usados.Contains(obrigatorio))
            {
                return ResultadoOperacao.Falha(
                    $"Esta mensagem precisa manter {{{obrigatorio}}} no texto, pois é onde aparece " +
                    $"{DescricaoInformacao(obrigatorio)}.");
            }
        }

        var invalido = usados.FirstOrDefault(u => !definicao.Placeholders.Contains(u));
        if (invalido is not null)
        {
            var permitidas = definicao.Placeholders.Count == 0
                ? "Esta mensagem não usa nenhuma informação entre chaves."
                : "Nesta mensagem você pode usar: " + string.Join(", ", definicao.Placeholders.Select(p => "{" + p + "}")) + ".";
            return ResultadoOperacao.Falha($"{{{invalido}}} não pode ser usado nesta mensagem. {permitidas}");
        }

        var existente = await _db.MensagensBot.FirstOrDefaultAsync(m => m.Chave == chave, ct);

        if (normalizado == definicao.Padrao)
        {
            if (existente is not null)
            {
                _db.MensagensBot.Remove(existente);
                await _db.SaveChangesAsync(ct);
            }
            return ResultadoOperacao.Ok();
        }

        if (existente is null)
        {
            _db.MensagensBot.Add(new MensagemBot { Chave = chave, Texto = normalizado, AtualizadoEm = DateTime.UtcNow });
        }
        else
        {
            existente.Texto = normalizado;
            existente.AtualizadoEm = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        return ResultadoOperacao.Ok();
    }

    public async Task<ResultadoOperacao> RestaurarPadraoAsync(string chave, CancellationToken ct = default)
    {
        if (MensagensBotPadrao.Buscar(chave) is null)
        {
            return ResultadoOperacao.Falha("Mensagem não encontrada.");
        }

        var existente = await _db.MensagensBot.FirstOrDefaultAsync(m => m.Chave == chave, ct);
        if (existente is not null)
        {
            _db.MensagensBot.Remove(existente);
            await _db.SaveChangesAsync(ct);
        }

        return ResultadoOperacao.Ok();
    }

    private static string DescricaoInformacao(string nome)
        => MensagensBotPadrao.InformacoesDisponiveis.FirstOrDefault(i => i.Nome == nome)?.Descricao ?? nome;
}
