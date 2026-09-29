using DeliciasDaNilda.Domain.Entities;
using DeliciasDaNilda.Services.Common;

namespace DeliciasDaNilda.Tests;

public class MensagemPedidoWhatsAppTests
{
    [Fact]
    public void Mensagem_TemCodigoQueOBotReconhece_EOsDadosDoPedido()
    {
        var pedido = new Pedido
        {
            Id = 123,
            Cliente = new Cliente { NomeCompleto = "Ana Paula" },
            Sabor = new Sabor { Nome = "Ninho com morango" },
            Peso = 1.5m,
            ValorTotal = 112.5m,
            DataEntrega = new DateOnly(2026, 10, 3),
            EhAniversario = true,
            NomeAniversariante = "Laura",
            IdadeAniversariante = 7,
            Tema = "unicórnio"
        };

        var texto = MensagemPedidoWhatsApp.Montar(pedido);

        Assert.Contains("#P123", texto);
        Assert.Contains("Ninho com morango", texto);
        Assert.Contains("1,5 kg", texto);
        Assert.Contains("Laura (7 anos)", texto);
        Assert.Contains("unicórnio", texto);
        Assert.Contains("R$ 112,50", texto);
    }

    [Fact]
    public void Link_UsaNumeroNormalizadoETextoCodificado()
    {
        var link = MensagemPedidoWhatsApp.Link("(16) 99999-0000", "Pedido #P1 ok");

        Assert.Equal("https://wa.me/5516999990000?text=Pedido%20%23P1%20ok", link);
    }
}
