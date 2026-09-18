using DeliciasDaNilda.Data;
using DeliciasDaNilda.Services;
using Microsoft.EntityFrameworkCore;

namespace DeliciasDaNilda.Tests;

/// <summary>Testes de RN02: hash/verificação de senha de Administrador.</summary>
public class AdministradorAuthServiceTests
{
    private static AppDbContext CriarContexto()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task CriarAdministrador_NuncaGravaSenhaEmTextoPlano()
    {
        await using var db = CriarContexto();
        var service = new AdministradorAuthService(db);

        var administrador = await service.CriarAdministradorAsync("ADM Teste", "adm@teste.com", "SenhaForte123!");

        Assert.NotEqual("SenhaForte123!", administrador.SenhaHash);
        Assert.False(string.IsNullOrWhiteSpace(administrador.SenhaHash));
    }

    [Fact]
    public async Task VerificarLogin_ComSenhaCorreta_RetornaSucesso()
    {
        await using var db = CriarContexto();
        var service = new AdministradorAuthService(db);
        await service.CriarAdministradorAsync("ADM Teste", "adm@teste.com", "SenhaForte123!");

        var (sucesso, administrador) = await service.VerificarLoginAsync("adm@teste.com", "SenhaForte123!");

        Assert.True(sucesso);
        Assert.NotNull(administrador);
        Assert.Equal("adm@teste.com", administrador!.Email);
    }

    [Fact]
    public async Task VerificarLogin_ComSenhaIncorreta_RetornaFalha()
    {
        await using var db = CriarContexto();
        var service = new AdministradorAuthService(db);
        await service.CriarAdministradorAsync("ADM Teste", "adm@teste.com", "SenhaForte123!");

        var (sucesso, administrador) = await service.VerificarLoginAsync("adm@teste.com", "SenhaErrada");

        Assert.False(sucesso);
        Assert.Null(administrador);
    }

    [Fact]
    public async Task VerificarLogin_ComEmailInexistente_RetornaFalha()
    {
        await using var db = CriarContexto();
        var service = new AdministradorAuthService(db);

        var (sucesso, administrador) = await service.VerificarLoginAsync("naoexiste@teste.com", "QualquerSenha");

        Assert.False(sucesso);
        Assert.Null(administrador);
    }

    [Fact]
    public async Task VerificarLogin_ComAdministradorInativo_RetornaFalha()
    {
        await using var db = CriarContexto();
        var service = new AdministradorAuthService(db);
        var administrador = await service.CriarAdministradorAsync("ADM Teste", "adm@teste.com", "SenhaForte123!");
        administrador.Ativo = false;
        await db.SaveChangesAsync();

        var (sucesso, resultado) = await service.VerificarLoginAsync("adm@teste.com", "SenhaForte123!");

        Assert.False(sucesso);
        Assert.Null(resultado);
    }
}
