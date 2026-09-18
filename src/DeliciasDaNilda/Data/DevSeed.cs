using DeliciasDaNilda.Domain.Enums;
using DeliciasDaNilda.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DeliciasDaNilda.Data;

/// <summary>Resumo do que o seed de desenvolvimento criou (usado na saída do comando e nos testes).</summary>
public sealed class DevSeedResultado
{
    public bool Executado { get; set; }
    public string? MotivoAborto { get; set; }
    public int Sabores { get; set; }
    public int Ingredientes { get; set; }
    public int Clientes { get; set; }
    public int Pedidos { get; set; }
    public DateOnly? DiaLotado { get; set; }
    public List<string> Cenarios { get; } = new();
    public List<string> Avisos { get; } = new();
}

/// <summary>
/// Seed de DADOS DE TESTE para exercitar o sistema no navegador.
///
/// Uso: <c>dotnet run -- --seed-dev [--reset]</c> (ver Program.cs). Só roda em
/// Development e nunca automaticamente.
///
/// Todo dado com regra de negócio é criado pelos SERVICES (nunca INSERT direto
/// no DbContext), para que o seed também valide os services e mantenha saldo e
/// movimentos de estoque consistentes:
///   - Ingredientes: IIngredienteService (saldo inicial 0)
///   - Sabores + ficha técnica: ISaborService (RN03/RN04)
///   - Estoque inicial e perda: IEstoqueService (RN11/RN13)
///   - Clientes: IClienteService (RN01)
///   - Pedidos: IPedidoService (RN03, RN05-RN08, RN12) — as datas respeitam a
///     antecedência mínima de 3 dias úteis (RN05) e o teto de 3 bolos/dia
///     (RN06/RN07); a regra NUNCA é contornada.
///   - Pagamentos: IPagamentoService (RN09/RN10)
///
/// ATENÇÃO: as FICHAS TÉCNICAS abaixo são FICTÍCIAS (quantidades inventadas
/// apenas plausíveis). Precisam ser substituídas pelas fichas reais da Nilda
/// (docs/cardapio-precos.md, seção "Observações para o cadastro (RN04)").
/// Os preços por kg, esses sim, são os reais do cardápio.
/// </summary>
public static class DevSeed
{
    // Ingredientes fictícios: (nome, unidade, estoque inicial, estoque mínimo de alerta).
    // Morango começa propositalmente escasso (cenário de reposição, RN15).
    private static readonly (string Nome, string Un, decimal Estoque, decimal? Minimo)[] IngredientesSeed =
    {
        ("Farinha de trigo", "kg", 30m, 5m),
        ("Ovos", "un", 300m, 60m),
        ("Açúcar", "kg", 20m, 3m),
        ("Manteiga", "kg", 8m, 1m),
        ("Leite integral", "L", 20m, 3m),
        ("Leite em pó", "kg", 10m, 2m),
        ("Leite condensado", "kg", 25m, 4m),
        ("Creme de leite", "kg", 30m, 5m),
        ("Doce de leite", "kg", 15m, 2m),
        ("Chocolate ao leite", "kg", 12m, 2m),
        ("Chocolate meio amargo", "kg", 15m, 2m),
        ("Chocolate branco", "kg", 6m, 1m),
        ("Chocolate Alpino", "kg", 8m, 1m),
        ("Cacau em pó", "kg", 3m, 0.5m),
        ("Cream cheese", "kg", 6m, 1m),
        ("Coco ralado", "kg", 4m, 0.5m),
        ("Polpa de maracujá", "kg", 6m, 1m),
        ("Morango", "kg", 2m, 1m),          // ESCASSO de propósito (cenário RN14/RN15)
        ("Abacaxi", "kg", 6m, 1m),
        ("Paçoca", "kg", 4m, 0.5m),
        ("Nozes", "kg", 4m, 0.5m),
        ("Ameixa seca", "kg", 3m, 0.5m),
        ("Biscoito Oreo", "kg", 5m, 1m),
    };

    // Base de massa comum a todos os sabores (FICTÍCIA), quantidade por 1 kg de bolo.
    private static readonly (string Ing, decimal Qtd)[] BaseMassa =
    {
        ("Farinha de trigo", 0.12m),
        ("Ovos", 2m),
        ("Açúcar", 0.10m),
        ("Manteiga", 0.04m),
    };

