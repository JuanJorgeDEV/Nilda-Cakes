using DeliciasDaNilda.Data;
using DeliciasDaNilda.Services;
using Microsoft.EntityFrameworkCore;

namespace DeliciasDaNilda.Tests;

/// <summary>
/// Testes do flag WhatsAppBotAtivo (tabela singleton Configuracoes — não é
/// RN numerada, ver Domain/Entities/Configuracao.cs).
/// </summary>
public class ConfiguracaoServiceTests
{
    private static AppDbContext CriarContexto()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task BotAtivoAsync_SemLinhaConfigurada_CriaSingletonDesligadoPorDefault()
    {
        using var db = CriarContexto();
        var service = new ConfiguracaoService(db);

        var ativo = await service.BotAtivoAsync();

        Assert.False(ativo);
        Assert.Equal(1, await db.Configuracoes.CountAsync());
    }

    [Fact]
    public async Task ExibirOuAtualizarBotAtivoAsync_Liga_RefletidoNaLeituraSeguinte()
    {
        using var db = CriarContexto();
        var service = new ConfiguracaoService(db);

        await service.ExibirOuAtualizarBotAtivoAsync(true);
        var ativo = await service.BotAtivoAsync();

        Assert.True(ativo);
    }

    [Fact]
    public async Task ExibirOuAtualizarBotAtivoAsync_Atualiza_NaoDuplicaLinhaSingleton()
    {
        using var db = CriarContexto();
        var service = new ConfiguracaoService(db);

        await service.ExibirOuAtualizarBotAtivoAsync(true);
        await service.ExibirOuAtualizarBotAtivoAsync(false);

        Assert.Equal(1, await db.Configuracoes.CountAsync());
        Assert.False(await service.BotAtivoAsync());
    }

    [Fact]
    public async Task ExibirOuAtualizarBotAtivoAsync_AtualizaCarimboDeTempo()
    {
        using var db = CriarContexto();
        var service = new ConfiguracaoService(db);

        await service.ExibirOuAtualizarBotAtivoAsync(true);
        var configuracao = await db.Configuracoes.FirstAsync();

        Assert.True(configuracao.AtualizadoEm > DateTime.UtcNow.AddMinutes(-1));
    }
}
