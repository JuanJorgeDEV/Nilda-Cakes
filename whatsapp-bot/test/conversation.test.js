'use strict';

// Testes da maquina de conversa com uma API falsa (sem WhatsApp nem .NET).
// Rodar: npm test

const test = require('node:test');
const assert = require('node:assert/strict');
const { criarConversa, numeroDoJid, parsePeso } = require('../src/conversation');

const MENSAGENS = {
  menu: 'MENU {nome}',
  bot_desativado: 'DESATIVADO',
  falar_com_nilda: 'FALAR',
  sabores_titulo: 'SABORES',
  sem_sabores: 'SEM_SABORES',
  pergunta_nome: 'QUAL_NOME',
  nome_invalido: 'NOME_INVALIDO',
  pergunta_cep: 'CEP {nome}',
  cep_invalido: 'CEP_INVALIDO',
  cadastro_concluido: 'CADASTRADO {nome}',
  pergunta_sabor: 'QUAL_SABOR',
  pergunta_peso: 'QUAL_PESO',
  peso_invalido: 'PESO_INVALIDO',
  pergunta_data: 'QUAL_DATA',
  sem_datas: 'SEM_DATAS',
  pergunta_observacao: 'OBS?',
  resumo_titulo: 'RESUMO',
  resumo_confirmacao: 'CONFIRMA?',
  confirmacao_invalida: 'SIM_OU_NAO',
  pedido_cancelado: 'CANCELADO',
  pedido_sucesso: 'SUCESSO #P{numero} {valor}',
  pedido_falha: 'FALHA {erro}',
  pedido_site_recebido: 'SITE #P{numero} {resumo}',
  pedido_site_nao_encontrado: 'NAO_ACHEI #P{numero}',
  pergunta_personalizacao: 'PERSONALIZA?',
  personalizacao_recebida: 'ANOTADO',
  foto_recebida: 'FOTO_OK',
  personalizacao_fim: 'FIM',
  meus_pedidos_titulo: 'MEUS',
  sem_pedidos: 'SEM_PEDIDOS',
  erro_temporario: 'ERRO_TEMP',
  opcao_invalida: 'OPCAO_INVALIDA',
};

const NUMERO = '5516999990001';

function apiFalsa({ cadastrado = true, ativo = true } = {}) {
  const chamadas = { cadastros: [], pedidos: [], personalizacoes: [] };
  let cliente = cadastrado
    ? { id: 1, nome: 'Ana Paula', primeiroNome: 'Ana', temCep: true, pedidos: [{ codigo: '#P7', resumo: 'Ninho · 2 kg · sábado, 03/10', situacao: 'confirmado' }] }
    : null;

  const api = {
    chamadas,
    getStatus: async () => ({ ativo }),
    getMensagens: async () => MENSAGENS,
    getSabores: async () => [{ id: 10, nome: 'Ninho', precoPorKg: 75 }, { id: 11, nome: 'Oreo', precoPorKg: 70 }],
    getDatasDisponiveis: async () => [
      { data: '2026-10-02', texto: 'sexta-feira, 02/10', disponivel: false },
      { data: '2026-10-03', texto: 'sábado, 03/10', disponivel: true },
    ],
    getCliente: async () => {
      if (!cliente) {
        const err = new Error('404');
        err.response = { status: 404 };
        throw err;
      }
      return cliente;
    },
    cadastrarCliente: async (payload) => {
      chamadas.cadastros.push(payload);
      cliente = { id: 2, nome: payload.nomeCompleto, primeiroNome: payload.nomeCompleto.split(' ')[0], temCep: true, pedidos: [] };
      return { sucesso: true, cliente };
    },
    buscarCep: async () => ({ bairro: 'Jardim América', cidade: 'Ribeirão Preto' }),
    getPedido: async (id, numero) => (id === 27 && numero === NUMERO ? { id: 27, resumo: 'Alpino · 1,5 kg · sábado, 03/10' } : null),
    adicionarPersonalizacao: async (id, numero, texto) => { chamadas.personalizacoes.push({ id, numero, texto }); },
    criarPedido: async (payload) => {
      chamadas.pedidos.push(payload);
      return { sucesso: true, id: 99, valorTotal: 112.5 };
    },
  };
  return api;
}

test('numero novo faz cadastro (nome + CEP) antes do menu', async () => {
  const api = apiFalsa({ cadastrado: false });
  const { handleMessage } = criarConversa(api);

  assert.deepEqual(await handleMessage(NUMERO, 'oi'), ['QUAL_NOME']);
  assert.deepEqual(await handleMessage(NUMERO, 'Bi'), ['NOME_INVALIDO']);
  assert.deepEqual(await handleMessage(NUMERO, 'Bia Souza'), ['CEP Bia']);
  assert.deepEqual(await handleMessage(NUMERO, '1402'), ['CEP_INVALIDO']);
  assert.deepEqual(await handleMessage(NUMERO, '14020-260'), ['CADASTRADO Bia', 'MENU Bia']);

  assert.equal(api.chamadas.cadastros.length, 1);
  assert.deepEqual(api.chamadas.cadastros[0], {
    nomeCompleto: 'Bia Souza', whatsApp: NUMERO, cep: '14020260', bairro: 'Jardim América', cidade: 'Ribeirão Preto',
  });
});

