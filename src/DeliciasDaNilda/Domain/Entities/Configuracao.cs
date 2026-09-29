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

    /// <summary>
    /// Número de WhatsApp da loja (só dígitos, com DDI) para onde o site
    /// manda o cliente depois de fazer o pedido (link wa.me).
    /// </summary>
    public string? WhatsAppLoja { get; set; }

    public DateTime AtualizadoEm { get; set; }
}
