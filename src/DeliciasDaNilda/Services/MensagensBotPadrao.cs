namespace DeliciasDaNilda.Services;

/// <summary>Definição de uma mensagem do bot: texto padrão e regras de edição.</summary>
public sealed record MensagemBotDefinicao(
    string Chave,
    string Titulo,
    string Quando,
    string Padrao,
    IReadOnlyList<string> Placeholders,
    IReadOnlyList<string> Obrigatorios);

/// <summary>Informação que a Nilda pode inserir na mensagem, no formato {nome}.</summary>
public sealed record PlaceholderInfo(string Nome, string Descricao, string Exemplo);

/// <summary>
/// FONTE ÚNICA dos textos padrão do bot de WhatsApp. O bot (Node) não
/// duplica estes textos: busca-os em GET /internal/whatsapp-bot/mensagens.
/// A ordem das perguntas é fixa (no bot); só os textos são editáveis.
/// Não corresponde a uma RN numerada.
/// </summary>
public static class MensagensBotPadrao
{
    public const int TamanhoMaximoChave = 60;
    public const int TamanhoMaximoTexto = 1000;

    public static class Chaves
    {
        public const string Menu = "menu";
        public const string BotDesativado = "bot_desativado";
        public const string FalarComNilda = "falar_com_nilda";
        public const string SaboresTitulo = "sabores_titulo";
        public const string SemSabores = "sem_sabores";
        public const string PerguntaNome = "pergunta_nome";
        public const string NomeInvalido = "nome_invalido";
        public const string ConfirmaWhatsApp = "confirma_whatsapp";
        public const string WhatsAppInvalido = "whatsapp_invalido";
        public const string PerguntaSabor = "pergunta_sabor";
        public const string PerguntaPeso = "pergunta_peso";
        public const string PesoInvalido = "peso_invalido";
        public const string PerguntaData = "pergunta_data";
        public const string SemDatas = "sem_datas";
        public const string PerguntaObservacao = "pergunta_observacao";
        public const string ResumoTitulo = "resumo_titulo";
        public const string ResumoConfirmacao = "resumo_confirmacao";
        public const string ConfirmacaoInvalida = "confirmacao_invalida";
        public const string PedidoCancelado = "pedido_cancelado";
        public const string PedidoSucesso = "pedido_sucesso";
        public const string PedidoFalha = "pedido_falha";
        public const string ErroTemporario = "erro_temporario";
        public const string OpcaoInvalida = "opcao_invalida";
    }

    /// <summary>Informações que podem aparecer nas mensagens, com exemplo para a prévia.</summary>
    public static readonly IReadOnlyList<PlaceholderInfo> InformacoesDisponiveis = new[]
    {
        new PlaceholderInfo("nome", "o nome que a cliente informou", "Maria Silva"),
        new PlaceholderInfo("numero", "o número do pedido", "123"),
        new PlaceholderInfo("valor", "o valor total do pedido", "R$ 150,00"),
        new PlaceholderInfo("erro", "o motivo pelo qual o pedido não foi aceito", "A data escolhida não está disponível."),
    };

    private static readonly string[] Nenhum = Array.Empty<string>();

