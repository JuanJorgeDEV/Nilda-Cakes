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

        // Cadastro de número novo (RN01 + CEP).
        public const string PerguntaNome = "pergunta_nome";
        public const string NomeInvalido = "nome_invalido";
        public const string PerguntaCep = "pergunta_cep";
        public const string CepInvalido = "cep_invalido";
        public const string CadastroConcluido = "cadastro_concluido";

        // Pedido pelo WhatsApp.
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

        // Pedido que veio do site (mensagem com #P123).
        public const string PedidoSiteRecebido = "pedido_site_recebido";
        public const string PedidoSiteNaoEncontrado = "pedido_site_nao_encontrado";
        public const string PerguntaPersonalizacao = "pergunta_personalizacao";
        public const string PersonalizacaoRecebida = "personalizacao_recebida";
        public const string FotoRecebida = "foto_recebida";
        public const string PersonalizacaoFim = "personalizacao_fim";

        // Meus pedidos.
        public const string MeusPedidosTitulo = "meus_pedidos_titulo";
        public const string SemPedidos = "sem_pedidos";

        public const string ErroTemporario = "erro_temporario";
        public const string OpcaoInvalida = "opcao_invalida";
    }

    /// <summary>Informações que podem aparecer nas mensagens, com exemplo para a prévia.</summary>
    public static readonly IReadOnlyList<PlaceholderInfo> InformacoesDisponiveis = new[]
    {
        new PlaceholderInfo("nome", "o primeiro nome da cliente", "Maria"),
        new PlaceholderInfo("numero", "o número do pedido", "123"),
        new PlaceholderInfo("valor", "o valor total do pedido", "R$ 150,00"),
        new PlaceholderInfo("resumo", "o resumo do pedido (bolo, tamanho, data)", "Ninho com morango · 2 kg · sábado, 03/10"),
        new PlaceholderInfo("erro", "o motivo pelo qual o pedido não foi aceito", "A data escolhida não está disponível."),
    };

    private static readonly string[] Nenhum = Array.Empty<string>();

    /// <summary>Abas da tela de mensagens, na ordem em que aparecem, e as chaves de cada uma.</summary>
    public static readonly IReadOnlyList<(string Titulo, string[] Chaves)> Grupos = new (string, string[])[]
    {
        ("Chegada", new[] { Chaves.Menu, Chaves.BotDesativado, Chaves.FalarComNilda, Chaves.SaboresTitulo, Chaves.SemSabores }),
        ("Cadastro", new[] { Chaves.PerguntaNome, Chaves.NomeInvalido, Chaves.PerguntaCep, Chaves.CepInvalido, Chaves.CadastroConcluido }),
        ("Pedido pelo WhatsApp", new[]
        {
            Chaves.PerguntaSabor, Chaves.PerguntaPeso, Chaves.PesoInvalido, Chaves.PerguntaData, Chaves.SemDatas,
            Chaves.PerguntaObservacao, Chaves.ResumoTitulo, Chaves.ResumoConfirmacao, Chaves.ConfirmacaoInvalida,
            Chaves.PedidoCancelado, Chaves.PedidoSucesso, Chaves.PedidoFalha
        }),
        ("Pedido do site", new[]
        {
            Chaves.PedidoSiteRecebido, Chaves.PedidoSiteNaoEncontrado, Chaves.PerguntaPersonalizacao,
            Chaves.PersonalizacaoRecebida, Chaves.FotoRecebida, Chaves.PersonalizacaoFim
        }),
        ("Meus pedidos e avisos", new[] { Chaves.MeusPedidosTitulo, Chaves.SemPedidos, Chaves.ErroTemporario, Chaves.OpcaoInvalida }),
    };

    public static readonly IReadOnlyList<MensagemBotDefinicao> Todas = new[]
    {
        new MensagemBotDefinicao(Chaves.Menu, "Menu principal",
            "Quando uma cliente já cadastrada puxa conversa, escolhe uma opção inválida ou volta ao menu. A ordem das opções não muda.",
            "Oi, {nome}! Sou o atendimento automático da *Delícias da Nilda* 🎂 Escolha uma opção:\n\n1. Fazer um pedido (ver sabores)\n2. Ver meus pedidos\n3. Falar com a Nilda\n\nResponda com o número da opção.",
            new[] { "nome" }, Nenhum),
        new MensagemBotDefinicao(Chaves.BotDesativado, "Atendimento automático desligado",
            "Quando a cliente escreve e o atendimento automático está desligado (enviada uma vez).",
            "No momento o atendimento automático está desativado. A Nilda vai te responder por aqui assim que puder.",
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

        new MensagemBotDefinicao(Chaves.PerguntaNome, "Boas-vindas a número novo (pergunta o nome)",
            "Primeira mensagem para um número que ainda não tem cadastro. O cadastro é feito uma vez só.",
            "Olá! Sou o atendimento automático da *Delícias da Nilda* 🎂 Vi que é sua primeira vez por aqui. Qual é o seu nome?",
            Nenhum, Nenhum),
        new MensagemBotDefinicao(Chaves.NomeInvalido, "Nome inválido",
            "Quando o nome informado é curto demais.",
            "Por favor, me diga seu nome (nome e sobrenome).",
            Nenhum, Nenhum),
        new MensagemBotDefinicao(Chaves.PerguntaCep, "Pergunta do CEP",
            "Depois do nome, no primeiro cadastro.",
            "Prazer, {nome}! Qual é o seu CEP? É só para a Nilda saber a região da entrega.",
            new[] { "nome" }, Nenhum),
        new MensagemBotDefinicao(Chaves.CepInvalido, "CEP inválido",
            "Quando o CEP não tem 8 números.",
            "Não entendi o CEP. Digite os 8 números, por exemplo 14020-260.",
            Nenhum, Nenhum),
        new MensagemBotDefinicao(Chaves.CadastroConcluido, "Cadastro concluído",
            "Quando o cadastro termina. É enviada junto com o menu.",
            "Pronto, {nome}, cadastro feito! Nas próximas vezes eu já te reconheço pelo número ✨",
            new[] { "nome" }, Nenhum),

        new MensagemBotDefinicao(Chaves.PerguntaSabor, "Pergunta do sabor",
            "Acima da lista numerada de sabores, durante o pedido.",
            "Escolha o sabor pelo número:",
            Nenhum, Nenhum),
        new MensagemBotDefinicao(Chaves.PerguntaPeso, "Pergunta do peso",
            "Depois que a cliente escolhe o sabor.",
            "Qual o tamanho, em quilos? Cada kg serve umas 10 fatias. (ex: 1,5)",
            Nenhum, Nenhum),
        new MensagemBotDefinicao(Chaves.PesoInvalido, "Peso inválido",
            "Quando o peso informado não é um número válido.",
            "Não entendi o peso. Informe em quilos, por exemplo 1,5.",
            Nenhum, Nenhum),
        new MensagemBotDefinicao(Chaves.PerguntaData, "Pergunta da data de entrega",
            "Acima da lista numerada de datas, durante o pedido.",
            "Escolha a data de entrega pelo número:",
            Nenhum, Nenhum),
        new MensagemBotDefinicao(Chaves.SemDatas, "Nenhuma data disponível",
            "Quando não há nenhuma data de entrega disponível.",
            "Não há datas disponíveis no momento. Tente novamente mais tarde ou fale com a Nilda (opção 4 do menu).",
            Nenhum, Nenhum),
        new MensagemBotDefinicao(Chaves.PerguntaObservacao, "Pergunta de observação",
            "Depois da data, perguntando se a cliente quer acrescentar alguma observação.",
            "É para alguma comemoração, ou tem alguma observação (tema, frase no bolo)? Se não, responda *não*.",
            Nenhum, Nenhum),
        new MensagemBotDefinicao(Chaves.ResumoTitulo, "Título do resumo do pedido",
            "Acima do resumo (sabor, peso, data...) que a cliente confere antes de confirmar.",
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
            "Quando o pedido feito pelo WhatsApp é registrado.",
            "Pedido *#P{numero}* registrado! Valor: {valor}.\n\nA Nilda vai conferir a agenda, confirmar e combinar o horário com você por aqui.",
            new[] { "numero", "valor" }, new[] { "numero" }),
        new MensagemBotDefinicao(Chaves.PedidoFalha, "Pedido não pôde ser registrado",
            "Quando o pedido não é aceito (por exemplo, data sem vaga). O motivo vem do sistema. É enviada junto com o menu.",
            "Não foi possível concluir o pedido: {erro}\n\nVocê pode tentar de novo escolhendo a opção *2* do menu.",
            new[] { "erro" }, new[] { "erro" }),

        new MensagemBotDefinicao(Chaves.PedidoSiteRecebido, "Pedido feito no site chegou",
            "Quando a cliente manda a mensagem pronta do site (com o código #P do pedido).",
            "Oba, recebi seu pedido *#P{numero}*! 🎂\n{resumo}\n\nA Nilda vai conferir a agenda e confirmar com você por aqui.",
            new[] { "numero", "resumo", "nome" }, new[] { "numero" }),
        new MensagemBotDefinicao(Chaves.PedidoSiteNaoEncontrado, "Código de pedido não encontrado",
            "Quando a mensagem tem um código #P que não é de um pedido deste número.",
            "Não encontrei o pedido #P{numero} no seu número. Confere o código, ou fale com a Nilda (opção 4 do menu).",
            new[] { "numero" }, Nenhum),
        new MensagemBotDefinicao(Chaves.PerguntaPersonalizacao, "Pergunta de personalização ou referência",
            "Logo depois de reconhecer um pedido feito no site.",
            "Tem alguma *personalização ou foto de referência* para o bolo? Pode mandar aqui, em texto ou foto. Se não tiver, responda *não*.",
            Nenhum, Nenhum),
        new MensagemBotDefinicao(Chaves.PersonalizacaoRecebida, "Personalização anotada",
            "Quando a cliente descreve a personalização em texto (fica anotada no pedido).",
            "Anotado no seu pedido! A Nilda vai ver 💕 Quer mandar mais alguma coisa? Se não, responda *não*.",
            Nenhum, Nenhum),
        new MensagemBotDefinicao(Chaves.FotoRecebida, "Foto de referência recebida",
            "Quando a cliente manda uma foto de referência (ela fica na conversa para a Nilda ver).",
            "Recebi a foto! Ela fica aqui na conversa para a Nilda ver. Quer mandar mais alguma coisa? Se não, responda *não*.",
            Nenhum, Nenhum),
        new MensagemBotDefinicao(Chaves.PersonalizacaoFim, "Fim da personalização",
            "Quando a cliente responde \"não\" depois da pergunta de personalização.",
            "Perfeito! Agora é só aguardar a confirmação da Nilda. Se precisar, envie *menu*.",
            Nenhum, Nenhum),

        new MensagemBotDefinicao(Chaves.MeusPedidosTitulo, "Título de \"meus pedidos\"",
            "Acima da lista de pedidos da cliente (opção 3 do menu).",
            "*Seus pedidos:*",
            Nenhum, Nenhum),
        new MensagemBotDefinicao(Chaves.SemPedidos, "Nenhum pedido em andamento",
            "Quando a cliente pede para ver os pedidos e não tem nenhum em andamento.",
            "Você não tem pedidos em andamento. Para fazer um, escolha a opção *2*.",
            Nenhum, Nenhum),

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