    // Sabores REAIS do cardápio (docs/cardapio-precos.md) com preço/kg exato;
    // recheio/cobertura por kg FICTÍCIO (somado à BaseMassa).
    private static readonly (string Nome, decimal Preco, (string Ing, decimal Qtd)[] Itens)[] SaboresSeed =
    {
        // R$ 70,00/kg
        ("Mousse de doce de leite trufado", 70m, new[] { ("Doce de leite", 0.30m), ("Creme de leite", 0.20m), ("Chocolate meio amargo", 0.10m) }),
        ("Mousse de chocolate", 70m, new[] { ("Chocolate meio amargo", 0.25m), ("Creme de leite", 0.25m), ("Leite condensado", 0.10m) }),
        ("Mousse de maracujá trufado", 70m, new[] { ("Polpa de maracujá", 0.15m), ("Creme de leite", 0.20m), ("Leite condensado", 0.20m), ("Chocolate meio amargo", 0.08m) }),
        ("Sensação", 70m, new[] { ("Morango", 0.25m), ("Chocolate ao leite", 0.15m), ("Creme de leite", 0.20m), ("Leite condensado", 0.15m) }),
        ("Prestígio tradicional", 70m, new[] { ("Coco ralado", 0.10m), ("Leite condensado", 0.25m), ("Chocolate ao leite", 0.12m), ("Creme de leite", 0.10m) }),
        ("Ninho com abacaxi", 70m, new[] { ("Leite em pó", 0.15m), ("Abacaxi", 0.20m), ("Leite condensado", 0.20m), ("Creme de leite", 0.15m) }),
        ("Ninho com paçoquinha", 70m, new[] { ("Leite em pó", 0.15m), ("Paçoca", 0.12m), ("Leite condensado", 0.20m), ("Creme de leite", 0.15m) }),
        ("Oreo", 70m, new[] { ("Biscoito Oreo", 0.20m), ("Creme de leite", 0.25m), ("Leite condensado", 0.15m) }),
        // R$ 75,00/kg
        ("Sensação com Brigadeiro", 75m, new[] { ("Morango", 0.25m), ("Chocolate ao leite", 0.15m), ("Creme de leite", 0.20m), ("Leite condensado", 0.35m), ("Cacau em pó", 0.03m) }),
        ("Prestígio Trufado", 75m, new[] { ("Coco ralado", 0.10m), ("Leite condensado", 0.20m), ("Chocolate meio amargo", 0.15m), ("Creme de leite", 0.10m) }),
        ("Ninho com morango", 75m, new[] { ("Leite em pó", 0.15m), ("Morango", 0.25m), ("Leite condensado", 0.20m), ("Creme de leite", 0.15m) }),
        ("Ninho c/ Nozes", 75m, new[] { ("Leite em pó", 0.15m), ("Nozes", 0.08m), ("Leite condensado", 0.20m), ("Creme de leite", 0.15m) }),
        ("Doce de leite com ameixas", 75m, new[] { ("Doce de leite", 0.30m), ("Ameixa seca", 0.10m), ("Creme de leite", 0.15m) }),
        ("Dois amores", 75m, new[] { ("Chocolate ao leite", 0.12m), ("Chocolate branco", 0.12m), ("Creme de leite", 0.20m), ("Leite condensado", 0.15m) }),
        ("Ninho trufado", 75m, new[] { ("Leite em pó", 0.15m), ("Leite condensado", 0.20m), ("Chocolate meio amargo", 0.10m), ("Creme de leite", 0.15m) }),
        // Preço individual
        ("Doces de leite com Nozes", 80m, new[] { ("Doce de leite", 0.30m), ("Nozes", 0.08m), ("Creme de leite", 0.15m) }),
        ("Ganache", 85m, new[] { ("Chocolate meio amargo", 0.35m), ("Creme de leite", 0.30m) }),
        ("Alpino", 85m, new[] { ("Chocolate Alpino", 0.25m), ("Creme de leite", 0.25m), ("Leite condensado", 0.15m) }),
        ("Red Velvet com 4 leites, sem frutas", 75m, new[] { ("Cacau em pó", 0.02m), ("Cream cheese", 0.20m), ("Leite condensado", 0.15m), ("Creme de leite", 0.15m), ("Leite integral", 0.10m) }),
        ("Red Velvet com 4 leites, com frutas em cima", 90m, new[] { ("Cacau em pó", 0.02m), ("Cream cheese", 0.20m), ("Leite condensado", 0.15m), ("Creme de leite", 0.15m), ("Leite integral", 0.10m), ("Morango", 0.15m) }),
    };

