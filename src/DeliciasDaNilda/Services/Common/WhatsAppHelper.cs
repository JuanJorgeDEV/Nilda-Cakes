namespace DeliciasDaNilda.Services.Common;

/// <summary>
/// Normalização compartilhada do número de WhatsApp (RN01 — chave única de
/// identificação do cliente). Mantém só os dígitos, descartando espaços,
/// parênteses, "+", "-", etc. Usado tanto pelo boundary Razor Pages
/// (Pages/Cliente/Identificacao.cshtml.cs) quanto pelo endpoint interno do
/// bot de WhatsApp, para garantir que o mesmo número seja sempre reconhecido
/// como o mesmo cliente independentemente de como foi digitado/enviado.
/// </summary>
public static class WhatsAppHelper
{
    public static string Normalizar(string whatsApp)
        => new string((whatsApp ?? string.Empty).Where(char.IsDigit).ToArray());
}
