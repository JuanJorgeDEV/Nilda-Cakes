'use strict';

/**
 * Maquina de estados da conversa, por numero de telefone.
 *
 * Guardada em memoria (Map), sem persistencia em banco — se o processo
 * reiniciar, quem estava no meio de um pedido precisa comecar de novo.
 * Nenhuma regra de negocio (preco, antecedencia, capacidade, etc.) e
 * decidida aqui: este modulo so coleta respostas do usuario e chama a API
 * (via apiClient) nos pontos certos, repassando o que a API responder.
 */

const api = require('./apiClient');

/** @type {Map<string, any>} jid -> estado da conversa */
const sessions = new Map();

const STEPS = {
  MENU: 'MENU',
  PEDIDO_NOME: 'PEDIDO_NOME',
  PEDIDO_CONFIRMA_WHATSAPP: 'PEDIDO_CONFIRMA_WHATSAPP',
  PEDIDO_SABOR: 'PEDIDO_SABOR',
  PEDIDO_PESO: 'PEDIDO_PESO',
  PEDIDO_DATA: 'PEDIDO_DATA',
  PEDIDO_OBSERVACAO: 'PEDIDO_OBSERVACAO',
  PEDIDO_CONFIRMACAO: 'PEDIDO_CONFIRMACAO',
};

const MENU_TEXT =
  'Ola! Sou o atendimento automatico da *Delicias da Nilda*. Escolha uma opcao:\n\n' +
  '1. Ver sabores disponiveis\n' +
  '2. Fazer um pedido\n' +
  '3. Falar com a Nilda\n\n' +
  'Responda com o numero da opcao desejada.';

const BOT_INATIVO_TEXT =
  'No momento o atendimento automatico esta desativado. Por favor, entre em ' +
  'contato diretamente com a Delicias da Nilda para fazer seu pedido.';

function novaSessao() {
  return { step: STEPS.MENU, dados: {}, cache: {}, avisouInativo: false };
}

function getSessao(jid) {
  let sessao = sessions.get(jid);
  if (!sessao) {
    sessao = novaSessao();
    sessao.jid = jid;
    sessions.set(jid, sessao);
  }
  return sessao;
}

function resetSessao(jid) {
  sessions.set(jid, novaSessao());
}

function formatarWhatsAppFromJid(jid) {
  // jid tipico: "5547999999999@s.whatsapp.net"
  const numero = jid.split('@')[0].replace(/[^0-9]/g, '');
  return `+${numero}`;
}

function formatarMoeda(valor) {
  return Number(valor).toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });
}

function parsePositiveNumber(texto) {
  const normalizado = texto.trim().replace(',', '.');
  const valor = Number(normalizado);
  if (!Number.isFinite(valor) || valor <= 0) {
    return null;
  }
  return valor;
}

function parseOpcao(texto, min, max) {
  const valor = Number(texto.trim());
  if (!Number.isInteger(valor) || valor < min || valor > max) {
    return null;
  }
  return valor;
}

/**
 * Processa uma mensagem recebida e retorna o texto (ou lista de textos) de
 * resposta a enviar. Nunca lanca excecao por entrada invalida do usuario —
 * qualquer entrada inesperada reexibe as opcoes validas do passo atual.
 *
 * @param {string} jid identificador do remetente no Baileys
 * @param {string} textoBruto conteudo da mensagem recebida
 * @returns {Promise<string[]>} lista de mensagens a enviar, em ordem
 */
async function handleMessage(jid, textoBruto) {
  const texto = (textoBruto || '').trim();

  // RN: bot so responde se estiver ativo no painel ADM (config remota, sem
  // regra de negocio reimplementada aqui — so consulta GET /status).
  let status;
  try {
    status = await api.getStatus();
  } catch (err) {
    console.error('[conversation] Falha ao consultar /status:', err.message);
    return ['Desculpe, o sistema esta temporariamente indisponivel. Tente novamente em instantes.'];
  }

  if (!status.ativo) {
    const sessao = getSessao(jid);
    if (!sessao.avisouInativo) {
      sessao.avisouInativo = true;
      return [BOT_INATIVO_TEXT];
    }
    return [];
  }

  const sessao = getSessao(jid);
  sessao.avisouInativo = false;

  switch (sessao.step) {
    case STEPS.MENU:
      return handleMenu(jid, sessao, texto);
    case STEPS.PEDIDO_NOME:
      return handlePedidoNome(sessao, texto);
    case STEPS.PEDIDO_CONFIRMA_WHATSAPP:
      return handlePedidoConfirmaWhatsApp(jid, sessao, texto);
    case STEPS.PEDIDO_SABOR:
      return handlePedidoSabor(sessao, texto);
    case STEPS.PEDIDO_PESO:
      return handlePedidoPeso(sessao, texto);
    case STEPS.PEDIDO_DATA:
      return handlePedidoData(sessao, texto);
    case STEPS.PEDIDO_OBSERVACAO:
      return handlePedidoObservacao(sessao, texto);
    case STEPS.PEDIDO_CONFIRMACAO:
      return handlePedidoConfirmacao(sessao, texto);
    default:
      resetSessao(jid);
      return [MENU_TEXT];
  }
}

