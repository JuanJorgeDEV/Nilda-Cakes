using System.Diagnostics;

namespace DeliciasDaNilda.Services;

/// <summary>Situação da conexão do bot com o WhatsApp, como mostrada no painel.</summary>
public sealed record EstadoBot(string Estado, string? Qr, string? Numero, string? Detalhe, DateTime AtualizadoEm);

public interface IWhatsAppBotProcess
{
    EstadoBot Obter();

    /// <summary>Chamado pelo próprio bot (API interna) para informar QR code e conexão.</summary>
    void Atualizar(string estado, string? qr, string? numero);

    bool PastaDoBotExiste { get; }

    void Iniciar();

    void Parar();

    /// <summary>Para o bot e apaga a sessão salva, para parear outro número com um novo QR.</summary>
    void TrocarNumero();
}

/// <summary>
/// Sobe e acompanha o processo Node do bot (whatsapp-bot/index.js) junto com o
/// site, para não depender de um segundo terminal. O bot informa o QR code e o
/// estado da conexão pela API interna; o painel (aba WhatsApp) mostra isso.
/// Não corresponde a uma RN numerada — é infraestrutura operacional.
/// </summary>
public sealed class WhatsAppBotProcess : IWhatsAppBotProcess, IHostedService
{
    private readonly ILogger<WhatsAppBotProcess> _log;
    private readonly IConfiguration _config;
    private readonly string _pasta;
    private readonly object _trava = new();
    private Process? _processo;
    private EstadoBot _estado = new("parado", null, null, null, DateTime.UtcNow);

    public WhatsAppBotProcess(ILogger<WhatsAppBotProcess> log, IConfiguration config, IWebHostEnvironment env)
    {
        _log = log;
        _config = config;
        var configurada = config["WhatsAppBot:Pasta"];
        _pasta = Path.GetFullPath(string.IsNullOrWhiteSpace(configurada)
            ? Path.Combine(env.ContentRootPath, "..", "..", "whatsapp-bot")
            : configurada);
    }

    public bool PastaDoBotExiste => File.Exists(Path.Combine(_pasta, "index.js"));

    public EstadoBot Obter()
    {
        lock (_trava) return _estado;
    }

    public void Atualizar(string estado, string? qr, string? numero)
    {
        lock (_trava)
        {
            _estado = new EstadoBot(estado, estado == "aguardando_qr" ? qr : null, numero ?? _estado.Numero, null, DateTime.UtcNow);
        }
    }

    public void Iniciar()
    {
        lock (_trava)
        {
            if (_processo is { HasExited: false }) return;

            if (!PastaDoBotExiste)
            {
                _estado = new EstadoBot("erro", null, null, "Pasta whatsapp-bot não encontrada.", DateTime.UtcNow);
                return;
            }

            var info = new ProcessStartInfo("node", "index.js")
            {
                WorkingDirectory = _pasta,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            info.Environment["API_BASE_URL"] = _config["WhatsAppBot:ApiBaseUrl"] ?? "http://localhost:5138";
            info.Environment["API_KEY"] = _config["InternalApi:WhatsAppBotKey"] ?? string.Empty;
            info.Environment["BOT_ESTADO_NA_API"] = "1";

            try
            {
                var p = new Process { StartInfo = info, EnableRaisingEvents = true };
                p.OutputDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) _log.LogInformation("[bot] {Linha}", e.Data); };
                p.ErrorDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) _log.LogWarning("[bot] {Linha}", e.Data); };
                p.Exited += (_, _) =>
                {
                    lock (_trava)
                    {
                        if (!ReferenceEquals(_processo, p)) return;
                        var codigo = p.ExitCode;
                        _estado = new EstadoBot(codigo == 0 ? "parado" : "erro", null, null,
                            codigo == 0 ? null : $"O bot parou (código {codigo}). Veja o log do site.", DateTime.UtcNow);
                        _processo = null;
                    }
                };
                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                _processo = p;
                _estado = new EstadoBot("iniciando", null, null, null, DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Não foi possível iniciar o bot (o Node.js está instalado?)");
                _estado = new EstadoBot("erro", null, null, "Não consegui iniciar o Node.js. Ele está instalado?", DateTime.UtcNow);
            }
        }
    }

    public void Parar()
    {
        Process? p;
        lock (_trava)
        {
            p = _processo;
            _processo = null;
            _estado = new EstadoBot("parado", null, null, null, DateTime.UtcNow);
        }

        if (p is { HasExited: false })
        {
            try { p.Kill(entireProcessTree: true); } catch (Exception ex) { _log.LogWarning(ex, "Falha ao parar o bot"); }
            p.WaitForExit(3000);
        }
    }

    public void TrocarNumero()
    {
        Parar();
        var sessao = Path.Combine(_pasta, "auth_info");
        if (Directory.Exists(sessao))
        {
            Directory.Delete(sessao, recursive: true);
        }
        Iniciar();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Só reconecta sozinho se já existe sessão salva (sem QR). Sem sessão, o QR
        // só é gerado quando a Nilda clica em "Conectar" no painel.
        var temSessao = File.Exists(Path.Combine(_pasta, "auth_info", "creds.json"));
        if (_config.GetValue("WhatsAppBot:IniciarComOSite", true) && PastaDoBotExiste && temSessao)
        {
            Iniciar();
        }
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Parar();
        return Task.CompletedTask;
    }
}
