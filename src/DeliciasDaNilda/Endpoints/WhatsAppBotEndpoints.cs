using System.Globalization;
using DeliciasDaNilda.Data;
using DeliciasDaNilda.Domain.Entities;
using DeliciasDaNilda.Domain.Enums;
using DeliciasDaNilda.Services;
using DeliciasDaNilda.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace DeliciasDaNilda.Endpoints;

/// <summary>
/// Endpoints internos consumidos exclusivamente pelo serviço Node.js do bot
/// de WhatsApp (Baileys). O bot não reimplementa nenhuma regra de negócio:
/// ele só conduz a conversa e chama estes endpoints, que reaproveitam os
/// services de domínio já validados (IClienteService/RN01,
/// IPrecificacaoService/RN03, IAgendamentoService/RN05-RN07,
/// ISaborService/RN04, IPedidoService/RN08).
///
/// Autenticação: exige o header "X-Internal-Api-Key" (ver
/// InternalApiKeyMiddleware), configurado apenas para o processo do bot —
/// não é uma sessão de cliente nem de administrador.
/// </summary>
public static class WhatsAppBotEndpoints
{
    private static readonly CultureInfo PtBr = new("pt-BR");

    public static void MapWhatsAppBotEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/internal/whatsapp-bot");

        // GET /internal/whatsapp-bot/status
        // Resposta: { "ativo": bool }
        // O bot deve consultar isso antes de responder a qualquer mensagem;
        // se "ativo" for false, o atendimento automático está desligado
        // pelo painel ADM e o bot não deve responder pedidos.
        grupo.MapGet("/status", async (IConfiguracaoService configuracaoService, CancellationToken ct) =>
        {
            var ativo = await configuracaoService.BotAtivoAsync(ct);
            return Results.Ok(new StatusBotResponse(ativo));
        });

        // GET /internal/whatsapp-bot/mensagens
        // Resposta: { "chave": "texto", ... } — textos efetivos (padrão ou
        // personalizados pela Nilda na tela de mensagens do painel).
        grupo.MapGet("/mensagens", async (IMensagemBotService mensagens, CancellationToken ct) =>
            Results.Ok(await mensagens.ObterEfetivasAsync(ct)));

        // GET /internal/whatsapp-bot/sabores
        // Resposta: [ { "id": int, "nome": string, "precoPorKg": decimal } ]
        // RN04 — só sabores marcados como disponíveis são listados, pois é
        // o cardápio que o cliente pode escolher para um novo pedido.
        grupo.MapGet("/sabores", async (ISaborService saborService, CancellationToken ct) =>
        {
            var sabores = await saborService.ListarAsync(apenasDisponiveis: true, ct);
            var resposta = sabores
                .Select(s => new SaborResponse(s.Id, s.Nome, s.PrecoPorKg))
                .ToList();
            return Results.Ok(resposta);
        });

        // GET /internal/whatsapp-bot/datas-disponiveis?quantidade=10
        // Resposta: [ { "data": "yyyy-MM-dd", "texto": "sábado, 03/10", "disponivel": bool } ]
        // RN05 (antecedência mínima) + RN06/RN07 (capacidade diária/bloqueio
        // automático): a listagem já começa na primeira data que atende à
        // antecedência mínima, e cada data traz se ainda tem vaga.
        grupo.MapGet("/datas-disponiveis", async (
            IAgendamentoService agendamentoService,
            int? quantidade,
            CancellationToken ct) =>
        {
            const int quantidadeDefault = 10;
            var quantidadeDias = quantidade is > 0 ? quantidade.Value : quantidadeDefault;

            var dataInicio = agendamentoService.CalcularDataMinimaPermitida(DateTime.UtcNow);
            var dataFim = dataInicio.AddDays(quantidadeDias - 1);

            var bloqueios = await agendamentoService.ObterBloqueiosNoIntervaloAsync(dataInicio, dataFim, ct);

            var resposta = bloqueios
                .OrderBy(par => par.Key)
                .Select(par => new DataDisponivelResponse(par.Key.ToString("yyyy-MM-dd"), TextoData(par.Key), Disponivel: !par.Value))
                .ToList();

            return Results.Ok(resposta);
        });