    public static readonly IReadOnlyList<MensagemBotDefinicao> Todas = new[]
    {
        new MensagemBotDefinicao(Chaves.Menu, "Menu principal e boas-vindas",
            "Quando a cliente puxa conversa, escolhe uma opção inválida ou volta ao menu.",
            "Olá! Sou o atendimento automático da *Delícias da Nilda*. Escolha uma opção:\n\n1. Ver sabores disponíveis\n2. Fazer um pedido\n3. Falar com a Nilda\n\nResponda com o número da opção desejada.",
            Nenhum, Nenhum),
        new MensagemBotDefinicao(Chaves.BotDesativado, "Atendimento automático desligado",
            "Quando a cliente escreve e o atendimento automático está desligado (enviada uma vez).",
            "No momento o atendimento automático está desativado. Por favor, entre em contato diretamente com a Delícias da Nilda para fazer seu pedido.",
            Nenhum, Nenhum),
        new MensagemBotDefinicao(Chaves.FalarComNilda, "Falar com a Nilda",
            "Quando a cliente escolhe a opção 3. Depois disso o atendimento automático fica em silêncio para ela; para voltar, ela envia \"menu\".",
            "Certo! A Nilda vai te responder por aqui assim que possível. Se quiser voltar ao atendimento automático, envie *menu*.",
            Nenhum, Nenhum),
        new MensagemBotDefinicao(Chaves.SaboresTitulo, "Título da lista de sabores",
            "Acima da lista de sabores e preços, quando a cliente escolhe a opção 1.",
            "*Sabores disponíveis:*",
            Nenhum, Nenhum),
        new MensagemBotDefinicao(Chaves.SemSabores, "Nenhum sabor disponível",
            "Quando não há nenhum sabor disponível para mostrar ou pedir.",
            "No momento não há sabores disponíveis. Tente novamente mais tarde.",
            Nenhum, Nenhum),
        new MensagemBotDefinicao(Chaves.PerguntaNome, "Pergunta do nome",
            "Primeira pergunta do pedido, logo após a cliente escolher a opção 2.",
            "Vamos começar seu pedido! Qual é o seu nome completo?",
            Nenhum, Nenhum),
        new MensagemBotDefinicao(Chaves.NomeInvalido, "Nome inválido",
            "Quando o nome informado é curto demais.",
            "Por favor, informe seu nome completo (nome e sobrenome).",
            Nenhum, Nenhum),
        new MensagemBotDefinicao(Chaves.ConfirmaWhatsApp, "Confirmação do número de WhatsApp",
            "Depois do nome, perguntando se o pedido pode usar o número de WhatsApp da conversa.",
            "Obrigada, {nome}! Podemos usar este número de WhatsApp para o pedido? Responda *sim* para confirmar ou digite outro número (com DDD).",
            new[] { "nome" }, Nenhum),
        new MensagemBotDefinicao(Chaves.WhatsAppInvalido, "Resposta não entendida (número de WhatsApp)",
            "Quando a cliente não responde \"sim\" nem digita um número válido.",
            "Não entendi. Responda *sim* para usar este número, ou digite outro número de WhatsApp válido.",
            Nenhum, Nenhum),
        new MensagemBotDefinicao(Chaves.PerguntaSabor, "Pergunta do sabor",
            "Acima da lista numerada de sabores, durante o pedido.",
            "Escolha o sabor pelo número:",
            Nenhum, Nenhum),
        new MensagemBotDefinicao(Chaves.PerguntaPeso, "Pergunta do peso",
            "Depois que a cliente escolhe o sabor.",
            "Qual o peso desejado, em quilos? (ex: 1.5)",
            Nenhum, Nenhum),
        new MensagemBotDefinicao(Chaves.PesoInvalido, "Peso inválido",
            "Quando o peso informado não é um número válido.",
            "Peso inválido. Informe um número positivo em quilos (ex: 1.5).",
            Nenhum, Nenhum),
        new MensagemBotDefinicao(Chaves.PerguntaData, "Pergunta da data de entrega",
            "Acima da lista numerada de datas, durante o pedido.",
            "Escolha a data de entrega pelo número:",
            Nenhum, Nenhum),
        new MensagemBotDefinicao(Chaves.SemDatas, "Nenhuma data disponível",
            "Quando não há nenhuma data de entrega disponível.",
            "Não há datas disponíveis no momento. Tente novamente mais tarde ou fale com a Nilda (opção 3 do menu).",
            Nenhum, Nenhum),
        new MensagemBotDefinicao(Chaves.PerguntaObservacao, "Pergunta de observação",
            "Depois da data, perguntando se a cliente quer acrescentar alguma observação.",
            "Deseja adicionar alguma observação ao pedido? Se não, responda *não*.",
            Nenhum, Nenhum),
        new MensagemBotDefinicao(Chaves.ResumoTitulo, "Título do resumo do pedido",
            "Acima do resumo (nome, sabor, peso, data...) que a cliente confere antes de confirmar.",
            "*Resumo do pedido:*",
            Nenhum, Nenhum),
        new MensagemBotDefinicao(Chaves.ResumoConfirmacao, "Pedido de confirmação",
            "Logo abaixo do resumo, pedindo para a cliente confirmar.",
            "Confirma o pedido? Responda *sim* ou *não*.",
            Nenhum, Nenhum),
        new MensagemBotDefinicao(Chaves.ConfirmacaoInvalida, "Resposta não entendida (confirmação)",
            "Quando a cliente não responde \"sim\" nem \"não\" na confirmação do pedido.",
            "Responda *sim* para confirmar o pedido ou *não* para cancelar.",
            Nenhum, Nenhum),
        new MensagemBotDefinicao(Chaves.PedidoCancelado, "Pedido cancelado pela cliente",
            "Quando a cliente responde \"não\" na confirmação. É enviada junto com o menu.",
            "Pedido cancelado. Nenhuma informação foi enviada.",
            Nenhum, Nenhum),
        new MensagemBotDefinicao(Chaves.PedidoSucesso, "Pedido registrado com sucesso",
            "Quando o pedido é registrado.",
            "Pedido #{numero} registrado com sucesso! Valor total: {valor}.\n\nA confirmação final e o horário de entrega serão combinados diretamente com a Nilda.",
            new[] { "numero", "valor" }, new[] { "numero" }),
        new MensagemBotDefinicao(Chaves.PedidoFalha, "Pedido não pôde ser registrado",
            "Quando o pedido não é aceito (por exemplo, data sem vaga). O motivo vem do sistema. É enviada junto com o menu.",
            "Não foi possível concluir o pedido: {erro}\n\nVocê pode tentar novamente escolhendo a opção *2* do menu.",
            new[] { "erro" }, new[] { "erro" }),
        new MensagemBotDefinicao(Chaves.ErroTemporario, "Problema temporário",
            "Quando não foi possível buscar sabores/datas ou enviar o pedido por um problema momentâneo.",
            "Não consegui fazer isso agora. Tente novamente em instantes.",
            Nenhum, Nenhum),
        new MensagemBotDefinicao(Chaves.OpcaoInvalida, "Opção inválida",
            "Quando a cliente escolhe um número que não está na lista (sabor ou data). A lista é repetida abaixo.",
            "Opção inválida. Escolha um dos números da lista:",
            Nenhum, Nenhum),
    };

    public static MensagemBotDefinicao? Buscar(string? chave)
        => Todas.FirstOrDefault(d => d.Chave == chave);

    /// <summary>Troca {nome}, {valor} etc. pelos exemplos — usado só na prévia da tela.</summary>
    public static string PreencherComExemplos(string texto)
    {
        foreach (var info in InformacoesDisponiveis)
        {
            texto = texto.Replace("{" + info.Nome + "}", info.Exemplo);
        }
        return texto;
    }
}
