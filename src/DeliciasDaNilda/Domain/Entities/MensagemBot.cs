namespace DeliciasDaNilda.Domain.Entities;

/// <summary>
/// Texto personalizado de uma mensagem do bot de WhatsApp. Guarda apenas
/// OVERRIDES: se não existe linha para a chave, vale o texto padrão definido
/// em Services/MensagensBotPadrao.cs. Não corresponde a uma RN numerada.
/// </summary>
public class MensagemBot
{
    public int Id { get; set; }

    /// <summary>Identificador estável da mensagem (ver MensagensBotPadrao).</summary>
    public string Chave { get; set; } = string.Empty;

    public string Texto { get; set; } = string.Empty;

    public DateTime AtualizadoEm { get; set; }
}
