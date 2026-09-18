using DeliciasDaNilda.Data;
using DeliciasDaNilda.Domain.Enums;
using DeliciasDaNilda.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace DeliciasDaNilda.Tests;

/// <summary>Testes do seed de desenvolvimento (Data/DevSeed.cs) contra InMemory.</summary>
public class DevSeedTests
{
    private sealed class FakeEnv(string nome) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = nome;
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static async Task<ServiceProvider> CriarProviderAsync()
    {
        var nomeDb = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        // InMemory não suporta transações reais: mesmo padrão dos demais testes.
        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(nomeDb)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
        services.AddScoped<IPrecificacaoService, PrecificacaoService>();
        services.AddScoped<IAgendamentoService, AgendamentoService>();
        services.AddScoped<IEstoqueService, EstoqueService>();
        services.AddScoped<IPedidoService, PedidoService>();
        services.AddScoped<IProjecaoEstoqueService, ProjecaoEstoqueService>();
        services.AddScoped<IPagamentoService, PagamentoService>();
        services.AddScoped<IAdministradorAuthService, AdministradorAuthService>();
        services.AddScoped<ISaborService, SaborService>();
        services.AddScoped<IIngredienteService, IngredienteService>();
        services.AddScoped<IClienteService, ClienteService>();
        var sp = services.BuildServiceProvider();

        using var scope = sp.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IAdministradorAuthService>()
            .CriarAdministradorAsync("Admin Teste", "admin@teste.local", "Senha123!");
        return sp;
    }

    [Fact]
    public async Task Seed_EmDevelopment_CriaCenariosPrincipais()
    {
        await using var sp = await CriarProviderAsync();

        var r = await DevSeed.ExecutarAsync(sp, new FakeEnv("Development"), reset: false);

        Assert.True(r.Executado, r.MotivoAborto);
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Dia lotado: 3 pedidos ativos (RN06/RN07).
        var ativos = new[] { StatusPedido.PendenteConfirmacao, StatusPedido.Confirmado };
        Assert.Equal(3, await db.Pedidos.CountAsync(p => p.DataEntrega == r.DiaLotado && ativos.Contains(p.StatusPedido)));

        // Cancelado com estorno, pagamentos parcial e integral.
        Assert.True(await db.Pedidos.AnyAsync(p => p.StatusPedido == StatusPedido.Cancelado));
        Assert.True(await db.MovimentosEstoque.AnyAsync(m => m.Tipo == TipoMovimentoEstoque.AjusteCorrecao && m.PedidoId != null));
        Assert.True(await db.Pedidos.AnyAsync(p => p.StatusPagamento == StatusPagamento.SinalPago));
        Assert.True(await db.Pedidos.AnyAsync(p => p.StatusPagamento == StatusPagamento.PagoIntegral));
        Assert.True(await db.Pedidos.AnyAsync(p => p.StatusPedido == StatusPedido.PendenteConfirmacao));

        // Alerta de projeção (RN14/RN15): roda sobre pendentes, contra o saldo já abatido pelos confirmados.
        var projecao = await scope.ServiceProvider.GetRequiredService<IProjecaoEstoqueService>().ProjetarAsync();
        Assert.False(projecao.EstoqueSuficiente);
        Assert.Equal("Morango", projecao.Alerta!.IngredienteNome);
        Assert.Equal(0.15m, projecao.Alerta.QuantidadeFaltante);
        var afetado = await db.Pedidos.SingleAsync(p => p.Id == projecao.Alerta.PedidoAfetadoId);
        Assert.Equal(StatusPedido.PendenteConfirmacao, afetado.StatusPedido);
        Assert.NotNull(projecao.UltimoPedidoCoberto);

        // Consistência: SaldoAtual == soma dos movimentos com sinal.
        foreach (var ing in await db.Ingredientes.ToListAsync())
        {
            var movs = await db.MovimentosEstoque.Where(m => m.IngredienteId == ing.Id).ToListAsync();
            var saldo = movs.Sum(m => m.Tipo is TipoMovimentoEstoque.Entrada or TipoMovimentoEstoque.AjusteCorrecao ? m.Quantidade : -m.Quantidade);
            Assert.Equal(saldo, ing.SaldoAtual);
        }
    }

    [Fact]
    public async Task Seed_ComSaboresExistentes_AbortaSemReset()
    {
        await using var sp = await CriarProviderAsync();
        var env = new FakeEnv("Development");
        Assert.True((await DevSeed.ExecutarAsync(sp, env, false)).Executado);

        var segunda = await DevSeed.ExecutarAsync(sp, env, false);

        Assert.False(segunda.Executado);
        Assert.Contains("banco VAZIO", segunda.MotivoAborto);
    }

    [Fact]
    public async Task Seed_ForaDeDevelopment_Recusa()
    {
        await using var sp = await CriarProviderAsync();

        var r = await DevSeed.ExecutarAsync(sp, new FakeEnv("Production"), reset: true);

        Assert.False(r.Executado);
        using var scope = sp.CreateScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<AppDbContext>().Sabores.AnyAsync());
    }
}
