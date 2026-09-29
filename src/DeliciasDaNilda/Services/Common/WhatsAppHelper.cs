namespace DeliciasDaNilda.Services.Common;

/// <summary>
/// Normalização compartilhada do número de WhatsApp (RN01 — chave única de
/// identificação do cliente). Site e bot precisam gravar o MESMO formato para
/// o bot reconhecer quem se cadastrou pelo site: só dígitos, sempre com o DDI
/// 55 (o bot recebe o número já com 55; no site a pessoa digita só DDD +
/// número).
/// </summary>
public static class WhatsAppHelper
{
    public static string Normalizar(string whatsApp)
    {
        var digitos = new string((whatsApp ?? string.Empty).Where(char.IsDigit).ToArray());

        // Número brasileiro sem DDI: DDD (2) + 8 ou 9 dígitos.
        if (digitos.Length is 10 or 11)
        {
            digitos = "55" + digitos;
        }

        return digitos;
    }

    /// <summary>
    /// Formas equivalentes do mesmo número para busca. O WhatsApp às vezes
    /// identifica celulares brasileiros antigos SEM o nono dígito
    /// (55 16 9999-0001 em vez de 55 16 9 9999-0001); as duas formas são
    /// tratadas como o mesmo cliente.
    /// </summary>
    public static IReadOnlyList<string> Variantes(string whatsApp)
    {
        var numero = Normalizar(whatsApp);
        var variantes = new List<string> { numero };

        if (numero.StartsWith("55"))
        {
            if (numero.Length == 13 && numero[4] == '9')
            {
                variantes.Add(numero[..4] + numero[5..]);
            }
            else if (numero.Length == 12)
            {
                variantes.Add(numero[..4] + "9" + numero[4..]);
            }
        }

        return variantes;
    }

    /// <summary>Exibição amigável: +55 (16) 99999-0001.</summary>
    public static string Formatar(string whatsApp)
    {
        var n = Normalizar(whatsApp);
        return n.Length switch
        {
            13 when n.StartsWith("55") => $"({n[2..4]}) {n[4..9]}-{n[9..]}",
            12 when n.StartsWith("55") => $"({n[2..4]}) {n[4..8]}-{n[8..]}",
            _ => whatsApp
        };
    }
}
