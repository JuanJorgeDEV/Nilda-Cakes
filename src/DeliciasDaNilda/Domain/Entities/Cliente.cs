namespace DeliciasDaNilda.Domain.Entities;

/// <summary>
/// Cliente da confeitaria. Autenticação simplificada por nome + WhatsApp (RN01).
/// </summary>
public class Cliente
{
    public int Id { get; set; }

    public string NomeCompleto { get; set; } = string.Empty;

    /// <summary>
    /// Chave única de identificação do cliente (RN01).
    /// Armazenado normalizado (somente dígitos, com DDI/DDD).
    /// </summary>
    public string WhatsApp { get; set; } = string.Empty;

    public DateTime CriadoEm { get; set; }

    public ICollection<Pedido> Pedidos { get; set; } = new List<Pedido>();
}
