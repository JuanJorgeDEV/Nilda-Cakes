namespace DeliciasDaNilda.Services.Common;

/// <summary>
/// Resultado padrão para operações de serviço que podem falhar por violação
/// de regra de negócio (não por exceção técnica). Usado por todos os services
/// de domínio para retornar sucesso/erro de forma explícita ao boundary
/// (PageModel), evitando exceptions para fluxo de validação esperado.
/// </summary>
public class ResultadoOperacao
{
    public bool Sucesso { get; }

    public string? Erro { get; }

    protected ResultadoOperacao(bool sucesso, string? erro)
    {
        Sucesso = sucesso;
        Erro = erro;
    }

    public static ResultadoOperacao Ok() => new(true, null);

    public static ResultadoOperacao Falha(string erro) => new(false, erro);
}

/// <summary>
/// Variante de <see cref="ResultadoOperacao"/> que carrega um valor de retorno
/// quando a operação é bem-sucedida (ex.: o Pedido recém-criado).
/// </summary>
public sealed class ResultadoOperacao<T> : ResultadoOperacao
{
    public T? Valor { get; }

    private ResultadoOperacao(bool sucesso, string? erro, T? valor) : base(sucesso, erro)
    {
        Valor = valor;
    }

    public static ResultadoOperacao<T> Ok(T valor) => new(true, null, valor);

    public static new ResultadoOperacao<T> Falha(string erro) => new(false, erro, default);
}
