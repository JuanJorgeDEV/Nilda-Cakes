using DeliciasDaNilda.Domain.Enums;

namespace DeliciasDaNilda.Domain.Entities;

/// <summary>
/// Pedido de um cliente para um sabor/peso/data (RN03, RN05, RN06, RN07,
/// RN08, RN09, RN12, RN14/RN15, RN16).
/// </summary>
public class Pedido
{
    public int Id { get; set; }

    public int ClienteId { get; set; }

    public Cliente Cliente { get; set; } = null!;

    public int SaborId { get; set; }

    public Sabor Sabor { get; set; } = null!;

    /// <summary>
    /// Peso em kg. Entrada de cálculo (RN03) — sempre armazenado, nunca só o total.
    /// </summary>
    public decimal Peso { get; set; }

    /// <summary>
    /// Snapshot do Sabor.PrecoPorKg no momento do pedido — garante auditoria
    /// mesmo se o preço do sabor mudar depois (RN03).
    /// </summary>
    public decimal PrecoPorKgUtilizado { get; set; }

    /// <summary>
    /// Calculado = Peso x PrecoPorKgUtilizado; persistido (não computed column)
    /// para auditoria histórica robusta (RN03).
    /// </summary>
    public decimal ValorTotal { get; set; }

    /// <summary>Data de entrega/retirada (sem horário).</summary>
    public DateOnly DataEntrega { get; set; }

    /// <summary>
    /// Cliente escolhe apenas a data no pedido; horário é combinado depois via
    /// WhatsApp (RN08). Não aparece na tela de criação de pedido — só pode ser
    /// preenchido pelo ADM depois.
    /// </summary>
    public TimeOnly? HorarioRetirada { get; set; }

    public StatusPedido StatusPedido { get; set; } = StatusPedido.PendenteConfirmacao;

    /// <summary>
    /// Denormalizado a partir de Pagamentos, mantido para leitura rápida na
    /// agenda (RN16). Sincronizado pela mesma disciplina de transação do
    /// Ingrediente.SaldoAtual.
    /// </summary>
    public StatusPagamento StatusPagamento { get; set; } = StatusPagamento.SemPagamento;

    /// <summary>Preenchido quando StatusPedido vira Confirmado (RN08).</summary>
    public DateTime? ConfirmadoEm { get; set; }

    /// <summary>Auditoria de quem confirmou (RN08).</summary>
    public int? ConfirmadoPorAdministradorId { get; set; }

    public Administrador? ConfirmadoPorAdministrador { get; set; }

    public string? Observacoes { get; set; }

    /// <summary>Data de registro do pedido — base para checar antecedência mínima (RN05).</summary>
    public DateTime CriadoEm { get; set; }

    public ICollection<MovimentoEstoque> MovimentosEstoque { get; set; } = new List<MovimentoEstoque>();

    public ICollection<Pagamento> Pagamentos { get; set; } = new List<Pagamento>();
}
