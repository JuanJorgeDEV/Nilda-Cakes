using DeliciasDaNilda.Data;
using DeliciasDaNilda.Domain.Entities;
using DeliciasDaNilda.Domain.Enums;
using DeliciasDaNilda.Services;
using Microsoft.EntityFrameworkCore;

namespace DeliciasDaNilda.Tests;

/// <summary>Testes de RN05 (antecedência mínima) e RN06/RN07 (capacidade diária).</summary>
public class AgendamentoServiceTests
{
    private static AppDbContext CriarContexto()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    // Quarta-feira 16/09/2026 (data usada no ambiente deste projeto).
    private static readonly DateTime SegundaFeiraBase = new(2026, 9, 14, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void CalcularDataMinimaPermitida_APartirDeSegunda_PulaFinsDeSemana()
    {
        var service = new AgendamentoService(CriarContexto());

        // Segunda 14/09 + 3 dias úteis => terça, quarta, quinta => 17/09/2026.
        var dataMinima = service.CalcularDataMinimaPermitida(SegundaFeiraBase);

        Assert.Equal(new DateOnly(2026, 9, 17), dataMinima);
    }

    [Fact]
    public void CalcularDataMinimaPermitida_APartirDeQuinta_PulaFinalDeSemana()
    {
        var service = new AgendamentoService(CriarContexto());
        var quintaFeira = new DateTime(2026, 9, 17, 10, 0, 0, DateTimeKind.Utc);

        // Quinta 17/09 + 3 dias úteis => sexta 18, (pula sáb/dom), segunda 21, terça 22.
        var dataMinima = service.CalcularDataMinimaPermitida(quintaFeira);

        Assert.Equal(new DateOnly(2026, 9, 22), dataMinima);
    }

    [Fact]
    public void AtendeAntecedenciaMinima_DataAnteriorAMinima_RetornaFalse()
    {
        var service = new AgendamentoService(CriarContexto());

        Assert.False(service.AtendeAntecedenciaMinima(new DateOnly(2026, 9, 16), SegundaFeiraBase));
        Assert.True(service.AtendeAntecedenciaMinima(new DateOnly(2026, 9, 17), SegundaFeiraBase));
    }

    [Fact]
    public async Task TemCapacidadeDisponivel_ComTresPedidosAtivos_RetornaFalse()
    {
        await using var db = CriarContexto();
        var data = new DateOnly(2026, 9, 20);
        await SeedPedidosAsync(db, data, StatusPedido.PendenteConfirmacao, StatusPedido.Confirmado, StatusPedido.PendenteConfirmacao);

        var service = new AgendamentoService(db);

        Assert.Equal(3, await service.ContarPedidosAtivosNaDataAsync(data));
        Assert.False(await service.TemCapacidadeDisponivelAsync(data));
    }

    [Fact]
    public async Task ContarPedidosAtivos_IgnoraPedidosCancelados()
    {
        await using var db = CriarContexto();
        var data = new DateOnly(2026, 9, 21);
        await SeedPedidosAsync(db, data, StatusPedido.Cancelado, StatusPedido.Cancelado, StatusPedido.Confirmado);

        var service = new AgendamentoService(db);

        Assert.Equal(1, await service.ContarPedidosAtivosNaDataAsync(data));
        Assert.True(await service.TemCapacidadeDisponivelAsync(data));
    }

    private static async Task SeedPedidosAsync(AppDbContext db, DateOnly data, params StatusPedido[] statuses)
    {
        var cliente = new Cliente { NomeCompleto = "Cliente Teste", WhatsApp = Guid.NewGuid().ToString("N")[..15], CriadoEm = DateTime.UtcNow };
        var sabor = new Sabor { Nome = $"Sabor {Guid.NewGuid()}", PrecoPorKg = 80m, CriadoEm = DateTime.UtcNow };
        db.Clientes.Add(cliente);
        db.Sabores.Add(sabor);
        await db.SaveChangesAsync();

        foreach (var status in statuses)
        {
            db.Pedidos.Add(new Pedido
            {
                ClienteId = cliente.Id,
                SaborId = sabor.Id,
                Peso = 1m,
                PrecoPorKgUtilizado = sabor.PrecoPorKg,
                ValorTotal = sabor.PrecoPorKg,
                DataEntrega = data,
                StatusPedido = status,
                CriadoEm = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync();
    }
}