    private static readonly (string Nome, string WhatsApp)[] ClientesSeed =
    {
        ("Ana Teste Fictícia", "5516999990001"),
        ("Bruno Teste Fictício", "5516999990002"),
        ("Carla Teste Fictícia", "5516999990003"),
        ("Diego Teste Fictício", "5516999990004"),
        ("Elisa Teste Fictícia", "5516999990005"),
        ("Fábio Teste Fictício", "5516999990006"),
    };

    /// <summary>
    /// Executa o seed. Recusa fora de Development (inclusive --reset). Aborta se
    /// já houver sabores cadastrados, a menos que <paramref name="reset"/> seja true.
    /// </summary>
    public static async Task<DevSeedResultado> ExecutarAsync(
        IServiceProvider services, IHostEnvironment env, bool reset, CancellationToken ct = default)
    {
        var resultado = new DevSeedResultado();

        if (!env.IsDevelopment())
        {
            resultado.MotivoAborto = $"O seed de desenvolvimento só pode rodar no ambiente Development (atual: {env.EnvironmentName}). Nada foi alterado.";
            return resultado;
        }

        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();

        var admin = await db.Administradores.Where(a => a.Ativo).OrderBy(a => a.Id).FirstOrDefaultAsync(ct);
        if (admin is null)
        {
            resultado.MotivoAborto = "Nenhum administrador ativo encontrado (o admin seed é criado no start da aplicação via AdminSeed:*).";
            return resultado;
        }

        if (reset)
        {
            await ResetarDadosDeNegocioAsync(db, ct);
        }
        else if (await db.Sabores.AnyAsync(ct))
        {
            resultado.MotivoAborto = "Já existem sabores cadastrados: o seed é para banco VAZIO. Use '--seed-dev --reset' para apagar os dados de negócio (pedidos, pagamentos, movimentos, fichas, sabores, ingredientes, clientes; administradores e Configuracoes são preservados) e recriar.";
            return resultado;
        }

        var ingredienteSvc = sp.GetRequiredService<IIngredienteService>();
        var saborSvc = sp.GetRequiredService<ISaborService>();
        var estoqueSvc = sp.GetRequiredService<IEstoqueService>();
        var clienteSvc = sp.GetRequiredService<IClienteService>();
        var pedidoSvc = sp.GetRequiredService<IPedidoService>();
        var pagamentoSvc = sp.GetRequiredService<IPagamentoService>();
        var agendamentoSvc = sp.GetRequiredService<IAgendamentoService>();

        // ---- Ingredientes + estoque inicial (RN11) ----
        var ingredienteIds = new Dictionary<string, int>();
        foreach (var (nome, un, estoque, minimo) in IngredientesSeed)
        {
            var ing = Exigir(await ingredienteSvc.CriarAsync(nome, un, minimo, ct), $"ingrediente {nome}");
            ingredienteIds[nome] = ing.Id;
            ExigirOk(await estoqueSvc.RegistrarEntradaAsync(ing.Id, estoque, admin.Id, "Estoque inicial (seed de teste)", ct), $"entrada {nome}");
        }

        // ---- Sabores + fichas técnicas FICTÍCIAS (RN04) ----
        var saborIds = new Dictionary<string, int>();
        foreach (var (nome, preco, itens) in SaboresSeed)
        {
            var merged = new Dictionary<string, decimal>();
            foreach (var (ing, qtd) in BaseMassa.Concat(itens))
            {
                merged[ing] = merged.GetValueOrDefault(ing) + qtd;
            }
            var entrada = merged.Select(kv => new ItemFichaTecnicaEntrada(ingredienteIds[kv.Key], kv.Value)).ToList();
            var sabor = Exigir(await saborSvc.CriarComFichaTecnicaAsync(nome, preco, true, entrada, ct), $"sabor {nome}");
            saborIds[nome] = sabor.Id;
        }

        // ---- Clientes (RN01) ----
        var clienteIds = new List<int>();
        foreach (var (nome, zap) in ClientesSeed)
        {
            clienteIds.Add((await clienteSvc.ObterOuCriarAsync(nome, zap, ct)).Id);
        }

        // ---- Datas: 1o dia permitido (RN05) e dias úteis seguintes ----
        var minima = agendamentoSvc.CalcularDataMinimaPermitida(DateTime.UtcNow);
        var diasUteis = new List<DateOnly>();
        for (var d = minima; diasUteis.Count < 8; d = d.AddDays(1))
        {
            if (d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
            {
                diasUteis.Add(d);
            }
        }
        // Obs.: feriados não são tratados pelo AgendamentoService (limitação conhecida da RN05).

        async Task<int> Criar(int cliente, string sabor, decimal kg, DateOnly data, string? obs = null)
        {
            var p = Exigir(await pedidoSvc.CriarPedidoAsync(clienteIds[cliente], saborIds[sabor], kg, data, obs, ct), $"pedido {sabor} {data}");
            resultado.Pedidos++;
            return p.Id;
        }
        async Task Confirmar(int pedidoId) => ExigirOk(await pedidoSvc.ConfirmarPedidoAsync(pedidoId, admin.Id, ct), $"confirmar #{pedidoId}");

        // ---- Pedidos ----
        // (e) Dia lotado: 3 pedidos ativos (2 confirmados + 1 pendente) => RN06/RN07 bloqueiam a data.
        var lotado = diasUteis[1];
        var l1 = await Criar(0, "Ganache", 2m, lotado, "Aniversário — retirar à tarde");
        var l2 = await Criar(1, "Oreo", 1.5m, lotado);
        var l3 = await Criar(2, "Mousse de chocolate", 3m, lotado);
        await Confirmar(l1);
        await Confirmar(l2);
        resultado.DiaLotado = lotado;

        // (f) Estoque escasso de morango (2 kg): 3 pedidos confirmados o consomem (abatido na confirmação, RN12).
        var m1 = await Criar(3, "Ninho com morango", 2m, diasUteis[0]);
        var m2 = await Criar(4, "Sensação", 2m, diasUteis[2]);
        var m3 = await Criar(5, "Red Velvet com 4 leites, com frutas em cima", 3m, diasUteis[2]);
        await Confirmar(m1);
        await Confirmar(m2);
        await Confirmar(m3);

        // (c) Cancelado que estava Confirmado (gera AjusteCorrecao de estorno).
        var c1 = await Criar(1, "Alpino", 2m, diasUteis[2]);
        await Confirmar(c1);
        ExigirOk(await pedidoSvc.CancelarPedidoAsync(c1, ct), $"cancelar #{c1}");

        // (a) Pendentes de confirmação.
        var p1 = await Criar(0, "Prestígio tradicional", 2m, diasUteis[0]);
        var p2 = await Criar(3, "Ninho com paçoquinha", 1.5m, diasUteis[3]);
        var p3 = await Criar(4, "Doce de leite com ameixas", 2.5m, diasUteis[5]);
        // Pendente que estoura o Morango: saldo após confirmados e perda = 0,35 kg; este pedido
        // consome 2 kg x 0,25 = 0,5 kg => faltam 0,15 kg (alerta RN15 em /Admin/Estoque/Alertas).
        var p4 = await Criar(5, "Ninho com morango", 2m, diasUteis[4]);

        // (g) Pagamentos: sinal de 50% no pedido do dia lotado (Ganache) e integral na Sensação.
        var ganache = await db.Pedidos.AsNoTracking().FirstAsync(p => p.Id == l1, ct);
        ExigirOk(await pagamentoSvc.RegistrarPagamentoAsync(l1, TipoPagamento.Sinal, Math.Round(ganache.ValorTotal / 2, 2), admin.Id, "Pix", ct), "sinal");
        var sensacao = await db.Pedidos.AsNoTracking().FirstAsync(p => p.Id == m2, ct);
        ExigirOk(await pagamentoSvc.RegistrarPagamentoAsync(m2, TipoPagamento.Integral, sensacao.ValorTotal, admin.Id, "Dinheiro", ct), "integral");

        // (RN13) Perda de morango depois das confirmações.
        ExigirOk(await estoqueSvc.RegistrarAjustePerdaAsync(ingredienteIds["Morango"], 0.2m, admin.Id, "Morangos estragados (seed de teste)", ct), "perda morango");

        // (d) Concluído: IPedidoService não tem método para concluir => lacuna, não forçado via UPDATE.

        resultado.Sabores = SaboresSeed.Length;
        resultado.Ingredientes = IngredientesSeed.Length;
        resultado.Clientes = ClientesSeed.Length;
        resultado.Executado = true;

        resultado.Cenarios.Add($"Dia lotado (3 pedidos ativos, data bloqueada no calendário): {lotado:yyyy-MM-dd} — pedidos #{l1} (Confirmado, sinal 50%), #{l2} (Confirmado), #{l3} (Pendente)");
        resultado.Cenarios.Add($"Pendentes de Confirmação: #{l3}, #{p1} ({diasUteis[0]:yyyy-MM-dd}), #{p2} ({diasUteis[3]:yyyy-MM-dd}), #{p3} ({diasUteis[5]:yyyy-MM-dd}), #{p4} ({diasUteis[4]:yyyy-MM-dd})");
        resultado.Cenarios.Add($"Confirmados com abate de estoque: #{l1}, #{l2}, #{m1} ({diasUteis[0]:yyyy-MM-dd}), #{m2} e #{m3} ({diasUteis[2]:yyyy-MM-dd})");
        resultado.Cenarios.Add($"Cancelado que estava Confirmado (estorno de estoque): #{c1} ({diasUteis[2]:yyyy-MM-dd})");
        resultado.Cenarios.Add($"Pagamento parcial (sinal): #{l1}; pagamento integral: #{m2}");
        resultado.Cenarios.Add($"Estoque escasso: Morango (2 kg inicial; confirmados consomem 1,45 kg + perda 0,2 kg => saldo 0,35 kg). A projeção RN14/RN15 roda sobre os PENDENTES: o pedido #{p4} (Ninho com morango, 2 kg, {diasUteis[4]:yyyy-MM-dd}) precisa de 0,5 kg e gera o alerta (faltam 0,15 kg) => /Admin/Estoque/Alertas");
        resultado.Avisos.Add("Fichas técnicas e ingredientes são FICTÍCIOS: substituir pelas fichas reais da Nilda (RN04).");
        resultado.Avisos.Add("Cenário 'Concluído' NÃO criado: IPedidoService não possui método para concluir pedido (lacuna).");
        return resultado;

        static T Exigir<T>(Services.Common.ResultadoOperacao<T> r, string contexto)
        {
            if (!r.Sucesso) throw new InvalidOperationException($"Seed falhou em {contexto}: {r.Erro}");
            return r.Valor!;
        }
        static void ExigirOk(Services.Common.ResultadoOperacao r, string contexto)
        {
            if (!r.Sucesso) throw new InvalidOperationException($"Seed falhou em {contexto}: {r.Erro}");
        }
    }

    /// <summary>Apaga dados de negócio (NÃO administradores nem Configuracoes), em ordem que respeita as FKs.</summary>
    private static async Task ResetarDadosDeNegocioAsync(AppDbContext db, CancellationToken ct)
    {
        await db.Pagamentos.ExecuteDeleteAsync(ct);
        await db.MovimentosEstoque.ExecuteDeleteAsync(ct);
        await db.Pedidos.ExecuteDeleteAsync(ct);
        await db.FichaTecnicaItens.ExecuteDeleteAsync(ct);
        await db.FichasTecnicas.ExecuteDeleteAsync(ct);
        await db.Sabores.ExecuteDeleteAsync(ct);
        await db.Ingredientes.ExecuteDeleteAsync(ct);
        await db.Clientes.ExecuteDeleteAsync(ct);
    }
}
