using DeliciasDaNilda.Data;
using DeliciasDaNilda.Domain.Entities;
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
}

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

    public async Task<ResultadoOperacao<Ingrediente>> CriarAsync(
        string nome,
        string unidadeMedida,
        decimal? estoqueMinimoAlerta = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            return ResultadoOperacao<Ingrediente>.Falha("Informe o nome do ingrediente.");
        }

        if (string.IsNullOrWhiteSpace(unidadeMedida))
        {
            return ResultadoOperacao<Ingrediente>.Falha("Informe a unidade de medida do ingrediente.");
        }

        var nomeTratado = nome.Trim();

        var nomeDuplicado = await _db.Ingredientes.AnyAsync(i => i.Nome == nomeTratado, ct);
        if (nomeDuplicado)
        {
            return ResultadoOperacao<Ingrediente>.Falha($"Já existe um ingrediente chamado '{nomeTratado}'.");
        }

        var ingrediente = new Ingrediente
        {
            Nome = nomeTratado,
            UnidadeMedida = unidadeMedida.Trim(),
            EstoqueMinimoAlerta = estoqueMinimoAlerta,
            SaldoAtual = 0,
            CriadoEm = DateTime.UtcNow
        };

        _db.Ingredientes.Add(ingrediente);
        await _db.SaveChangesAsync(ct);

        return ResultadoOperacao<Ingrediente>.Ok(ingrediente);
    }
}
