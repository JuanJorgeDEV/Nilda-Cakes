using DeliciasDaNilda.Data;
using DeliciasDaNilda.Domain.Enums;
using DeliciasDaNilda.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace DeliciasDaNilda.Services;

public interface IAgendamentoService
{
    /// <summary>
    /// RN05 — data mínima permitida para entrega, contando 3 dias úteis a
    /// partir de "agora" (exclui sábados e domingos).
    /// LIMITAÇÃO CONHECIDA: feriados nacionais não são considerados no MVP
    /// (não há decisão de produto/lista de feriados definida ainda). Sinalizar
    /// à Nilda antes de assumir que isso é aceitável para produção.
    /// </summary>
    DateOnly CalcularDataMinimaPermitida(DateTime agora);

    /// <summary>RN05 — true se a data de entrega respeita a antecedência mínima.</summary>
    bool AtendeAntecedenciaMinima(DateOnly dataEntrega, DateTime agora);

    /// <summary>
    /// RN06/RN07 — conta pedidos com status PendenteConfirmacao OU Confirmado
    /// na data informada (pedidos cancelados/concluídos não contam para a
    /// capacidade do dia).
    /// </summary>
    Task<int> ContarPedidosAtivosNaDataAsync(DateOnly data, CancellationToken ct = default);

    /// <summary>RN06/RN07 — true se a data ainda não atingiu o teto de 3 bolos/dia.</summary>
    Task<bool> TemCapacidadeDisponivelAsync(DateOnly data, CancellationToken ct = default);

    /// <summary>
    /// RN05 + RN06/RN07 — validação completa de uma data candidata para novo
    /// pedido (antecedência mínima e capacidade diária).
    /// </summary>
    Task<ResultadoOperacao> ValidarDataParaNovoPedidoAsync(DateOnly dataEntrega, DateTime agora, CancellationToken ct = default);

    /// <summary>
    /// RN06/RN07 — para uso do calendário (frontend): retorna, para cada data
    /// do intervalo [inicio, fim], se ela está bloqueada (>= 3 pedidos ativos).
    /// </summary>
    Task<IReadOnlyDictionary<DateOnly, bool>> ObterBloqueiosNoIntervaloAsync(DateOnly inicio, DateOnly fim, CancellationToken ct = default);
}

/// <summary>
/// Serviço de agendamento: antecedência mínima (RN05) e capacidade diária
/// (RN06/RN07).
/// </summary>
public class AgendamentoService : IAgendamentoService
{
    /// <summary>RN05 — 3 dias úteis de antecedência mínima.</summary>
    public const int DiasUteisMinimosAntecedencia = 3;

    /// <summary>RN06 — limite operacional de 3 bolos por dia.</summary>
    public const int CapacidadeMaximaDiaria = 3;

    /// <summary>
    /// RN06/RN07 — status que ocupam capacidade diária. Reaproveitado por
    /// outros services (ex.: AgendaService/RN16) para não duplicar a regra.
    /// </summary>
    public static readonly StatusPedido[] StatusQueOcupamCapacidade =
    {
        StatusPedido.PendenteConfirmacao,
        StatusPedido.Confirmado
    };

    private readonly AppDbContext _db;

    public AgendamentoService(AppDbContext db)
    {
        _db = db;
    }

    public DateOnly CalcularDataMinimaPermitida(DateTime agora)
    {
        var data = DateOnly.FromDateTime(agora);
        var diasUteisAdicionados = 0;

        while (diasUteisAdicionados < DiasUteisMinimosAntecedencia)
        {
            data = data.AddDays(1);
            if (data.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
            {
                diasUteisAdicionados++;
            }
        }

        return data;
    }

    public bool AtendeAntecedenciaMinima(DateOnly dataEntrega, DateTime agora)
        => dataEntrega >= CalcularDataMinimaPermitida(agora);

    public Task<int> ContarPedidosAtivosNaDataAsync(DateOnly data, CancellationToken ct = default)
        => _db.Pedidos
            .Where(p => p.DataEntrega == data && StatusQueOcupamCapacidade.Contains(p.StatusPedido))
            .CountAsync(ct);

    public async Task<bool> TemCapacidadeDisponivelAsync(DateOnly data, CancellationToken ct = default)
        => await ContarPedidosAtivosNaDataAsync(data, ct) < CapacidadeMaximaDiaria;

    public async Task<ResultadoOperacao> ValidarDataParaNovoPedidoAsync(DateOnly dataEntrega, DateTime agora, CancellationToken ct = default)
    {
        if (!AtendeAntecedenciaMinima(dataEntrega, agora))
        {
            var dataMinima = CalcularDataMinimaPermitida(agora);
            return ResultadoOperacao.Falha(
                $"A data de entrega deve respeitar a antecedência mínima de {DiasUteisMinimosAntecedencia} dias úteis (a partir de {dataMinima:dd/MM/yyyy}).");
        }

        if (!await TemCapacidadeDisponivelAsync(dataEntrega, ct))
        {
            return ResultadoOperacao.Falha(
                $"A data {dataEntrega:dd/MM/yyyy} já atingiu o limite de {CapacidadeMaximaDiaria} encomendas e está indisponível.");
        }

        return ResultadoOperacao.Ok();
    }

    public async Task<IReadOnlyDictionary<DateOnly, bool>> ObterBloqueiosNoIntervaloAsync(DateOnly inicio, DateOnly fim, CancellationToken ct = default)
    {
        var contagens = await _db.Pedidos
            .Where(p => p.DataEntrega >= inicio && p.DataEntrega <= fim && StatusQueOcupamCapacidade.Contains(p.StatusPedido))
            .GroupBy(p => p.DataEntrega)
            .Select(g => new { Data = g.Key, Quantidade = g.Count() })
            .ToListAsync(ct);

        var mapaContagens = contagens.ToDictionary(x => x.Data, x => x.Quantidade);

        var resultado = new Dictionary<DateOnly, bool>();
        for (var data = inicio; data <= fim; data = data.AddDays(1))
        {
            mapaContagens.TryGetValue(data, out var quantidade);
            resultado[data] = quantidade >= CapacidadeMaximaDiaria;
        }

        return resultado;
    }
}
