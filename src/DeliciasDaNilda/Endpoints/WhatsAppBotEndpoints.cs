using DeliciasDaNilda.Services;

namespace DeliciasDaNilda.Endpoints;

/// <summary>
/// Endpoints internos consumidos exclusivamente pelo serviço Node.js do bot
/// de WhatsApp (Baileys). O bot não reimplementa nenhuma regra de negócio:
/// ele só coleta as respostas do menu numerado da conversa e chama estes
/// endpoints, que reaproveitam os services de domínio já validados
/// (IPrecificacaoService/RN03, IAgendamentoService/RN05-RN07,
/// ISaborService/RN04, IPedidoService/RN08).
///
/// Autenticação: exige o header "X-Internal-Api-Key" (ver
/// InternalApiKeyMiddleware), configurado apenas para o processo do bot —
/// não é uma sessão de cliente nem de administrador.
/// </summary>
public static class WhatsAppBotEndpoints
{
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
        // Resposta: [ { "data": "yyyy-MM-dd", "disponivel": bool } ]
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
                .Select(par => new DataDisponivelResponse(par.Key.ToString("yyyy-MM-dd"), Disponivel: !par.Value))
                .ToList();

            return Results.Ok(resposta);
        });

        // POST /internal/whatsapp-bot/pedidos
        // Request: { "nomeCompleto": string, "whatsApp": string, "saborId": int,
        //            "pesoKg": decimal, "dataEntrega": "yyyy-MM-dd", "observacoes"?: string }
        // Resposta 201: { "id": int, "valorTotal": decimal, "status": string }
        // Resposta 400: { "erro": string } — mensagem em português pronta
        // para o bot repassar ao cliente (RN03/RN05-RN08 validados dentro de
        // IPedidoService.CriarPedidoAsync).
        grupo.MapPost("/pedidos", async (
            CriarPedidoBotRequest request,
            IClienteService clienteService,
            IPedidoService pedidoService,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.NomeCompleto) || string.IsNullOrWhiteSpace(request.WhatsApp))
            {
                return Results.BadRequest(new ErroResponse("Informe nome completo e WhatsApp."));
            }

            if (!DateOnly.TryParse(request.DataEntrega, out var dataEntrega))
            {
                return Results.BadRequest(new ErroResponse("Data de entrega inválida. Use o formato AAAA-MM-DD."));
            }

            Domain.Entities.Cliente cliente;
            try
            {
                // RN01 — obtém ou cria o cliente pelo WhatsApp normalizado.
                cliente = await clienteService.ObterOuCriarAsync(request.NomeCompleto, request.WhatsApp, ct);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new ErroResponse(ex.Message));
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

    public record StatusBotResponse(bool Ativo);

    public record SaborResponse(int Id, string Nome, decimal PrecoPorKg);

    public record DataDisponivelResponse(string Data, bool Disponivel);

    public record CriarPedidoBotRequest(
        string NomeCompleto,
        string WhatsApp,
        int SaborId,
        decimal PesoKg,
        string DataEntrega,
        string? Observacoes);

    public record PedidoCriadoResponse(int Id, decimal ValorTotal, string Status);

    public record ErroResponse(string Erro);
}
