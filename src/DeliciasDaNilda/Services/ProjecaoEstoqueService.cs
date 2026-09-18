using DeliciasDaNilda.Data;
using DeliciasDaNilda.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DeliciasDaNilda.Services;

/// <summary>
/// Alerta preventivo de reposição (RN15): qual ingrediente vai faltar,
/// quanto falta e a data limite para compra (antes da produção do pedido
/// afetado).
/// </summary>
public sealed record AlertaReposicaoEstoque(
    int IngredienteId,
    string IngredienteNome,
    string UnidadeMedida,
    decimal QuantidadeFaltante,
    int PedidoAfetadoId,
    DateOnly DataEntregaPedidoAfetado,
    DateOnly DataLimiteCompra);

/// <summary>Último pedido pendente de confirmação que o estoque atual ainda atende (RN14).</summary>
public sealed record PedidoCoberto(int PedidoId, DateOnly DataEntrega);

/// <summary>
/// Resultado da projeção (RN14): se o estoque atual cobre todos os pedidos
/// aguardando confirmação e, se não, o primeiro alerta encontrado.
/// <see cref="UltimoPedidoCoberto"/> é o último pedido pendente (em ordem
/// cronológica) atendido antes do primeiro que faltaria ingrediente; null se
/// não há pendentes ou se já o primeiro não é atendido.
/// </summary>
public sealed record ProjecaoEstoqueResultado(
    bool EstoqueSuficiente,
    AlertaReposicaoEstoque? Alerta,
    PedidoCoberto? UltimoPedidoCoberto = null);

public interface IProjecaoEstoqueService
{
    /// <summary>
    /// RN14/RN15 — percorre os pedidos PendenteConfirmacao em ordem cronológica
    /// (DataEntrega, Id), subtraindo a demanda por ingrediente
    /// (FichaTecnicaItem.QuantidadePorKg x Pedido.Peso) do SaldoAtual, até
    /// encontrar o primeiro ingrediente que ficaria insuficiente. Confirmados
    /// NÃO entram: o estoque deles já foi abatido na confirmação (RN12), então
    /// contá-los de novo seria contagem em dobro (decisão de produto 2026-09-18). <paramref name="diasAntecedenciaCompra"/>
    /// é o parâmetro configurável de quantos dias antes da entrega a compra
    /// precisa ser feita (RN15) — simples, sem persistência própria.
    /// </summary>
    Task<ProjecaoEstoqueResultado> ProjetarAsync(int diasAntecedenciaCompra = 3, CancellationToken ct = default);
}

/// <summary>
/// Serviço de projeção/inteligência de estoque (RN14, RN15).
/// </summary>
public class ProjecaoEstoqueService : IProjecaoEstoqueService
{
    private readonly AppDbContext _db;

    public ProjecaoEstoqueService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<ProjecaoEstoqueResultado> ProjetarAsync(int diasAntecedenciaCompra = 3, CancellationToken ct = default)
    {
        // RN14: fila cronológica de pedidos aguardando confirmação (os confirmados
        // já foram abatidos do SaldoAtual pela RN12).
        var pedidosPendentes = await _db.Pedidos
            .Where(p => p.StatusPedido == StatusPedido.PendenteConfirmacao)
            .OrderBy(p => p.DataEntrega)
            .ThenBy(p => p.Id)
            .Include(p => p.Sabor)
                .ThenInclude(s => s.FichaTecnica!)
                    .ThenInclude(f => f.Itens)
                        .ThenInclude(i => i.Ingrediente)
            .ToListAsync(ct);

        // Saldos projetados começam a partir do saldo atual real (cache),
        // mas são apenas simulados em memória — não persistidos aqui.
        var saldosProjetados = await _db.Ingredientes
            .ToDictionaryAsync(i => i.Id, i => i.SaldoAtual, ct);

        PedidoCoberto? ultimoCoberto = null;

        foreach (var pedido in pedidosPendentes)
        {
            var fichaTecnica = pedido.Sabor.FichaTecnica;
            if (fichaTecnica is null)
            {
                // Sabor sem ficha técnica: não há como calcular consumo —
                // pula (não é o objetivo desta projeção sinalizar RN04).
                continue;
            }

            foreach (var item in fichaTecnica.Itens)
            {
                var demanda = item.QuantidadePorKg * pedido.Peso;

                if (!saldosProjetados.TryGetValue(item.IngredienteId, out var saldoAtual))
                {
                    saldoAtual = 0;
                }

                var saldoAposDemanda = saldoAtual - demanda;
                saldosProjetados[item.IngredienteId] = saldoAposDemanda;

                if (saldoAposDemanda < 0)
                {
                    // RN15: primeiro ingrediente insuficiente encontrado —
                    // monta o alerta e encerra a projeção aqui.
                    var quantidadeFaltante = -saldoAposDemanda;
                    var dataLimiteCompra = pedido.DataEntrega.AddDays(-diasAntecedenciaCompra);

                    var alerta = new AlertaReposicaoEstoque(
                        IngredienteId: item.IngredienteId,
                        IngredienteNome: item.Ingrediente.Nome,
                        UnidadeMedida: item.Ingrediente.UnidadeMedida,
                        QuantidadeFaltante: quantidadeFaltante,
                        PedidoAfetadoId: pedido.Id,
                        DataEntregaPedidoAfetado: pedido.DataEntrega,
                        DataLimiteCompra: dataLimiteCompra);

                    return new ProjecaoEstoqueResultado(false, alerta, ultimoCoberto);
                }
            }

            ultimoCoberto = new PedidoCoberto(pedido.Id, pedido.DataEntrega);
        }

        return new ProjecaoEstoqueResultado(true, null, ultimoCoberto);
    }
}