async function handleMenu(jid, sessao, texto) {
  const opcao = parseOpcao(texto, 1, 3);

  if (opcao === null) {
    return [MENU_TEXT];
  }

  if (opcao === 1) {
    return listarSabores();
  }

  if (opcao === 2) {
    sessao.step = STEPS.PEDIDO_NOME;
    sessao.dados = {};
    return ['Vamos comecar seu pedido! Qual e o seu nome completo?'];
  }

  // opcao === 3
  return [
    'Certo! A Nilda vai te responder por aqui assim que possivel. ' +
      'Nenhuma acao automatica sera feita a partir de agora nesta conversa.',
  ];
}

async function listarSabores() {
  try {
    const sabores = await api.getSabores();
    if (!sabores.length) {
      return ['No momento nao ha sabores disponiveis cadastrados. Tente novamente mais tarde.'];
    }
    const linhas = sabores.map((s) => `- ${s.nome}: ${formatarMoeda(s.precoPorKg)}/kg`);
    return ['*Sabores disponiveis:*\n' + linhas.join('\n'), MENU_TEXT];
  } catch (err) {
    console.error('[conversation] Falha ao buscar sabores:', err.message);
    return ['Nao consegui buscar os sabores agora. Tente novamente em instantes.'];
  }
}

function handlePedidoNome(sessao, texto) {
  if (!texto || texto.length < 3) {
    return ['Por favor, informe seu nome completo (nome e sobrenome).'];
  }
  sessao.dados.nomeCompleto = texto;
  sessao.step = STEPS.PEDIDO_CONFIRMA_WHATSAPP;
  return [
    `Obrigado, ${texto}! Podemos usar este numero de WhatsApp para o pedido? ` +
      'Responda *sim* para confirmar ou digite outro numero (com DDD).',
  ];
}

async function handlePedidoConfirmaWhatsApp(jid, sessao, texto) {
  const textoLower = texto.toLowerCase();

  if (textoLower === 'sim' || textoLower === 's') {
    sessao.dados.whatsApp = formatarWhatsAppFromJid(jid);
  } else if (/^[0-9()+\-.\s]{8,}$/.test(texto)) {
    sessao.dados.whatsApp = texto;
  } else {
    return ['Nao entendi. Responda *sim* para usar este numero, ou digite outro numero de WhatsApp valido.'];
  }

  return prepararEscolhaSabor(sessao);
}

async function prepararEscolhaSabor(sessao) {
  try {
    const sabores = await api.getSabores();
    if (!sabores.length) {
      return ['No momento nao ha sabores disponiveis para pedido. Tente novamente mais tarde.'];
    }
    sessao.cache.sabores = sabores;
    sessao.step = STEPS.PEDIDO_SABOR;
    const linhas = sabores.map((s, i) => `${i + 1}. ${s.nome} (${formatarMoeda(s.precoPorKg)}/kg)`);
    return ['Escolha o sabor pelo numero:\n' + linhas.join('\n')];
  } catch (err) {
    console.error('[conversation] Falha ao buscar sabores:', err.message);
    return ['Nao consegui buscar os sabores agora. Tente novamente em instantes.'];
  }
}

function handlePedidoSabor(sessao, texto) {
  const sabores = sessao.cache.sabores || [];
  const opcao = parseOpcao(texto, 1, sabores.length);

  if (opcao === null) {
    const linhas = sabores.map((s, i) => `${i + 1}. ${s.nome} (${formatarMoeda(s.precoPorKg)}/kg)`);
    return ['Opcao invalida. Escolha o sabor pelo numero:\n' + linhas.join('\n')];
  }

  const saborEscolhido = sabores[opcao - 1];
  sessao.dados.saborId = saborEscolhido.id;
  sessao.dados.saborNome = saborEscolhido.nome;
  sessao.dados.precoPorKg = saborEscolhido.precoPorKg;
  sessao.step = STEPS.PEDIDO_PESO;
  return ['Qual o peso desejado, em quilos? (ex: 1.5)'];
}

