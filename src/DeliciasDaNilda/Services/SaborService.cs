using DeliciasDaNilda.Data;
using DeliciasDaNilda.Domain.Entities;
using DeliciasDaNilda.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace DeliciasDaNilda.Services;

/// <summary>Item de ficha técnica informado na criação do sabor (ingrediente + quantidade por kg).</summary>
public record ItemFichaTecnicaEntrada(int IngredienteId, decimal QuantidadePorKg);

/// <summary>Dados editáveis de um sabor (cadastro e edição).</summary>
public record DadosSabor(
    string Nome,
    decimal PrecoPorKg,
    bool Disponivel,
    IReadOnlyCollection<ItemFichaTecnicaEntrada> Itens,
    string? Descricao = null,
    decimal? CustoExtraPorKg = null);

public interface ISaborService
{
    /// <summary>
    /// Lista sabores cadastrados (nunca os excluídos), com a ficha técnica (e
    /// ingredientes) carregada. <paramref name="apenasDisponiveis"/> filtra por
    /// Sabor.Disponivel quando informado (ex.: tela de pedido do cliente).
    /// </summary>
    Task<List<Sabor>> ListarAsync(bool? apenasDisponiveis = null, CancellationToken ct = default);

    /// <summary>Obtém um sabor não excluído pelo id (com ficha técnica), ou null.</summary>
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

    /// <summary>Cria um sabor com todos os campos (RN03 preço base, RN04 ficha).</summary>
    Task<ResultadoOperacao<Sabor>> CriarAsync(DadosSabor dados, CancellationToken ct = default);

    /// <summary>
    /// Atualiza dados e ficha técnica (RN04). A ficha não é versionada: a
    /// nova composição vale para projeções e confirmações futuras; pedidos
    /// já confirmados mantêm o que foi abatido (RN12). O preço de pedidos já
    /// feitos não muda (snapshot em Pedido.PrecoPorKgUtilizado, RN03).
    /// </summary>
    Task<ResultadoOperacao<Sabor>> AtualizarAsync(int id, DadosSabor dados, CancellationToken ct = default);

    Task<ResultadoOperacao> DefinirImagemAsync(int id, string? imagemUrl, CancellationToken ct = default);

    Task<ResultadoOperacao> AlternarDisponibilidadeAsync(int id, CancellationToken ct = default);

    /// <summary>Exclusão lógica: some do cardápio, mas os pedidos antigos continuam ligados a ele.</summary>
    Task<ResultadoOperacao> ExcluirAsync(int id, CancellationToken ct = default);
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

    /// <summary>
    /// Custo estimado por kg: soma de QuantidadePorKg × custo do ingrediente
    /// (ingredientes sem custo contam como zero) + custo extra do sabor.
    /// Precisa da ficha técnica com os ingredientes carregados.
    /// </summary>
    public static decimal CalcularCustoPorKg(Sabor sabor)
    {
        var ingredientes = sabor.FichaTecnica?.Itens
            .Sum(i => i.QuantidadePorKg * (i.Ingrediente?.CustoPorUnidade ?? 0m)) ?? 0m;
        return ingredientes + (sabor.CustoExtraPorKg ?? 0m);
    }

    /// <summary>True se algum ingrediente da ficha ainda está sem custo cadastrado.</summary>
    public static bool CustoIncompleto(Sabor sabor)
        => sabor.FichaTecnica?.Itens.Any(i => i.Ingrediente?.CustoPorUnidade is null) ?? true;

    public async Task<List<Sabor>> ListarAsync(bool? apenasDisponiveis = null, CancellationToken ct = default)
    {
        var query = _db.Sabores
            .Where(s => s.ExcluidoEm == null)
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
            .FirstOrDefaultAsync(s => s.Id == id && s.ExcluidoEm == null, ct);
    }

    public Task<ResultadoOperacao<Sabor>> CriarComFichaTecnicaAsync(
        string nome,
        decimal precoPorKg,
        bool disponivel,
        IReadOnlyCollection<ItemFichaTecnicaEntrada> itens,
        CancellationToken ct = default)
        => CriarAsync(new DadosSabor(nome, precoPorKg, disponivel, itens), ct);

