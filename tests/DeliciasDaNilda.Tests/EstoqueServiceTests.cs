using DeliciasDaNilda.Data;
using DeliciasDaNilda.Domain.Entities;
using DeliciasDaNilda.Domain.Enums;
using DeliciasDaNilda.Services;
using Microsoft.EntityFrameworkCore;

namespace DeliciasDaNilda.Tests;

/// <summary>Testes de RN11 (entrada), RN12 (abate automático, idempotente) e RN13 (perda).</summary>
public class EstoqueServiceTests
{
    private static AppDbContext CriarContexto()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<(Administrador administrador, Ingrediente ingrediente)> SeedBaseAsync(AppDbContext db, decimal saldoInicial = 100m)
    {
        var administrador = new Administrador { NomeCompleto = "ADM Teste", Email = $"{Guid.NewGuid()}@teste.com", SenhaHash = "hash", CriadoEm = DateTime.UtcNow };
        var ingrediente = new Ingrediente { Nome = $"Farinha {Guid.NewGuid()}", UnidadeMedida = "kg", SaldoAtual = saldoInicial, CriadoEm = DateTime.UtcNow };
        db.Administradores.Add(administrador);
        db.Ingredientes.Add(ingrediente);
        await db.SaveChangesAsync();
        return (administrador, ingrediente);
    }

    [Fact]
    public async Task RegistrarEntrada_IncrementaSaldoEGeraMovimento()
    {
        await using var db = CriarContexto();
        var (administrador, ingrediente) = await SeedBaseAsync(db, saldoInicial: 10m);
        var service = new EstoqueService(db);

        var resultado = await service.RegistrarEntradaAsync(ingrediente.Id, 5m, administrador.Id);

        Assert.True(resultado.Sucesso);
        var ingredienteAtualizado = await db.Ingredientes.FindAsync(ingrediente.Id);
        Assert.Equal(15m, ingredienteAtualizado!.SaldoAtual);

        var movimento = Assert.Single(db.MovimentosEstoque.Local);
        Assert.Equal(TipoMovimentoEstoque.Entrada, movimento.Tipo);
        Assert.Equal(5m, movimento.Quantidade);
    }

    [Fact]
    public async Task RegistrarSaidaPorConfirmacao_AbateSegundoFichaTecnicaXPeso()
    {
        await using var db = CriarContexto();
        var (_, farinha) = await SeedBaseAsync(db, saldoInicial: 100m);

        var cliente = new Cliente { NomeCompleto = "Cliente", WhatsApp = "5511999999999", CriadoEm = DateTime.UtcNow };
        var sabor = new Sabor { Nome = "Chocolate", PrecoPorKg = 90m, CriadoEm = DateTime.UtcNow };
        db.Clientes.Add(cliente);
        db.Sabores.Add(sabor);
        await db.SaveChangesAsync();

        var ficha = new FichaTecnica { SaborId = sabor.Id, CriadoEm = DateTime.UtcNow };
        db.FichasTecnicas.Add(ficha);
        await db.SaveChangesAsync();

        db.FichaTecnicaItens.Add(new FichaTecnicaItem { FichaTecnicaId = ficha.Id, IngredienteId = farinha.Id, QuantidadePorKg = 0.5m });
        await db.SaveChangesAsync();

        var pedido = new Pedido
        {
            ClienteId = cliente.Id,
            SaborId = sabor.Id,
            Peso = 2m,
            PrecoPorKgUtilizado = sabor.PrecoPorKg,
            ValorTotal = sabor.PrecoPorKg * 2m,
            DataEntrega = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)),
            StatusPedido = StatusPedido.PendenteConfirmacao,
            CriadoEm = DateTime.UtcNow
        };
        db.Pedidos.Add(pedido);
        await db.SaveChangesAsync();

        var service = new EstoqueService(db);

        // 0.5 kg de farinha por kg de bolo x 2 kg de pedido = 1 kg abatido.
        var resultado = await service.RegistrarSaidaPorConfirmacaoAsync(pedido.Id);

        Assert.True(resultado.Sucesso);
        var farinhaAtualizada = await db.Ingredientes.FindAsync(farinha.Id);
        Assert.Equal(99m, farinhaAtualizada!.SaldoAtual);
    }

    [Fact]
    public async Task RegistrarSaidaPorConfirmacao_ChamadoDuasVezes_NaoAbateDuasVezes()
    {
        await using var db = CriarContexto();
        var (_, farinha) = await SeedBaseAsync(db, saldoInicial: 100m);

        var cliente = new Cliente { NomeCompleto = "Cliente", WhatsApp = "5511999999998", CriadoEm = DateTime.UtcNow };
        var sabor = new Sabor { Nome = "Baunilha", PrecoPorKg = 70m, CriadoEm = DateTime.UtcNow };
        db.Clientes.Add(cliente);
        db.Sabores.Add(sabor);
        await db.SaveChangesAsync();

        var ficha = new FichaTecnica { SaborId = sabor.Id, CriadoEm = DateTime.UtcNow };
        db.FichasTecnicas.Add(ficha);
        await db.SaveChangesAsync();

        db.FichaTecnicaItens.Add(new FichaTecnicaItem { FichaTecnicaId = ficha.Id, IngredienteId = farinha.Id, QuantidadePorKg = 1m });
        await db.SaveChangesAsync();

        var pedido = new Pedido
        {
            ClienteId = cliente.Id,
            SaborId = sabor.Id,
            Peso = 3m,
            PrecoPorKgUtilizado = sabor.PrecoPorKg,
            ValorTotal = sabor.PrecoPorKg * 3m,
            DataEntrega = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)),
            StatusPedido = StatusPedido.PendenteConfirmacao,
            CriadoEm = DateTime.UtcNow
        };
        db.Pedidos.Add(pedido);
        await db.SaveChangesAsync();

        var service = new EstoqueService(db);

        await service.RegistrarSaidaPorConfirmacaoAsync(pedido.Id);
        await service.RegistrarSaidaPorConfirmacaoAsync(pedido.Id);

        var farinhaAtualizada = await db.Ingredientes.FindAsync(farinha.Id);
        // Só um abate de 3kg deve ter ocorrido (idempotência RN12).
        Assert.Equal(97m, farinhaAtualizada!.SaldoAtual);

        var totalMovimentosSaida = await db.MovimentosEstoque
            .CountAsync(m => m.PedidoId == pedido.Id && m.Tipo == TipoMovimentoEstoque.SaidaConfirmacaoPedido);
        Assert.Equal(1, totalMovimentosSaida);
    }

    [Fact]
    public async Task RegistrarAjustePerda_SemMotivo_Falha()
    {
        await using var db = CriarContexto();
        var (administrador, ingrediente) = await SeedBaseAsync(db);
        var service = new EstoqueService(db);

        var resultado = await service.RegistrarAjustePerdaAsync(ingrediente.Id, 2m, administrador.Id, motivo: "");

        Assert.False(resultado.Sucesso);
    }

    [Fact]
    public async Task RegistrarAjustePerda_SubtraiSaldoEGeraMovimento()
    {
        await using var db = CriarContexto();
        var (administrador, ingrediente) = await SeedBaseAsync(db, saldoInicial: 20m);
        var service = new EstoqueService(db);

        var resultado = await service.RegistrarAjustePerdaAsync(ingrediente.Id, 3m, administrador.Id, motivo: "Queda no chão");

        Assert.True(resultado.Sucesso);
        var ingredienteAtualizado = await db.Ingredientes.FindAsync(ingrediente.Id);
        Assert.Equal(17m, ingredienteAtualizado!.SaldoAtual);
    }
}