        // GET /internal/whatsapp-bot/clientes/{whatsapp}
        // RN01 — o número é a identificação. Resposta 200:
        // { "id", "nome", "primeiroNome", "temCep", "pedidos": [PedidoBotResponse] }
        // (pedidos em andamento e entregues na última semana). 404 se o número é novo.
        grupo.MapGet("/clientes/{whatsapp}", async (
            string whatsapp,
            IClienteService clienteService,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var cliente = await clienteService.ObterPorWhatsAppAsync(whatsapp, ct);
            if (cliente is null)
            {
                return Results.NotFound(new ErroResponse("Cliente não cadastrado."));
            }

            var desde = DateOnly.FromDateTime(DateTime.Today).AddDays(-7);
            var pedidos = await db.Pedidos
                .AsNoTracking()
                .Include(p => p.Sabor)
                .Include(p => p.Pagamentos)
                .Where(p => p.ClienteId == cliente.Id && p.StatusPedido != StatusPedido.Cancelado && p.DataEntrega >= desde)
                .OrderBy(p => p.DataEntrega)
                .Take(5)
                .ToListAsync(ct);

            return Results.Ok(new ClienteBotResponse(
                cliente.Id,
                cliente.NomeCompleto,
                PrimeiroNome(cliente.NomeCompleto),
                !string.IsNullOrWhiteSpace(cliente.Cep),
                pedidos.Select(ParaResposta).ToList()));
        });

        // POST /internal/whatsapp-bot/clientes
        // Request: { "nomeCompleto", "whatsApp", "cep", "bairro"?, "cidade"? }
        // Primeiro cadastro de um número (RN01 + CEP para a região de entrega).
        grupo.MapPost("/clientes", async (CadastroBotRequest request, IClienteService clienteService, CancellationToken ct) =>
        {
            var resultado = await clienteService.CadastrarAsync(
                new DadosCadastroCliente(request.NomeCompleto, request.WhatsApp, request.Cep, request.Bairro, request.Cidade), ct);

            if (!resultado.Sucesso)
            {
                return Results.BadRequest(new ErroResponse(resultado.Erro!));
            }

            var cliente = resultado.Valor!;
            return Results.Ok(new ClienteBotResponse(cliente.Id, cliente.NomeCompleto, PrimeiroNome(cliente.NomeCompleto), true, new List<PedidoBotResponse>()));
        });

        // GET /internal/whatsapp-bot/pedidos/{id}?whatsapp=5516...
        // Resumo de um pedido — só se ele for do número informado (o código
        // #P vem na mensagem que o site monta; não expõe pedido de terceiros).
        grupo.MapGet("/pedidos/{id:int}", async (int id, string? whatsapp, AppDbContext db, CancellationToken ct) =>
        {
            var pedido = await BuscarPedidoDoNumeroAsync(db, id, whatsapp, ct);
            return pedido is null
                ? Results.NotFound(new ErroResponse("Pedido não encontrado para este número."))
                : Results.Ok(ParaResposta(pedido));
        });

        // POST /internal/whatsapp-bot/pedidos/{id}/personalizacao
        // Request: { "whatsApp", "texto" } — anota a personalização/referência
        // que a cliente mandou no pedido (só se o pedido for do número).
        grupo.MapPost("/pedidos/{id:int}/personalizacao", async (
            int id,
            PersonalizacaoRequest request,
            AppDbContext db,
            IPedidoService pedidoService,
            CancellationToken ct) =>
        {
            var pedido = await BuscarPedidoDoNumeroAsync(db, id, request.WhatsApp, ct);
            if (pedido is null)
            {
                return Results.NotFound(new ErroResponse("Pedido não encontrado para este número."));
            }

            var resultado = await pedidoService.AdicionarPersonalizacaoAsync(id, request.Texto ?? string.Empty, ct);
            return resultado.Sucesso ? Results.NoContent() : Results.BadRequest(new ErroResponse(resultado.Erro!));
        });

        // POST /internal/whatsapp-bot/pedidos
        // Request: { "nomeCompleto"?: string, "whatsApp": string, "saborId": int,
        //            "pesoKg": decimal, "dataEntrega": "yyyy-MM-dd", "observacoes"?: string }
        // Resposta 201: { "id": int, "valorTotal": decimal, "status": string }
        // Resposta 400: { "erro": string } — mensagem em português pronta
        // para o bot repassar ao cliente (RN03/RN05-RN08 validados dentro de
        // IPedidoService.CriarPedidoAsync). O nome só é exigido se o número
        // ainda não tiver cadastro.
        grupo.MapPost("/pedidos", async (
            CriarPedidoBotRequest request,
            IClienteService clienteService,
            IPedidoService pedidoService,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.WhatsApp))
            {
                return Results.BadRequest(new ErroResponse("Informe o WhatsApp."));
            }

            if (!DateOnly.TryParse(request.DataEntrega, CultureInfo.InvariantCulture, out var dataEntrega))
            {
                return Results.BadRequest(new ErroResponse("Data de entrega inválida. Use o formato AAAA-MM-DD."));
            }

