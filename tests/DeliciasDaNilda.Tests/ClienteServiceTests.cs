using DeliciasDaNilda.Data;
using DeliciasDaNilda.Services;
using Microsoft.EntityFrameworkCore;

namespace DeliciasDaNilda.Tests;

/// <summary>Testes de RN01 (WhatsApp como chave única do cliente).</summary>
public class ClienteServiceTests
{
    private static AppDbContext CriarContexto()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task ObterOuCriarAsync_ClienteNovo_CriaComWhatsAppNormalizado()
    {
        using var db = CriarContexto();
        var service = new ClienteService(db);

        var cliente = await service.ObterOuCriarAsync("Maria Silva", "(11) 98888-7777");

        Assert.NotEqual(0, cliente.Id);
        Assert.Equal("Maria Silva", cliente.NomeCompleto);
        Assert.Equal("11988887777", cliente.WhatsApp);
        Assert.Equal(1, await db.Clientes.CountAsync());
    }

    [Fact]
    public async Task ObterOuCriarAsync_MesmoWhatsAppFormatadoDiferente_ReaproveitaClienteExistente()
    {
        using var db = CriarContexto();
        var service = new ClienteService(db);

        var primeiro = await service.ObterOuCriarAsync("Maria Silva", "(11) 98888-7777");
        var segundo = await service.ObterOuCriarAsync("Maria Silva", "11988887777");

        Assert.Equal(primeiro.Id, segundo.Id);
        Assert.Equal(1, await db.Clientes.CountAsync());
    }

    [Fact]
    public async Task ObterOuCriarAsync_ClienteExistenteComNomeDiferente_NaoSobrescreveNome()
    {
        using var db = CriarContexto();
        var service = new ClienteService(db);

        var original = await service.ObterOuCriarAsync("Maria Silva", "11988887777");
        var reobtido = await service.ObterOuCriarAsync("Maria S.", "11988887777");

        Assert.Equal(original.Id, reobtido.Id);
        Assert.Equal("Maria Silva", reobtido.NomeCompleto);
    }

    [Fact]
    public async Task ObterOuCriarAsync_WhatsAppSemDigitos_LancaArgumentException()
    {
        using var db = CriarContexto();
        var service = new ClienteService(db);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.ObterOuCriarAsync("Maria Silva", "abc"));
    }
}
