namespace DeliciasDaNilda.Endpoints;

/// <summary>
/// Middleware simples de autenticação para os endpoints internos do bot de
/// WhatsApp (não é RN numerada — é infraestrutura de integração). Exige que
/// a requisição traga o header "X-Internal-Api-Key" com o valor configurado
/// em "InternalApi:WhatsAppBotKey" (appsettings.json/appsettings.Development.json
/// ou variável de ambiente/user-secrets equivalente). Sem o header correto,
/// responde 401 e não segue para o endpoint.
///
/// Aplicado apenas ao grupo "/internal/whatsapp-bot" (ver Program.cs) — não
/// afeta Razor Pages nem a autenticação por cookie do ADM.
/// </summary>
public class InternalApiKeyMiddleware
{
    public const string HeaderName = "X-Internal-Api-Key";

    private readonly RequestDelegate _next;
    private readonly IConfiguration _configuration;

    public InternalApiKeyMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        _configuration = configuration;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var chaveEsperada = _configuration["InternalApi:WhatsAppBotKey"];

        if (string.IsNullOrWhiteSpace(chaveEsperada))
        {
            // Sem chave configurada, não há como validar — nega por padrão
            // (fail-closed) em vez de deixar o endpoint aberto por engano.
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var chaveRecebida = context.Request.Headers[HeaderName].ToString();

        if (!string.Equals(chaveRecebida, chaveEsperada, StringComparison.Ordinal))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        await _next(context);
    }
}
