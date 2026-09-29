using DeliciasDaNilda.Data;
using DeliciasDaNilda.Domain.Entities;
using DeliciasDaNilda.Domain.Enums;
using DeliciasDaNilda.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace DeliciasDaNilda.Services;

public interface IIngredienteService
{
    /// <summary>Lista ingredientes cadastrados, ordenados por nome.</summary>
    Task<List<Ingrediente>> ListarAsync(CancellationToken ct = default);

    /// <summary>Obtém um ingrediente pelo id, ou null se não encontrado.</summary>
    Task<Ingrediente?> ObterPorIdAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Cadastra um novo ingrediente (base para a ficha técnica — RN04).
    /// Valida nome único (não redundante com constraint do banco: aqui
    /// devolvemos um erro de negócio amigável antes de tentar persistir).
    /// Saldo inicial sempre 0 — entradas de estoque passam por
    /// IEstoqueService.RegistrarEntradaAsync (RN11).
    /// </summary>
    Task<ResultadoOperacao<Ingrediente>> CriarAsync(
        string nome,
        string unidadeMedida,
        decimal? estoqueMinimoAlerta = null,
        CancellationToken ct = default);

    /// <summary>Cadastro completo (com categoria e custo opcional).</summary>
    Task<ResultadoOperacao<Ingrediente>> CriarAsync(DadosIngrediente dados, CancellationToken ct = default);

    /// <summary>
    /// Edita nome, categoria, estoque mínimo e custo. A unidade de medida não
    /// muda: o saldo, os movimentos e as fichas técnicas estão nela.
    /// </summary>
    Task<ResultadoOperacao> AtualizarAsync(int id, DadosIngrediente dados, CancellationToken ct = default);
}

/// <summary>Dados editáveis de um ingrediente. A unidade só vale no cadastro.</summary>
public record DadosIngrediente(
    string Nome,
    string UnidadeMedida,
    CategoriaIngrediente Categoria,
    decimal? EstoqueMinimoAlerta = null,
    decimal? CustoPorUnidade = null);

/// <summary>
/// CRUD de Ingrediente (cadastro básico do insumo). Movimentações de saldo
/// (entrada, abate, ajuste — RN11/RN12/RN13) continuam exclusivas do
/// IEstoqueService.
/// </summary>
public class IngredienteService : IIngredienteService
{
    private readonly AppDbContext _db;

    public IngredienteService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<Ingrediente>> ListarAsync(CancellationToken ct = default)
    {
        return await _db.Ingredientes.OrderBy(i => i.Nome).ToListAsync(ct);
    }

    public async Task<Ingrediente?> ObterPorIdAsync(int id, CancellationToken ct = default)
    {
        return await _db.Ingredientes.FirstOrDefaultAsync(i => i.Id == id, ct);
    }

    public Task<ResultadoOperacao<Ingrediente>> CriarAsync(
        string nome,
        string unidadeMedida,
        decimal? estoqueMinimoAlerta = null,
        CancellationToken ct = default)
        => CriarAsync(new DadosIngrediente(nome, unidadeMedida, CategoriaIngrediente.Outros, estoqueMinimoAlerta), ct);

    public async Task<ResultadoOperacao<Ingrediente>> CriarAsync(DadosIngrediente dados, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dados.UnidadeMedida))
        {
            return ResultadoOperacao<Ingrediente>.Falha("Informe a unidade de medida do ingrediente.");
        }

        var erro = await ValidarAsync(dados, idAtual: null, ct);
        if (erro is not null)
        {
            return ResultadoOperacao<Ingrediente>.Falha(erro);
        }

        var ingrediente = new Ingrediente
        {
            Nome = dados.Nome.Trim(),
            UnidadeMedida = dados.UnidadeMedida.Trim(),
            Categoria = dados.Categoria,
            EstoqueMinimoAlerta = dados.EstoqueMinimoAlerta,
            CustoPorUnidade = dados.CustoPorUnidade,
            SaldoAtual = 0,
            CriadoEm = DateTime.UtcNow
        };

        _db.Ingredientes.Add(ingrediente);
        await _db.SaveChangesAsync(ct);

        return ResultadoOperacao<Ingrediente>.Ok(ingrediente);
    }

    public async Task<ResultadoOperacao> AtualizarAsync(int id, DadosIngrediente dados, CancellationToken ct = default)
    {
        var ingrediente = await _db.Ingredientes.FirstOrDefaultAsync(i => i.Id == id, ct);
        if (ingrediente is null)
        {
            return ResultadoOperacao.Falha("Ingrediente não encontrado.");
        }

        var erro = await ValidarAsync(dados, idAtual: id, ct);
        if (erro is not null)
        {
            return ResultadoOperacao.Falha(erro);
        }

        ingrediente.Nome = dados.Nome.Trim();
        ingrediente.Categoria = dados.Categoria;
        ingrediente.EstoqueMinimoAlerta = dados.EstoqueMinimoAlerta;
        ingrediente.CustoPorUnidade = dados.CustoPorUnidade;

        await _db.SaveChangesAsync(ct);
        return ResultadoOperacao.Ok();
    }

    private async Task<string?> ValidarAsync(DadosIngrediente dados, int? idAtual, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dados.Nome))
        {
            return "Informe o nome do ingrediente.";
        }

        if (dados.EstoqueMinimoAlerta < 0)
        {
            return "O estoque mínimo não pode ser negativo.";
        }

        if (dados.CustoPorUnidade < 0)
        {
            return "O custo não pode ser negativo.";
        }

        var nomeTratado = dados.Nome.Trim();
        var duplicado = await _db.Ingredientes.AnyAsync(i => i.Nome == nomeTratado && i.Id != (idAtual ?? 0), ct);
        return duplicado ? $"Já existe um ingrediente chamado '{nomeTratado}'." : null;
    }
}
