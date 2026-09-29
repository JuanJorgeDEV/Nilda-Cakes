using System.Globalization;
using System.Text;
using DeliciasDaNilda.Domain.Entities;

namespace DeliciasDaNilda.Services.Common;

/// <summary>
/// Monta a mensagem que o cliente manda para o WhatsApp da loja depois de
/// fazer o pedido no site. O código "#P{id}" é o que o bot usa para achar o
/// pedido (ver whatsapp-bot/src/conversation.js).
/// </summary>
public static class MensagemPedidoWhatsApp
{
    private static readonly CultureInfo PtBr = new("pt-BR");

    public static string Codigo(int pedidoId) => $"#P{pedidoId}";

    /// <summary>Pedido precisa vir com Cliente e Sabor carregados.</summary>
    public static string Montar(Pedido pedido)
    {
        var texto = new StringBuilder();
        texto.AppendLine("Oi, Nilda! Acabei de fazer um pedido pelo site 🎂");
        texto.AppendLine();
        texto.AppendLine($"*Pedido {Codigo(pedido.Id)}*");
        texto.AppendLine($"Nome: {pedido.Cliente.NomeCompleto}");
        texto.AppendLine($"Bolo: {pedido.Sabor.Nome}");
        texto.AppendLine($"Tamanho: {pedido.Peso.ToString("0.##", PtBr)} kg");
        texto.AppendLine($"Entrega: {pedido.DataEntrega.ToString("dddd, dd/MM", PtBr)}");

        if (pedido.EhAniversario && !string.IsNullOrWhiteSpace(pedido.NomeAniversariante))
        {
            var idade = pedido.IdadeAniversariante is { } i ? $" ({i} anos)" : "";
            texto.AppendLine($"Aniversário de: {pedido.NomeAniversariante}{idade}");
        }

        if (!string.IsNullOrWhiteSpace(pedido.Tema))
        {
            texto.AppendLine($"Tema: {pedido.Tema}");
        }

        if (!string.IsNullOrWhiteSpace(pedido.Observacoes))
        {
            texto.AppendLine($"Obs: {pedido.Observacoes}");
        }

        texto.AppendLine($"Valor: {pedido.ValorTotal.ToString("C", PtBr)}");
        // Quebra de linha simples (\n), independente do sistema do servidor.
        return texto.ToString().Replace("\r\n", "\n").TrimEnd();
    }

    public static string Link(string numeroLoja, string mensagem)
        => $"https://wa.me/{WhatsAppHelper.Normalizar(numeroLoja)}?text={Uri.EscapeDataString(mensagem)}";
}
