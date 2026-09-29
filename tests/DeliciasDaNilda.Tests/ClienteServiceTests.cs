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
        Assert.Equal("5511988887777", cliente.WhatsApp); // sempre com DDI, igual ao que o bot recebe
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

    [Fact]
    public async Task ObterPorWhatsApp_ReconheceNumeroDoSiteQuandoBotMandaComDdi()
    {
        using var db = CriarContexto();
        var service = new ClienteService(db);
        await service.ObterOuCriarAsync("Ana", "(16) 99999-0001"); // digitado no site

        var peloBot = await service.ObterPorWhatsAppAsync("5516999990001"); // como chega do WhatsApp

        Assert.NotNull(peloBot);
        Assert.Equal("Ana", peloBot!.NomeCompleto);
    }

    [Fact]
    public async Task ObterPorWhatsApp_AceitaNumeroSemONonoDigito()
    {
        using var db = CriarContexto();
        var service = new ClienteService(db);
        await service.ObterOuCriarAsync("Ana", "16999990001");

        Assert.NotNull(await service.ObterPorWhatsAppAsync("551699990001")); // WhatsApp antigo, sem o 9
        Assert.Null(await service.ObterPorWhatsAppAsync("5516988887777"));
    }

    [Fact]
    public async Task Cadastrar_NumeroNovo_CriaComCepBairroCidade()
    {
        using var db = CriarContexto();
        var service = new ClienteService(db);

        var r = await service.CadastrarAsync(new DadosCadastroCliente("Bia Souza", "(16) 98888-1111", "14020-000", "Jardim Irajá", "Ribeirão Preto"));

        Assert.True(r.Sucesso);
        Assert.Equal("5516988881111", r.Valor!.WhatsApp);
        Assert.Equal("14020000", r.Valor.Cep);
        Assert.Equal("Jardim Irajá", r.Valor.Bairro);
    }

    [Fact]
    public async Task Cadastrar_NumeroExistente_CompletaCepSemTrocarNome()
    {
        using var db = CriarContexto();
        var service = new ClienteService(db);
        await service.ObterOuCriarAsync("Maria Silva", "16988887777");

        var r = await service.CadastrarAsync(new DadosCadastroCliente("Outro Nome", "5516988887777", "14020000", "Centro", "Ribeirão Preto"));

        Assert.True(r.Sucesso);
        Assert.Equal("Maria Silva", r.Valor!.NomeCompleto);
        Assert.Equal("Centro", r.Valor.Bairro);
        Assert.Equal(1, await db.Clientes.CountAsync());
    }

    [Theory]
    [InlineData("Ana", "169999", "14020000")]
    [InlineData("Ana", "16999990001", "1402")]
    [InlineData("", "16999990001", "14020000")]
    public async Task Cadastrar_DadosInvalidos_Recusa(string nome, string zap, string cep)
    {
        using var db = CriarContexto();

        var r = await new ClienteService(db).CadastrarAsync(new DadosCadastroCliente(nome, zap, cep, null, null));

        Assert.False(r.Sucesso);
        Assert.Equal(0, await db.Clientes.CountAsync());
    }
}
