# Bot de WhatsApp — Delicias da Nilda

Servico Node.js separado que conversa com os clientes no WhatsApp usando
[Baileys](https://github.com/WhiskeySockets/Baileys) e orquestra a conversa
por um menu numerado simples. **Nenhuma regra de negocio (RN01-RN16) e
implementada aqui** — toda decisao (preco, antecedencia minima, capacidade
diaria, criacao do pedido) e feita pela API interna do backend .NET, em
`src/DeliciasDaNilda/Endpoints/WhatsAppBotEndpoints.cs`. Este bot apenas:

1. Le a mensagem do cliente.
2. Decide, por um estado de conversa simples em memoria, qual pergunta fazer
   em seguida ou qual endpoint chamar.
3. Repassa para o cliente exatamente o que a API respondeu (inclusive
   mensagens de erro, que ja vem prontas em portugues).

## Aviso importante sobre o Baileys

Baileys **nao e uma biblioteca oficial do WhatsApp** — ela se conecta
simulando um cliente WhatsApp Web. Isso significa:

- Use um **numero de WhatsApp secundario/dedicado exclusivamente a loja**,
  nunca o numero pessoal da Nilda ou de qualquer outra pessoa. Ha risco real
  de o WhatsApp banir o numero por uso de automacao nao oficial.
- Evite enviar mensagens em volume muito alto ou identico para muitos
  numeros em pouco tempo (risco adicional de bloqueio).
- Este projeto e de extensao universitaria; trate o bot como um piloto e
  monitore o numero regularmente.

## Requisitos

- Node.js 18 ou superior.
- A API do backend (.NET) rodando e acessivel (por padrao,
  `http://localhost:5138`), com `InternalApi:WhatsAppBotKey` configurado em
  `appsettings.Development.json`.

## Instalacao

```bash
cd whatsapp-bot
npm install
```

## Configuracao

Copie o arquivo de exemplo e preencha os valores:

```bash
cp .env.example .env
```

Variaveis:

| Variavel           | Obrigatoria | Descricao                                                                 |
|--------------------|-------------|----------------------------------------------------------------------------|
| `API_BASE_URL`     | sim         | URL base da API .NET (ex.: `http://localhost:5138`).                      |
| `API_KEY`          | sim         | Mesmo valor de `InternalApi:WhatsAppBotKey` configurado no backend.        |
| `POLL_INTERVAL_MS` | nao         | Reservado para telemetria/verificacoes periodicas futuras (default 60000).|

## Executando

```bash
npm start
# ou
node index.js
```

Na primeira execucao (ou sempre que a sessao expirar/for deslogada), o
terminal vai exibir um **QR code**. Abra o WhatsApp no celular dedicado a
loja → **Aparelhos conectados** → **Conectar um aparelho** → escaneie o QR
code exibido no terminal.

Apos parear uma vez, a sessao fica salva na pasta `auth_info/` (criada
automaticamente, **nao deve ser versionada** — ja esta no `.gitignore`), e
os proximos `npm start` reconectam sozinhos, sem precisar escanear de novo.

### Reconectando se a sessao cair ou for deslogada

Se o bot for desconectado permanentemente (por exemplo, apos um logout
manual no celular, ou se o WhatsApp invalidar a sessao):

1. Pare o processo (`Ctrl+C`).
2. Apague a pasta `auth_info/`.
3. Rode `npm start` novamente e escaneie o novo QR code.

## Comportamento quando o atendimento automatico esta desativado

O bot consulta `GET /internal/whatsapp-bot/status` a cada mensagem recebida.
Se `ativo` for `false` (desligado pelo painel ADM), o bot **responde uma
unica vez por conversa** avisando que o atendimento automatico esta
desativado e para procurar a loja diretamente, e depois fica em silencio
para novas mensagens da mesma pessoa ate o atendimento ser reativado (para
nao insistir em mandar o mesmo aviso a cada mensagem).

## Fluxo da conversa

Os textos vem do painel (WhatsApp → Mensagens do bot); a ordem das perguntas
fica no codigo. Detalhes em [docs/atendimento-whatsapp.md](../docs/atendimento-whatsapp.md).

1. **Mensagem com codigo `#P123`** (a mensagem pronta que o site monta): o
   bot confere que o pedido e deste numero, responde com o resumo e pergunta
   por personalizacao ou foto de referencia. Texto fica anotado no pedido;
   foto fica na conversa. **Nao cria outro pedido.**
2. **Numero novo**: cadastro primeiro (nome e CEP; bairro e cidade pelo
   ViaCEP), depois o menu.
3. **Cliente conhecido**: menu `1. Ver sabores` / `2. Fazer um pedido` /
   `3. Ver meus pedidos` / `4. Falar com a Nilda`.
   - Pedido: sabor, peso (aceita `1,5`), data com vaga
     (`GET /datas-disponiveis`), observacao, resumo e confirmacao antes de
     chamar `POST /pedidos`.
   - Falar com a Nilda: o bot fica em silencio com essa pessoa ate ela
     mandar *menu*.

Qualquer resposta invalida reexibe as opcoes validas do passo atual, sem
travar a conversa. A qualquer momento, *menu* volta ao inicio.

## Testes

```bash
npm test
```

Testa a maquina de conversa com uma API falsa (sem WhatsApp e sem o .NET).

## Estrutura de arquivos

```
whatsapp-bot/
├── index.js              # conexao Baileys, QR code, roteamento de mensagens
├── src/
│   ├── apiClient.js      # chamadas HTTP para a API interna .NET (e ViaCEP)
│   └── conversation.js   # maquina de estados da conversa (em memoria)
├── test/
│   └── conversation.test.js
├── package.json
├── .env.example
├── auth_info/            # sessao do WhatsApp (criada em runtime, git-ignored)
└── README.md
```
