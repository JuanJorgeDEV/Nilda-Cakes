using DeliciasDaNilda.Domain.Entities;
using DeliciasDaNilda.Services;

namespace DeliciasDaNilda.Pages.Shared;

/// <summary>
/// Dados para a partial _PedidoCard (usada em Pedidos e na Agenda). O
/// pedido precisa vir com Cliente, Sabor e Pagamentos carregados.
/// </summary>
public sealed record PedidoCardModel(
    Pedido Pedido,
    IReadOnlyList<FaltaIngrediente>? Faltas,
    DateOnly Hoje,
    string Voltar,
    bool Aberto = false);
