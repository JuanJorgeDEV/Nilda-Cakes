using DeliciasDaNilda.Data;
using DeliciasDaNilda.Domain.Entities;
using DeliciasDaNilda.Domain.Enums;
using DeliciasDaNilda.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace DeliciasDaNilda.Tests;

/// <summary>
/// Testes de RN08 (transição de status) e do estorno automático de estoque
/// ao cancelar um pedido Confirmado (decisão de produto de 2026-09-16).
/// </summary>
public class PedidoServiceTests
{
    private static AppDbContext CriarContexto()
    {
        // O provedor InMemory não suporta transações reais; suprimimos o
        // warning (tratado como erro por padrão) só nos testes. Em produção,
        // com um provedor relacional, a transação de fato é usada.
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<(Cliente cliente, Sabor sabor, Ingrediente farinha)> SeedBaseAsync(AppDbContext db)
    {
        var cliente = new Cliente { NomeCompleto = "Cliente Teste", WhatsApp = $"5511{Guid.NewGuid():N}"[..13], CriadoEm = DateTime.UtcNow };
        var farinha = new Ingrediente { Nome = $"Farinha {Guid.NewGuid()}", UnidadeMedida = "kg", SaldoAtual = 100m, CriadoEm = DateTime.UtcNow };
        var sabor = new Sabor { Nome = "Chocolate", PrecoPorKg = 90m, CriadoEm = DateTime.UtcNow };
        db.Clientes.Add(cliente);
        db.Ingredientes.Add(farinha);
        db.Sabores.Add(sabor);
        await db.SaveChangesAsync();

        var ficha = new FichaTecnica { SaborId = sabor.Id, CriadoEm = DateTime.UtcNow };
        db.FichasTecnicas.Add(ficha);
        await db.SaveChangesAsync();

        db.FichaTecnicaItens.Add(new FichaTecnicaItem { FichaTecnicaId = ficha.Id, IngredienteId = farinha.Id, QuantidadePorKg = 0.5m });
        await db.SaveChangesAsync();

        return (cliente, sabor, farinha);
    }

    private static async Task<Pedido> SeedPedidoAsync(AppDbContext db, Cliente cliente, Sabor sabor, StatusPedido status, decimal peso = 2m)
    {
        var pedido = new Pedido
        {
            ClienteId = cliente.Id,
            SaborId = sabor.Id,
            Peso = peso,
            PrecoPorKgUtilizado = sabor.PrecoPorKg,
            ValorTotal = sabor.PrecoPorKg * peso,
            DataEntrega = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)),
            StatusPedido = status,
            CriadoEm = DateTime.UtcNow
        };
        db.Pedidos.Add(pedido);
        await db.SaveChangesAsync();
        return pedido;
    }

    [Fact]
    public async Task CancelarPedido_Pendente_ApenasMudaStatus_SemEstorno()
    {
        await using var db = CriarContexto();
        var (cliente, sabor, farinha) = await SeedBaseAsync(db);
        var pedido = await SeedPedidoAsync(db, cliente, sabor, StatusPedido.PendenteConfirmacao);

        var estoqueService = new EstoqueService(db);
        var service = new PedidoService(db, new PrecificacaoService(), new AgendamentoService(db), estoqueService);

        var resultado = await service.CancelarPedidoAsync(pedido.Id);

        Assert.True(resultado.Sucesso);
        var pedidoAtualizado = await db.Pedidos.FindAsync(pedido.Id);
        Assert.Equal(StatusPedido.Cancelado, pedidoAtualizado!.StatusPedido);

        // Nunca houve abate (RN12), então nenhum movimento deve existir.
        var totalMovimentos = await db.MovimentosEstoque.CountAsync(m => m.PedidoId == pedido.Id);
        Assert.Equal(0, totalMovimentos);

        var farinhaAtualizada = await db.Ingredientes.FindAsync(farinha.Id);
        Assert.Equal(100m, farinhaAtualizada!.SaldoAtual);
    }

    [Fact]
    public async Task CancelarPedido_Confirmado_EstornaSaldoAbatido()
    {
        await using var db = CriarContexto();
        var (cliente, sabor, farinha) = await SeedBaseAsync(db);
        var pedido = await SeedPedidoAsync(db, cliente, sabor, StatusPedido.PendenteConfirmacao, peso: 2m);

        var estoqueService = new EstoqueService(db);
        var service = new PedidoService(db, new PrecificacaoService(), new AgendamentoService(db), estoqueService);

        // 0.5 kg de farinha por kg de bolo x 2 kg de pedido = 1 kg abatido na confirmação.
        var abate = await estoqueService.RegistrarSaidaPorConfirmacaoAsync(pedido.Id);
        Assert.True(abate.Sucesso);
        pedido.StatusPedido = StatusPedido.Confirmado;
        await db.SaveChangesAsync();

        var farinhaPosAbate = await db.Ingredientes.FindAsync(farinha.Id);
        Assert.Equal(99m, farinhaPosAbate!.SaldoAtual);

        var resultado = await service.CancelarPedidoAsync(pedido.Id);

        Assert.True(resultado.Sucesso);
        var pedidoAtualizado = await db.Pedidos.FindAsync(pedido.Id);
        Assert.Equal(StatusPedido.Cancelado, pedidoAtualizado!.StatusPedido);

        var farinhaEstornada = await db.Ingredientes.FindAsync(farinha.Id);
        Assert.Equal(100m, farinhaEstornada!.SaldoAtual);

        var totalEstornos = await db.MovimentosEstoque
            .CountAsync(m => m.PedidoId == pedido.Id && m.Tipo == TipoMovimentoEstoque.AjusteCorrecao);
        Assert.Equal(1, totalEstornos);
    }

    [Fact]
    public async Task CancelarPedido_Confirmado_ChamadoDuasVezes_SegundaChamadaNaoAlteraNadaENaoDuplicaEstorno()
    {
        await using var db = CriarContexto();
        var (cliente, sabor, farinha) = await SeedBaseAsync(db);
        var pedido = await SeedPedidoAsync(db, cliente, sabor, StatusPedido.PendenteConfirmacao, peso: 2m);

        var estoqueService = new EstoqueService(db);
        var service = new PedidoService(db, new PrecificacaoService(), new AgendamentoService(db), estoqueService);

        await estoqueService.RegistrarSaidaPorConfirmacaoAsync(pedido.Id);
        pedido.StatusPedido = StatusPedido.Confirmado;
        await db.SaveChangesAsync();

        var primeiroCancelamento = await service.CancelarPedidoAsync(pedido.Id);
        Assert.True(primeiroCancelamento.Sucesso);

        // Segundo cancelamento: pedido já está Cancelado. O contrato atual de
        // CancelarPedidoAsync trata "já cancelado" como idempotente (Ok, sem
        // efeito colateral) — não deve duplicar o estorno.
        var segundoCancelamento = await service.CancelarPedidoAsync(pedido.Id);
        Assert.True(segundoCancelamento.Sucesso);

        var farinhaFinal = await db.Ingredientes.FindAsync(farinha.Id);
        Assert.Equal(100m, farinhaFinal!.SaldoAtual);

        var totalEstornos = await db.MovimentosEstoque
            .CountAsync(m => m.PedidoId == pedido.Id && m.Tipo == TipoMovimentoEstoque.AjusteCorrecao);
        Assert.Equal(1, totalEstornos);
    }
}
