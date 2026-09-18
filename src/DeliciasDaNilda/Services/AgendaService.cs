using DeliciasDaNilda.Data;
using DeliciasDaNilda.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DeliciasDaNilda.Services;

/// <summary>
/// Um pedido do dia na visão consolidada da agenda (RN16).
/// </summary>
public sealed record PedidoDoDia(
    int PedidoId,
    string ClienteNomeCompleto,
    string ClienteWhatsApp,
    string Sabor,
    decimal Peso,
    TimeOnly? HorarioRetirada,
    StatusPagamento StatusPagamento,
    StatusPedido StatusPedido);

/// <summary>
/// Quantidade total necessária de um ingrediente, agregada entre todos os
/// pedidos do dia (RN16, cálculo derivado de RN12).
/// </summary>
public sealed record IngredienteNecessarioDoDia(
    int IngredienteId,
    string IngredienteNome,
    string UnidadeMedida,
    decimal QuantidadeTotalNecessaria);

/// <summary>
/// Visão consolidada da agenda para uma data (RN16): pedidos do dia e a
/// ficha técnica total (soma de ingredientes necessários) para produção.
/// </summary>
public sealed record AgendaDoDiaResultado(
    DateOnly Data,
    IReadOnlyList<PedidoDoDia> Pedidos,
    IReadOnlyList<IngredienteNecessarioDoDia> FichaTecnicaTotalDoDia);

public interface IAgendaService
{
    /// <summary>
    /// RN16 — agenda operacional consolidada de uma data: pedidos (com
    /// cliente, sabor, peso, horário de retirada e status de pagamento/pedido)
    /// e a ficha técnica total do dia (soma de FichaTecnicaItem.QuantidadePorKg
    /// x Pedido.Peso, para pedidos Confirmado ou PendenteConfirmacao — mesmo
    /// filtro de capacidade de RN06/RN07).
    /// </summary>
    Task<AgendaDoDiaResultado> ObterAgendaDoDiaAsync(DateOnly data, CancellationToken ct = default);
}

/// <summary>
/// Serviço de agenda operacional integrada (RN16).
/// </summary>
public class AgendaService : IAgendaService
{
    // RN06/RN07 — mesmo conjunto de status que ocupam capacidade diária,
    // reaproveitado de AgendamentoService para não duplicar a regra.
    private static readonly StatusPedido[] StatusQueEntramNaAgenda = AgendamentoService.StatusQueOcupamCapacidade;

    private readonly AppDbContext _db;

    public AgendaService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<AgendaDoDiaResultado> ObterAgendaDoDiaAsync(DateOnly data, CancellationToken ct = default)
    {
        var pedidosDoDia = await _db.Pedidos
            .Where(p => p.DataEntrega == data && StatusQueEntramNaAgenda.Contains(p.StatusPedido))
            .Include(p => p.Cliente)
            .Include(p => p.Sabor)
                .ThenInclude(s => s.FichaTecnica!)
                    .ThenInclude(f => f.Itens)
                        .ThenInclude(i => i.Ingrediente)
            .OrderBy(p => p.HorarioRetirada)
            .ThenBy(p => p.Id)
            .ToListAsync(ct);

        var pedidos = pedidosDoDia
            .Select(p => new PedidoDoDia(
                PedidoId: p.Id,
                ClienteNomeCompleto: p.Cliente.NomeCompleto,
                ClienteWhatsApp: p.Cliente.WhatsApp,
                Sabor: p.Sabor.Nome,
                Peso: p.Peso,
                HorarioRetirada: p.HorarioRetirada,
                StatusPagamento: p.StatusPagamento,
                StatusPedido: p.StatusPedido))
            .ToList();

        // Ficha técnica total do dia: agregação de todos os ingredientes
        // necessários (RN16), somando QuantidadePorKg x Peso de cada pedido.
        var totaisPorIngrediente = new Dictionary<int, IngredienteNecessarioDoDia>();

        foreach (var pedido in pedidosDoDia)
        {
            var fichaTecnica = pedido.Sabor.FichaTecnica;
            if (fichaTecnica is null)
            {
                // Sabor sem ficha técnica cadastrada (RN04): não há como
                // calcular consumo para este pedido — pula silenciosamente,
                // não é objetivo desta consulta sinalizar RN04.
                continue;
            }

            foreach (var item in fichaTecnica.Itens)
            {
                var quantidadeNecessaria = item.QuantidadePorKg * pedido.Peso;

                if (totaisPorIngrediente.TryGetValue(item.IngredienteId, out var existente))
                {
                    totaisPorIngrediente[item.IngredienteId] = existente with
                    {
                        QuantidadeTotalNecessaria = existente.QuantidadeTotalNecessaria + quantidadeNecessaria
                    };
                }
                else
                {
                    totaisPorIngrediente[item.IngredienteId] = new IngredienteNecessarioDoDia(
                        IngredienteId: item.IngredienteId,
                        IngredienteNome: item.Ingrediente.Nome,
                        UnidadeMedida: item.Ingrediente.UnidadeMedida,
                        QuantidadeTotalNecessaria: quantidadeNecessaria);
                }
            }
        }

        var fichaTecnicaTotalDoDia = totaisPorIngrediente.Values
            .OrderBy(i => i.IngredienteNome)
            .ToList();

        return new AgendaDoDiaResultado(data, pedidos, fichaTecnicaTotalDoDia);
    }
}