            // RN01 — cliente pelo número; cria só se for novo (com o nome informado).
            var cliente = await clienteService.ObterPorWhatsAppAsync(request.WhatsApp, ct);
            if (cliente is null)
            {
                if (string.IsNullOrWhiteSpace(request.NomeCompleto))
                {
                    return Results.BadRequest(new ErroResponse("Informe o nome para o primeiro pedido."));
                }

                try
                {
                    cliente = await clienteService.ObterOuCriarAsync(request.NomeCompleto, request.WhatsApp, ct);
                }
                catch (ArgumentException ex)
                {
                    return Results.BadRequest(new ErroResponse(ex.Message));
                }
            }

            // RN03 (preço), RN05-RN07 (antecedência/capacidade), RN08 (status
            // inicial sempre Pendente de Confirmação) — tudo validado dentro
            // do service, nada reimplementado aqui.
            var resultado = await pedidoService.CriarPedidoAsync(
                cliente.Id,
                request.SaborId,
                request.PesoKg,
                dataEntrega,
                request.Observacoes,
                ct);

            if (!resultado.Sucesso)
            {
                return Results.BadRequest(new ErroResponse(resultado.Erro!));
            }

            var pedido = resultado.Valor!;
            return Results.Created(
                $"/internal/whatsapp-bot/pedidos/{pedido.Id}",
                new PedidoCriadoResponse(pedido.Id, pedido.ValorTotal, pedido.StatusPedido.ToString()));
        });
    }

    private static async Task<Pedido?> BuscarPedidoDoNumeroAsync(AppDbContext db, int id, string? whatsapp, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(whatsapp))
        {
            return null;
        }

        var variantes = WhatsAppHelper.Variantes(whatsapp);
        return await db.Pedidos
            .AsNoTracking()
            .Include(p => p.Sabor)
            .Include(p => p.Pagamentos)
            .Include(p => p.Cliente)
            .FirstOrDefaultAsync(p => p.Id == id && variantes.Contains(p.Cliente.WhatsApp), ct);
    }

    private static PedidoBotResponse ParaResposta(Pedido p)
    {
        var pago = p.Pagamentos.Sum(x => x.Valor);
        var situacao = p.StatusPedido switch
        {
            StatusPedido.PendenteConfirmacao => "aguardando a Nilda confirmar",
            StatusPedido.Confirmado => p.HorarioRetirada is { } h ? $"confirmado, retirada às {h:HH\\:mm}" : "confirmado",
            StatusPedido.Concluido => "entregue",
            _ => "cancelado"
        };
        var resumo = $"{p.Sabor.Nome} · {QuantidadeFormatter.Formatar(p.Peso, "kg")} · {TextoData(p.DataEntrega)}";

        return new PedidoBotResponse(
            p.Id,
            MensagemPedidoWhatsApp.Codigo(p.Id),
            p.Sabor.Nome,
            p.Peso,
            p.DataEntrega.ToString("yyyy-MM-dd"),
            TextoData(p.DataEntrega),
            p.StatusPedido.ToString(),
            situacao,
            p.ValorTotal,
            pago,
            resumo);
    }

    private static string TextoData(DateOnly data) => data.ToString("dddd, dd/MM", PtBr);

    private static string PrimeiroNome(string nome) => nome.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? nome;

    public record StatusBotResponse(bool Ativo);

    public record SaborResponse(int Id, string Nome, decimal PrecoPorKg);

    public record DataDisponivelResponse(string Data, string Texto, bool Disponivel);

    public record ClienteBotResponse(int Id, string Nome, string PrimeiroNome, bool TemCep, IReadOnlyList<PedidoBotResponse> Pedidos);

    public record CadastroBotRequest(string NomeCompleto, string WhatsApp, string? Cep, string? Bairro, string? Cidade);

    public record PedidoBotResponse(
        int Id,
        string Codigo,
        string Sabor,
        decimal PesoKg,
        string DataEntrega,
        string DataTexto,
        string Status,
        string Situacao,
        decimal ValorTotal,
        decimal Pago,
        string Resumo);

    public record PersonalizacaoRequest(string WhatsApp, string? Texto);

    public record CriarPedidoBotRequest(
        string? NomeCompleto,
        string WhatsApp,
        int SaborId,
        decimal PesoKg,
        string DataEntrega,
        string? Observacoes);

    public record PedidoCriadoResponse(int Id, decimal ValorTotal, string Status);

    public record ErroResponse(string Erro);
}
