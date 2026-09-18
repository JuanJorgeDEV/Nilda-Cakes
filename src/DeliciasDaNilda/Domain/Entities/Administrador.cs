namespace DeliciasDaNilda.Domain.Entities;

/// <summary>
/// Administrador do sistema, com credenciais próprias e acesso total (RN02).
/// Entidade isolada de Cliente propositalmente — não há "papel" compartilhado.
/// </summary>
public class Administrador
{
    public int Id { get; set; }

    public string NomeCompleto { get; set; } = string.Empty;

    /// <summary>Login (RN02).</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Hash de senha (ex.: ASP.NET Identity / BCrypt), nunca texto plano.</summary>
    public string SenhaHash { get; set; } = string.Empty;

    /// <summary>Permite desativar acesso sem apagar histórico de auditoria.</summary>
    public bool Ativo { get; set; } = true;

    public DateTime CriadoEm { get; set; }

    public ICollection<MovimentoEstoque> MovimentosEstoque { get; set; } = new List<MovimentoEstoque>();

    public ICollection<Pedido> PedidosConfirmados { get; set; } = new List<Pedido>();

    public ICollection<Pagamento> PagamentosRegistrados { get; set; } = new List<Pagamento>();
}