test('cliente conhecido cai direto no menu com o nome', async () => {
  const { handleMessage } = criarConversa(apiFalsa());
  assert.deepEqual(await handleMessage(NUMERO, 'boa tarde'), ['MENU Ana']);
});

test('mensagem do site com #P reconhece o pedido e anota personalizacao', async () => {
  const api = apiFalsa();
  const { handleMessage } = criarConversa(api);

  const respostas = await handleMessage(NUMERO, 'Oi, Nilda! Acabei de fazer um pedido pelo site\n*Pedido #P27*\nBolo: Alpino');
  assert.deepEqual(respostas, ['SITE #P27 Alpino · 1,5 kg · sábado, 03/10', 'PERSONALIZA?']);

  assert.deepEqual(await handleMessage(NUMERO, 'quero escrito Parabéns Laura'), ['ANOTADO']);
  assert.deepEqual(await handleMessage(NUMERO, '', { tipo: 'imagem' }), ['FOTO_OK']);
  assert.deepEqual(await handleMessage(NUMERO, 'não'), ['FIM']);

  assert.deepEqual(api.chamadas.personalizacoes, [{ id: 27, numero: NUMERO, texto: 'quero escrito Parabéns Laura' }]);
  assert.equal(api.chamadas.pedidos.length, 0, 'pedido do site nao pode virar outro pedido');
});

test('codigo #P de pedido de outro numero nao e revelado', async () => {
  const { handleMessage } = criarConversa(apiFalsa());
  assert.deepEqual(await handleMessage(NUMERO, 'Pedido #P5'), ['NAO_ACHEI #P5']);
});

test('pedido pelo whatsapp: sabor, peso com virgula, data disponivel e confirmacao', async () => {
  const api = apiFalsa();
  const { handleMessage } = criarConversa(api);

  await handleMessage(NUMERO, 'oi');
  assert.match((await handleMessage(NUMERO, '2'))[0], /^QUAL_SABOR\n1\. Ninho/);
  assert.match((await handleMessage(NUMERO, '9'))[0], /^OPCAO_INVALIDA/);
  assert.deepEqual(await handleMessage(NUMERO, '1'), ['QUAL_PESO']);
  const datas = await handleMessage(NUMERO, '1,5 kg');
  assert.equal(datas[0], 'QUAL_DATA\n1. sábado, 03/10', 'so datas com vaga aparecem');
  assert.deepEqual(await handleMessage(NUMERO, '1'), ['OBS?']);
  assert.match((await handleMessage(NUMERO, 'não'))[0], /Tamanho: 1,5 kg[\s\S]*CONFIRMA\?$/);
  assert.deepEqual(await handleMessage(NUMERO, 'talvez'), ['SIM_OU_NAO']);
  const fim = await handleMessage(NUMERO, 'sim');
  assert.match(fim[0], /^SUCESSO #P99 R\$\s?112,50$/);

  assert.deepEqual(api.chamadas.pedidos[0], {
    whatsApp: NUMERO, nomeCompleto: 'Ana Paula', saborId: 10, pesoKg: 1.5, dataEntrega: '2026-10-03', observacoes: undefined,
  });
});

test('meus pedidos lista os pedidos do cliente', async () => {
  const { handleMessage } = criarConversa(apiFalsa());
  await handleMessage(NUMERO, 'oi');
  const [lista] = await handleMessage(NUMERO, '3');
  assert.match(lista, /^MEUS\n\n\*#P7\* Ninho · 2 kg · sábado, 03\/10\n {3}confirmado$/);
});

test('falar com a Nilda silencia o bot ate "menu"', async () => {
  const { handleMessage } = criarConversa(apiFalsa());
  await handleMessage(NUMERO, 'oi');
  assert.deepEqual(await handleMessage(NUMERO, '4'), ['FALAR']);
  assert.deepEqual(await handleMessage(NUMERO, 'oi, tudo bem?'), []);
  assert.deepEqual(await handleMessage(NUMERO, 'Menu'), ['MENU Ana']);
});

test('bot desligado avisa uma vez so', async () => {
  const { handleMessage } = criarConversa(apiFalsa({ ativo: false }));
  assert.deepEqual(await handleMessage(NUMERO, 'oi'), ['DESATIVADO']);
  assert.deepEqual(await handleMessage(NUMERO, 'oi?'), []);
});

test('utilitarios', () => {
  assert.equal(numeroDoJid('5516999990001@s.whatsapp.net'), '5516999990001');
  assert.equal(numeroDoJid('5516999990001:12@s.whatsapp.net'), '5516999990001');
  assert.equal(parsePeso('1,5'), 1.5);
  assert.equal(parsePeso('2kg'), 2);
  assert.equal(parsePeso('abc'), null);
});
