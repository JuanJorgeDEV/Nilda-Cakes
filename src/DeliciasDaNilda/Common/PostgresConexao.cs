using Npgsql;

namespace DeliciasDaNilda.Common;

/// <summary>
/// Aceita a conexão do PostgreSQL (Supabase) tanto no formato de URI
/// (postgresql://usuario:senha@host:5432/postgres, como o Supabase entrega)
/// quanto no formato "Host=...;Username=...". Nuvem exige SSL.
/// </summary>
public static class PostgresConexao
{
    public static string Normalizar(string? bruta)
    {
        if (string.IsNullOrWhiteSpace(bruta))
        {
            throw new InvalidOperationException("ConnectionStrings:DefaultConnection não configurada.");
        }

        var texto = bruta.Trim();
        NpgsqlConnectionStringBuilder construtor;

        if (texto.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
            texto.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            var uri = new Uri(texto);
            var credenciais = uri.UserInfo.Split(':', 2);
            construtor = new NpgsqlConnectionStringBuilder
            {
                Host = uri.Host,
                Port = uri.Port > 0 ? uri.Port : 5432,
                Username = Uri.UnescapeDataString(credenciais[0]),
                Password = credenciais.Length > 1 ? Uri.UnescapeDataString(credenciais[1]) : null,
                Database = uri.AbsolutePath.Trim('/') is { Length: > 0 } banco ? banco : "postgres"
            };
        }
        else
        {
            construtor = new NpgsqlConnectionStringBuilder(texto);
        }

        var local = construtor.Host is "localhost" or "127.0.0.1" or "::1";
        if (!local && construtor.SslMode == SslMode.Disable)
        {
            construtor.SslMode = SslMode.Require;
        }
        // Pooler do Supabase (pgbouncer): sem prepared statements persistentes.
        construtor.MaxAutoPrepare = 0;
        return construtor.ConnectionString;
    }
}
