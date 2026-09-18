using DeliciasDaNilda.Data;
using DeliciasDaNilda.Domain.Entities;
using DeliciasDaNilda.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace DeliciasDaNilda.Services;

/// <summary>Item de ficha técnica informado na criação do sabor (ingrediente + quantidade por kg).</summary>
public record ItemFichaTecnicaEntrada(int IngredienteId, decimal QuantidadePorKg);

public interface ISaborService
{
    /// <summary>
    /// Lista sabores cadastrados, com a ficha técnica (e ingredientes)
    /// carregada. <paramref name="apenasDisponiveis"/> filtra por
    /// Sabor.Disponivel quando informado (ex.: tela de pedido do cliente).
    /// </summary>
    Task<List<Sabor>> ListarAsync(bool? apenasDisponiveis = null, CancellationToken ct = default);

    /// <summary>Obtém um sabor pelo id (com ficha técnica), ou null se não encontrado.</summary>
    Task<Sabor?> ObterPorIdAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Cria um Sabor e sua Ficha Técnica (RN04) numa única operação
    /// transacional. Valida nome de sabor único e ausência de ingrediente
    /// repetido na lista de itens (constraint
    /// UX_FichaTecnicaItens_Ficha_Ingrediente).
    /// </summary>
    Task<ResultadoOperacao<Sabor>> CriarComFichaTecnicaAsync(
        string nome,
        decimal precoPorKg,
        bool disponivel,
        IReadOnlyCollection<ItemFichaTecnicaEntrada> itens,
        CancellationToken ct = default);
}

/// <summary>
/// Cadastro de Sabor + Ficha Técnica (RN03 base de preço, RN04 ficha técnica).
/// </summary>
public class SaborService : ISaborService
{
    private readonly AppDbContext _db;

    public SaborService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<Sabor>> ListarAsync(bool? apenasDisponiveis = null, CancellationToken ct = default)
    {
        var query = _db.Sabores
            .Include(s => s.FichaTecnica!)
                .ThenInclude(f => f.Itens)
                    .ThenInclude(i => i.Ingrediente)
            .AsQueryable();

        if (apenasDisponiveis.HasValue)
        {
            query = query.Where(s => s.Disponivel == apenasDisponiveis.Value);
        }

        return await query.OrderBy(s => s.Nome).ToListAsync(ct);
    }

    public async Task<Sabor?> ObterPorIdAsync(int id, CancellationToken ct = default)
    {
        return await _db.Sabores
            .Include(s => s.FichaTecnica!)
                .ThenInclude(f => f.Itens)
                    .ThenInclude(i => i.Ingrediente)
            .FirstOrDefaultAsync(s => s.Id == id, ct);
    }

    public async Task<ResultadoOperacao<Sabor>> CriarComFichaTecnicaAsync(
        string nome,
        decimal precoPorKg,
        bool disponivel,
        IReadOnlyCollection<ItemFichaTecnicaEntrada> itens,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            return ResultadoOperacao<Sabor>.Falha("Informe o nome do sabor.");
        }

        if (precoPorKg <= 0)
        {
            return ResultadoOperacao<Sabor>.Falha("Preço por kg deve ser maior que zero (RN03).");
        }

        if (itens.Count == 0)
        {
            return ResultadoOperacao<Sabor>.Falha("Informe ao menos um ingrediente na ficha técnica (RN04).");
        }

        // Bate com a constraint UX_FichaTecnicaItens_Ficha_Ingrediente: um
        // mesmo ingrediente não pode aparecer duas vezes na mesma ficha.
        var ingredientesRepetidos = itens
            .GroupBy(i => i.IngredienteId)
            .Any(g => g.Count() > 1);
        if (ingredientesRepetidos)
        {
            return ResultadoOperacao<Sabor>.Falha("Um mesmo ingrediente não pode aparecer mais de uma vez na ficha técnica.");
        }

        if (itens.Any(i => i.QuantidadePorKg <= 0))
        {
            return ResultadoOperacao<Sabor>.Falha("A quantidade por kg de cada ingrediente deve ser maior que zero.");
        }

        var nomeTratado = nome.Trim();

        var nomeDuplicado = await _db.Sabores.AnyAsync(s => s.Nome == nomeTratado, ct);
        if (nomeDuplicado)
        {
            return ResultadoOperacao<Sabor>.Falha($"Já existe um sabor chamado '{nomeTratado}'.");
        }

        var idsIngredientes = itens.Select(i => i.IngredienteId).ToList();
        var quantidadeIngredientesExistentes = await _db.Ingredientes
            .CountAsync(i => idsIngredientes.Contains(i.Id), ct);
        if (quantidadeIngredientesExistentes != idsIngredientes.Distinct().Count())
        {
            return ResultadoOperacao<Sabor>.Falha("Um ou mais ingredientes selecionados não existem.");
        }

        var agora = DateTime.UtcNow;

        var sabor = new Sabor
        {
            Nome = nomeTratado,
            PrecoPorKg = precoPorKg,
            Disponivel = disponivel,
            CriadoEm = agora,
            FichaTecnica = new FichaTecnica
            {
                CriadoEm = agora,
                Itens = itens
                    .Select(i => new FichaTecnicaItem
                    {
                        IngredienteId = i.IngredienteId,
                        QuantidadePorKg = i.QuantidadePorKg
                    })
                    .ToList()
            }
        };

        // Sabor + FichaTecnica + Itens são persistidos em um único
        // SaveChangesAsync (unidade de trabalho do EF Core), garantindo
        // atomicidade sem depender de transação explícita do provedor de
        // banco (o InMemory usado em testes não suporta transações).
        _db.Sabores.Add(sabor);
        await _db.SaveChangesAsync(ct);

        return ResultadoOperacao<Sabor>.Ok(sabor);
    }
}