function handlePedidoPeso(sessao, texto) {
  const peso = parsePositiveNumber(texto);
  if (peso === null) {
    return ['Peso invalido. Informe um numero positivo em quilos (ex: 1.5).'];
  }
  sessao.dados.pesoKg = peso;
  return prepararEscolhaData(sessao);
}

async function prepararEscolhaData(sessao) {
  try {
    const datas = await api.getDatasDisponiveis(10);
    const disponiveis = datas.filter((d) => d.disponivel);
    if (!disponiveis.length) {
      return ['Nao ha datas disponiveis no momento. Tente novamente mais tarde ou fale com a Nilda (opcao 3 no menu).'];
    }
    sessao.cache.datas = disponiveis;
    sessao.step = STEPS.PEDIDO_DATA;
    const linhas = disponiveis.map((d, i) => `${i + 1}. ${d.data}`);
    return ['Escolha a data de entrega pelo numero:\n' + linhas.join('\n')];
  } catch (err) {
    console.error('[conversation] Falha ao buscar datas disponiveis:', err.message);
    return ['Nao consegui buscar as datas disponiveis agora. Tente novamente em instantes.'];
  }
}

function handlePedidoData(sessao, texto) {
  const datas = sessao.cache.datas || [];
  const opcao = parseOpcao(texto, 1, datas.length);

  if (opcao === null) {
    const linhas = datas.map((d, i) => `${i + 1}. ${d.data}`);
    return ['Opcao invalida. Escolha a data pelo numero:\n' + linhas.join('\n')];
  }

  sessao.dados.dataEntrega = datas[opcao - 1].data;
  sessao.step = STEPS.PEDIDO_OBSERVACAO;
  return ['Deseja adicionar alguma observacao ao pedido? Se nao, responda *nao*.'];
}

function handlePedidoObservacao(sessao, texto) {
  const textoLower = texto.toLowerCase();
  sessao.dados.observacoes = textoLower === 'nao' || textoLower === 'não' || textoLower === 'n' ? undefined : texto;
  sessao.step = STEPS.PEDIDO_CONFIRMACAO;

  const d = sessao.dados;
  const resumo =
    '*Resumo do pedido:*\n' +
    `Nome: ${d.nomeCompleto}\n` +
    `WhatsApp: ${d.whatsApp}\n` +
    `Sabor: ${d.saborNome}\n` +
    `Peso: ${d.pesoKg} kg\n` +
    `Data de entrega: ${d.dataEntrega}\n` +
    `Observacoes: ${d.observacoes || '(nenhuma)'}\n\n` +
    'Confirma o pedido? Responda *sim* ou *nao*.';

  return [resumo];
}

async function handlePedidoConfirmacao(sessao, texto) {
  const textoLower = texto.toLowerCase();

  if (textoLower === 'nao' || textoLower === 'não' || textoLower === 'n') {
    resetSessao(sessao.jid);
    sessao.step = STEPS.MENU;
    return ['Pedido cancelado. Nenhuma informacao foi enviada.', MENU_TEXT];
  }

  if (textoLower !== 'sim' && textoLower !== 's') {
    return ['Responda *sim* para confirmar o pedido ou *nao* para cancelar.'];
  }

  const d = sessao.dados;
  try {
    const resultado = await api.criarPedido({
      nomeCompleto: d.nomeCompleto,
      whatsApp: d.whatsApp,
      saborId: d.saborId,
      pesoKg: d.pesoKg,
      dataEntrega: d.dataEntrega,
      observacoes: d.observacoes,
    });

    if (resultado.sucesso) {
      sessao.step = STEPS.MENU;
      sessao.dados = {};
      return [
        `Pedido #${resultado.id} registrado com sucesso! Valor total: ${formatarMoeda(resultado.valorTotal)}.\n\n` +
          'A confirmacao final e o horario de entrega serao combinados diretamente com a Nilda.',
      ];
    }

    // 400 — mensagem de erro ja pronta em portugues, vinda da API.
    sessao.step = STEPS.MENU;
    sessao.dados = {};
    return [
      `Nao foi possivel concluir o pedido: ${resultado.erro}`,
      'Voce pode tentar novamente respondendo *2* para comecar um novo pedido.',
      MENU_TEXT,
    ];
  } catch (err) {
    console.error('[conversation] Falha ao criar pedido:', err.message);
    return ['Nao foi possivel enviar seu pedido agora. Tente novamente em instantes.'];
  }
}

module.exports = {
  STEPS,
  MENU_TEXT,
  handleMessage,
  getSessao,
  resetSessao,
};
