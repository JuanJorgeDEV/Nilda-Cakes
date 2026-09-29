using System.ComponentModel.DataAnnotations;
using DeliciasDaNilda.Domain.Entities;
using DeliciasDaNilda.Services;
using DeliciasDaNilda.Services.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DeliciasDaNilda.Pages.Cliente;

/// <summary>
/// Funil de pedido em etapas: bolo (foto), tamanho, data, ocasião e
/// revisão. Preço sempre peso × preço/kg (RN03); datas respeitam a
/// antecedência mínima (RN05) e o limite diário (RN06/RN07); o pedido nasce
/// Pendente de Confirmação (RN08).
/// </summary>
public class NovoPedidoModel : PageModel
{
    private const int DiasDeJanelaParaSelecaoDeData = 45;

    private readonly IPedidoService _pedidoService;
    private readonly IAgendamentoService _agendamentoService;
    private readonly ISaborService _saborService;
    private readonly IConfiguracaoService _configuracao;

    public NovoPedidoModel(
        IPedidoService pedidoService,
        IAgendamentoService agendamentoService,
        ISaborService saborService,
        IConfiguracaoService configuracao)
    {
        _configuracao = configuracao;
        _pedidoService = pedidoService;
        _agendamentoService = agendamentoService;
        _saborService = saborService;
    }

    [BindProperty]
    public EntradaModel Entrada { get; set; } = new();

    public List<Sabor> SaboresDisponiveis { get; set; } = new();

    public List<OpcaoData> OpcoesDeData { get; set; } = new();

    public string? ClienteNome { get; set; }

    public string? Erro { get; set; }

    /// <summary>Link wa.me para pedir um bolo fora do cardápio (null se a loja não cadastrou o número).</summary>
    public string? LinkOutroBolo { get; set; }

    /// <summary>Link wa.me para a cliente que prefere fazer o pedido conversando.</summary>
    public string? LinkConversar { get; set; }

    public class EntradaModel
    {
        [Range(1, int.MaxValue, ErrorMessage = "Escolha o bolo.")]
        public int SaborId { get; set; }

        [Range(0.5, 20, ErrorMessage = "Escolha um tamanho entre 0,5 kg e 20 kg.")]
        public decimal PesoKg { get; set; }

        [Required(ErrorMessage = "Escolha a data de entrega.")]
        public DateOnly? DataEntrega { get; set; }

        public bool EhAniversario { get; set; }

        [StringLength(100)]
        public string? NomeAniversariante { get; set; }

        [Range(0, 129, ErrorMessage = "Idade inválida.")]
        public int? IdadeAniversariante { get; set; }

        [StringLength(200)]
        public string? Tema { get; set; }

        [StringLength(500)]
        public string? Observacoes { get; set; }
    }

    public record OpcaoData(DateOnly Data, bool Bloqueada);

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (HttpContext.Session.GetInt32("ClienteId") is null)
        {
            return RedirectToPage("/Cliente/Identificacao");
        }

        ClienteNome = HttpContext.Session.GetString("ClienteNome");
        await CarregarDadosDeApoioAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        var clienteId = HttpContext.Session.GetInt32("ClienteId");
        if (clienteId is null)
        {
            return RedirectToPage("/Cliente/Identificacao");
        }

        ClienteNome = HttpContext.Session.GetString("ClienteNome");

        if (!ModelState.IsValid)
        {
            Erro = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).FirstOrDefault(m => !string.IsNullOrWhiteSpace(m))
                   ?? "Confira os dados do pedido.";
            await CarregarDadosDeApoioAsync(ct);
            return Page();
        }

        // RN03/RN05/RN06/RN07/RN08 — toda a validação de negócio acontece no service.
        var resultado = await _pedidoService.CriarPedidoAsync(
            clienteId.Value,
            Entrada.SaborId,
            Entrada.PesoKg,
            Entrada.DataEntrega!.Value,
            Entrada.Observacoes,
            ct,
            new OcasiaoPedido(Entrada.EhAniversario, Entrada.NomeAniversariante, Entrada.IdadeAniversariante, Entrada.Tema));

        if (!resultado.Sucesso)
        {
            Erro = resultado.Erro;
            await CarregarDadosDeApoioAsync(ct);
            return Page();
        }

        return RedirectToPage("/Cliente/Confirmacao", new { id = resultado.Valor!.Id, enviar = true });
    }

    private async Task CarregarDadosDeApoioAsync(CancellationToken ct)
    {
        SaboresDisponiveis = await _saborService.ListarAsync(apenasDisponiveis: true, ct);

        var numeroLoja = await _configuracao.WhatsAppLojaAsync(ct);
        if (numeroLoja is not null)
        {
            var nome = (ClienteNome ?? string.Empty).Split(' ')[0];
            var saudacao = string.IsNullOrWhiteSpace(nome) ? "Oi, Nilda!" : $"Oi, Nilda! Aqui é {nome}.";
            LinkOutroBolo = MensagemPedidoWhatsApp.Link(numeroLoja, saudacao + " Quero encomendar um bolo que não está no cardápio.");
            LinkConversar = MensagemPedidoWhatsApp.Link(numeroLoja, saudacao + " Prefiro fazer minha encomenda pelo WhatsApp.");
        }

        // RN05: data mínima de entrega (3 dias úteis a partir de agora).
        var dataMinima = _agendamentoService.CalcularDataMinimaPermitida(DateTime.UtcNow);
        var dataFinal = dataMinima.AddDays(DiasDeJanelaParaSelecaoDeData);

        // RN06/RN07: mapa de bloqueio por data (>= 3 pedidos ativos no dia).
        var bloqueios = await _agendamentoService.ObterBloqueiosNoIntervaloAsync(dataMinima, dataFinal, ct);
        OpcoesDeData = bloqueios
            .OrderBy(kv => kv.Key)
            .Select(kv => new OpcaoData(kv.Key, kv.Value))
            .ToList();
    }
}
