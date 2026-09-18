namespace DeliciasDaNilda.Domain.Entities;

/// <summary>
/// Configurações globais do sistema — tabela singleton (deve existir exatamente
/// uma linha). Não corresponde a uma RN numerada de docs/regras-de-negocio.md;
/// é uma decisão de produto para suportar o atendimento automático via bot de
/// WhatsApp, que a Nilda pode ligar/desligar pelo painel ADM.
/// </summary>
public class Configuracao
{
    public int Id { get; set; }

    /// <summary>Liga/desliga o atendimento automático de pedidos via WhatsApp (bot).</summary>
    public bool WhatsAppBotAtivo { get; set; }

    public DateTime AtualizadoEm { get; set; }
}