    public async Task<ResultadoOperacao<Sabor>> CriarAsync(DadosSabor dados, CancellationToken ct = default)
    {
        var erro = await ValidarAsync(dados, idAtual: null, ct);
        if (erro is not null)
        {
            return ResultadoOperacao<Sabor>.Falha(erro);
        }

        var agora = DateTime.UtcNow;

        var sabor = new Sabor
        {
            Nome = dados.Nome.Trim(),
            PrecoPorKg = dados.PrecoPorKg,
            Disponivel = dados.Disponivel,
            Descricao = Limpar(dados.Descricao),
            CustoExtraPorKg = dados.CustoExtraPorKg is > 0 ? dados.CustoExtraPorKg : null,
            CriadoEm = agora,
            FichaTecnica = new FichaTecnica
            {
                CriadoEm = agora,
                Itens = dados.Itens
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

    public async Task<ResultadoOperacao<Sabor>> AtualizarAsync(int id, DadosSabor dados, CancellationToken ct = default)
    {
        var sabor = await _db.Sabores
            .Include(s => s.FichaTecnica!)
                .ThenInclude(f => f.Itens)
            .FirstOrDefaultAsync(s => s.Id == id && s.ExcluidoEm == null, ct);

        if (sabor is null)
        {
            return ResultadoOperacao<Sabor>.Falha("Sabor não encontrado.");
        }

        var erro = await ValidarAsync(dados, idAtual: id, ct);
        if (erro is not null)
        {
            return ResultadoOperacao<Sabor>.Falha(erro);
        }

        var agora = DateTime.UtcNow;
        sabor.Nome = dados.Nome.Trim();
        sabor.PrecoPorKg = dados.PrecoPorKg;
        sabor.Disponivel = dados.Disponivel;
        sabor.Descricao = Limpar(dados.Descricao);
        sabor.CustoExtraPorKg = dados.CustoExtraPorKg is > 0 ? dados.CustoExtraPorKg : null;
        sabor.AtualizadoEm = agora;

        sabor.FichaTecnica ??= new FichaTecnica { CriadoEm = agora };
        var ficha = sabor.FichaTecnica;
        var novos = dados.Itens.ToDictionary(i => i.IngredienteId, i => i.QuantidadePorKg);

        // RN04: sincroniza os itens — remove os que saíram, atualiza e inclui.
        foreach (var item in ficha.Itens.Where(i => !novos.ContainsKey(i.IngredienteId)).ToList())
        {
            ficha.Itens.Remove(item);
            _db.FichaTecnicaItens.Remove(item);
        }

        foreach (var (ingredienteId, quantidade) in novos)
        {
            var existente = ficha.Itens.FirstOrDefault(i => i.IngredienteId == ingredienteId);
            if (existente is null)
            {
                ficha.Itens.Add(new FichaTecnicaItem { IngredienteId = ingredienteId, QuantidadePorKg = quantidade });
            }
            else
            {
                existente.QuantidadePorKg = quantidade;
            }
        }

        ficha.AtualizadoEm = agora;

        await _db.SaveChangesAsync(ct);
        return ResultadoOperacao<Sabor>.Ok(sabor);
    }

    public async Task<ResultadoOperacao> DefinirImagemAsync(int id, string? imagemUrl, CancellationToken ct = default)
    {
        var sabor = await _db.Sabores.FirstOrDefaultAsync(s => s.Id == id && s.ExcluidoEm == null, ct);
        if (sabor is null)
        {
            return ResultadoOperacao.Falha("Sabor não encontrado.");
        }

        sabor.ImagemUrl = imagemUrl;
        sabor.AtualizadoEm = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ResultadoOperacao.Ok();
    }

    public async Task<ResultadoOperacao> AlternarDisponibilidadeAsync(int id, CancellationToken ct = default)
    {
        var sabor = await _db.Sabores.FirstOrDefaultAsync(s => s.Id == id && s.ExcluidoEm == null, ct);
        if (sabor is null)
        {
            return ResultadoOperacao.Falha("Sabor não encontrado.");
        }

        sabor.Disponivel = !sabor.Disponivel;
        sabor.AtualizadoEm = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ResultadoOperacao.Ok();
    }

    public async Task<ResultadoOperacao> ExcluirAsync(int id, CancellationToken ct = default)
    {
        var sabor = await _db.Sabores.FirstOrDefaultAsync(s => s.Id == id && s.ExcluidoEm == null, ct);
        if (sabor is null)
        {
            return ResultadoOperacao.Falha("Sabor não encontrado.");
        }

        // Exclusão lógica: preserva o histórico de pedidos (FK Restrict).
        sabor.ExcluidoEm = DateTime.UtcNow;
        sabor.Disponivel = false;
        await _db.SaveChangesAsync(ct);
        return ResultadoOperacao.Ok();
    }

    private async Task<string?> ValidarAsync(DadosSabor dados, int? idAtual, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dados.Nome))
        {
            return "Informe o nome do sabor.";
        }

        if (dados.PrecoPorKg <= 0)
        {
            return "Preço por kg deve ser maior que zero (RN03).";
        }

        if (dados.CustoExtraPorKg < 0)
        {
            return "O custo extra não pode ser negativo.";
        }

        if (dados.Itens.Count == 0)
        {
            return "Informe ao menos um ingrediente na ficha técnica (RN04).";
        }

        // Bate com a constraint UX_FichaTecnicaItens_Ficha_Ingrediente: um
        // mesmo ingrediente não pode aparecer duas vezes na mesma ficha.
        if (dados.Itens.GroupBy(i => i.IngredienteId).Any(g => g.Count() > 1))
        {
            return "Um mesmo ingrediente não pode aparecer mais de uma vez na ficha técnica.";
        }

        if (dados.Itens.Any(i => i.QuantidadePorKg <= 0))
        {
            return "A quantidade por kg de cada ingrediente deve ser maior que zero.";
        }

        var nomeTratado = dados.Nome.Trim();
        var nomeDuplicado = await _db.Sabores.AnyAsync(
            s => s.Nome == nomeTratado && s.ExcluidoEm == null && s.Id != (idAtual ?? 0), ct);
        if (nomeDuplicado)
        {
            return $"Já existe um sabor chamado '{nomeTratado}'.";
        }

        var idsIngredientes = dados.Itens.Select(i => i.IngredienteId).Distinct().ToList();
        var existentes = await _db.Ingredientes.CountAsync(i => idsIngredientes.Contains(i.Id), ct);
        if (existentes != idsIngredientes.Count)
        {
            return "Um ou mais ingredientes selecionados não existem.";
        }

        return null;
    }

    private static string? Limpar(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
}
