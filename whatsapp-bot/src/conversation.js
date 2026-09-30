'use strict';

/**
 * Maquina de estados da conversa, por numero de telefone.
 *
 * Guardada em memoria (Map), sem persistencia em banco — se o processo
 * reiniciar, quem estava no meio de um pedido precisa comecar de novo.
 * Nenhuma regra de negocio (preco, antecedencia, capacidade, etc.) e
 * decidida aqui: este modulo so conduz a conversa e chama a API interna
 * (.NET) nos pontos certos, repassando o que a API responder.
 *
 * Os TEXTOS vem da API (GET /mensagens), editaveis pela Nilda no painel.
 * A ordem das perguntas e fixa aqui.
 *
 * Caminhos da conversa:
 *  - Numero novo: cadastro (nome + CEP) antes de qualquer coisa.
 *  - Mensagem com "#P123" (enviada pelo site): reconhece o pedido feito no
 *    site e pergunta por personalizacao/foto de referencia — nao cria outro
 *    pedido.
 *  - Cliente conhecido: menu (sabores, fazer pedido, meus pedidos, falar
 *    com a Nilda).
 */

const STEPS = {
  MENU: 'MENU',
  CADASTRO_NOME: 'CADASTRO_NOME',
  CADASTRO_CEP: 'CADASTRO_CEP',
  PEDIDO_SABOR: 'PEDIDO_SABOR',
  PEDIDO_PESO: 'PEDIDO_PESO',
  PEDIDO_DATA: 'PEDIDO_DATA',
  PEDIDO_OBSERVACAO: 'PEDIDO_OBSERVACAO',
  PEDIDO_CONFIRMACAO: 'PEDIDO_CONFIRMACAO',
  PERSONALIZACAO: 'PERSONALIZACAO',
  SILENCIO: 'SILENCIO',
};

// Usado so se a API de mensagens estiver fora do ar.
const TEXTO_INDISPONIVEL = 'Desculpe, o sistema esta temporariamente indisponivel. Tente novamente em instantes.';
const CACHE_MENSAGENS_MS = 60_000;
const SABORES_POR_PAGINA = 8;

const RESPOSTAS_NAO = ['nao', 'não', 'n', 'nada', 'nenhum', 'nenhuma', 'sem'];
const RESPOSTAS_SIM = ['sim', 's', 'ok', 'isso', 'confirmo', 'pode'];

function normalizarResposta(texto) {
  return (texto || '').trim().toLowerCase().replace(/[.!]+$/, '');
}

function ehNao(texto) {
  return RESPOSTAS_NAO.includes(normalizarResposta(texto));
}

function ehSim(texto) {
  return RESPOSTAS_SIM.includes(normalizarResposta(texto));
}

function formatarMoeda(valor) {
  return Number(valor).toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });
}

/** Aceita "1,5", "1.5", "1,5 kg", "2kg". */
function parsePeso(texto) {
  const limpo = (texto || '').toLowerCase().replace('kg', '').replace(',', '.').trim();
  const valor = Number(limpo);
  return Number.isFinite(valor) && valor > 0 ? valor : null;
}

function parseOpcao(texto, min, max) {
  const valor = Number((texto || '').trim());
  if (!Number.isInteger(valor) || valor < min || valor > max) {
    return null;
  }
  return valor;
}

/** "5516999990001@s.whatsapp.net" -> "5516999990001" */
function numeroDoJid(jid) {
  return (jid || '').split('@')[0].split(':')[0].replace(/[^0-9]/g, '');
}

/**
 * Cria a conversa ligada a uma API (injetada para facilitar testes).
 * @param {object} api modulo ./apiClient (ou um dublê nos testes)
 */
function criarConversa(api, { siteUrl } = {}) {
  /** @type {Map<string, any>} numero -> estado da conversa */
  const sessoes = new Map();
  let mensagens = null;
  let mensagensCarregadasEm = 0;

  function novaSessao() {
    return { step: STEPS.MENU, dados: {}, cache: {}, cliente: null, avisouInativo: false };
  }

  function getSessao(numero) {
    if (!sessoes.has(numero)) sessoes.set(numero, novaSessao());
    return sessoes.get(numero);
  }

  async function carregarMensagens() {
    const agora = Date.now();
    if (mensagens && agora - mensagensCarregadasEm < CACHE_MENSAGENS_MS) return mensagens;
    try {
      mensagens = await api.getMensagens();
      mensagensCarregadasEm = agora;
    } catch (err) {
      console.error('[conversation] Falha ao buscar mensagens:', err.message);
      if (!mensagens) throw err;
    }
    return mensagens;
  }

  /** Texto da mensagem configurada, trocando {nome}, {numero}... */
  function msg(chave, vars = {}) {
    let texto = (mensagens && mensagens[chave]) || '';
    for (const [nome, valor] of Object.entries(vars)) {
      texto = texto.split(`{${nome}}`).join(valor == null ? '' : String(valor));
    }
    return texto;
  }

  function menu(sessao) {
    return msg('menu', { nome: sessao.cliente ? sessao.cliente.primeiroNome : '' });
  }

  async function buscarCliente(numero, sessao) {
    try {
      sessao.cliente = await api.getCliente(numero);
    } catch (err) {
      if (err.response && err.response.status === 404) {
        sessao.cliente = null;
      } else {
        throw err;
      }
    }
    return sessao.cliente;
  }

  /**
   * Processa uma mensagem recebida e devolve as respostas a enviar, em
   * ordem. Nunca lanca excecao por entrada invalida — reexibe as opcoes.
   *
   * @param {string} numero telefone do remetente (so digitos, com DDI)
   * @param {string} textoBruto texto da mensagem (legenda, se for foto)
   * @param {{tipo?: 'texto'|'imagem'}} [opcoes]
   * @returns {Promise<Array<string|{imagem: string, legenda: string}>>}
   */
  async function handleMessage(numero, textoBruto, opcoes = {}) {
    const respostas = await processar(numero, textoBruto, opcoes);
    return comLinkDoSite(respostas);
  }

  /** Rodape com o link do site na ultima mensagem de texto (quem preferir pedir por la). */
  function comLinkDoSite(respostas) {
    if (!siteUrl || !respostas.length) return respostas;
    const ultimo = respostas.length - 1;
    if (typeof respostas[ultimo] !== 'string') return respostas;
    const copia = [...respostas];
    copia[ultimo] = `${copia[ultimo]}\n\n🌐 Prefere pedir pelo site? ${siteUrl}`;
    return copia;
  }

  async function processar(numero, textoBruto, opcoes = {}) {
    const texto = (textoBruto || '').trim();
    const tipo = opcoes.tipo || 'texto';

    let status;
    try {
      status = await api.getStatus();
      await carregarMensagens();
    } catch (err) {
      console.error('[conversation] API indisponivel:', err.message);
      return [TEXTO_INDISPONIVEL];
    }

    const sessao = getSessao(numero);

    if (!status.ativo) {
      if (!sessao.avisouInativo) {
        sessao.avisouInativo = true;
        return [msg('bot_desativado')];
      }
      return [];
    }
    sessao.avisouInativo = false;

    try {
      // "Falar com a Nilda": silencio ate a cliente mandar "menu".
      if (sessao.step === STEPS.SILENCIO) {
        if (normalizarResposta(texto) !== 'menu') return [];
        sessao.step = STEPS.MENU;
      }

      // Pedido feito no site: a mensagem pronta traz o codigo #P123.
      const codigoSite = texto.match(/#P(\d+)/i);
      if (codigoSite) {
        return await handlePedidoSite(numero, sessao, Number(codigoSite[1]));
      }

      if (tipo === 'imagem') {
        return sessao.step === STEPS.PERSONALIZACAO ? await handlePersonalizacao(numero, sessao, texto, tipo) : [];
      }

      // Cadastro em andamento.
      if (sessao.step === STEPS.CADASTRO_NOME) return handleCadastroNome(sessao, texto);
      if (sessao.step === STEPS.CADASTRO_CEP) return await handleCadastroCep(numero, sessao, texto);

      // Numero novo (ou sem CEP): cadastro antes de tudo.
      if (!sessao.cliente || sessao.step === STEPS.MENU) {
        await buscarCliente(numero, sessao);
      }
      if (!sessao.cliente) {
        sessao.step = STEPS.CADASTRO_NOME;
        sessao.dados = {};
        return [msg('pergunta_nome')];
      }
      if (!sessao.cliente.temCep) {
        sessao.step = STEPS.CADASTRO_CEP;
        sessao.dados = { nomeCompleto: sessao.cliente.nome };
        return [msg('pergunta_cep', { nome: sessao.cliente.primeiroNome })];
      }

      if (normalizarResposta(texto) === 'menu') {
        sessao.step = STEPS.MENU;
        sessao.dados = {};
        return [menu(sessao)];
      }

      switch (sessao.step) {
        case STEPS.MENU:
          return await handleMenu(sessao, texto);
        case STEPS.PEDIDO_SABOR:
          return handlePedidoSabor(sessao, texto);
        case STEPS.PEDIDO_PESO:
          return await handlePedidoPeso(sessao, texto);
        case STEPS.PEDIDO_DATA:
          return handlePedidoData(sessao, texto);
        case STEPS.PEDIDO_OBSERVACAO:
          return handlePedidoObservacao(sessao, texto);
        case STEPS.PEDIDO_CONFIRMACAO:
          return await handlePedidoConfirmacao(numero, sessao, texto);
        case STEPS.PERSONALIZACAO:
          return await handlePersonalizacao(numero, sessao, texto, tipo);
        default:
          sessao.step = STEPS.MENU;
          return [menu(sessao)];
      }
    } catch (err) {
      console.error('[conversation] Erro ao falar com a API:', err.message);
      return [msg('erro_temporario')];
    }
  }

  // ---- Cadastro -----------------------------------------------------------

  function handleCadastroNome(sessao, texto) {
    if (!texto || texto.length < 3 || /^\d+$/.test(texto)) {
      return [msg('nome_invalido')];
    }
    sessao.dados.nomeCompleto = texto;
    sessao.step = STEPS.CADASTRO_CEP;
    return [msg('pergunta_cep', { nome: texto.split(' ')[0] })];
  }

  async function handleCadastroCep(numero, sessao, texto) {
    const cep = texto.replace(/\D/g, '');
    if (cep.length !== 8) {
      return [msg('cep_invalido')];
    }

    // Bairro e cidade pelo ViaCEP (opcional: se falhar, segue so com o CEP).
    const endereco = api.buscarCep ? await api.buscarCep(cep) : null;

    const resultado = await api.cadastrarCliente({
      nomeCompleto: sessao.dados.nomeCompleto,
      whatsApp: numero,
      cep,
      bairro: endereco ? endereco.bairro : null,
      cidade: endereco ? endereco.cidade : null,
    });

    if (!resultado.sucesso) {
      return [resultado.erro];
    }

    sessao.cliente = resultado.cliente;
    sessao.step = STEPS.MENU;
    sessao.dados = {};
    return [msg('cadastro_concluido', { nome: sessao.cliente.primeiroNome }), menu(sessao)];
  }

  // ---- Menu ---------------------------------------------------------------

  async function handleMenu(sessao, texto) {
    const opcao = parseOpcao(texto, 1, 3);
    if (opcao === null) return [menu(sessao)];

    if (opcao === 1) return await prepararEscolhaSabor(sessao);
    if (opcao === 2) return listarMeusPedidos(sessao);

    sessao.step = STEPS.SILENCIO;
    return [msg('falar_com_nilda')];
  }

  function listarMeusPedidos(sessao) {
    const pedidos = (sessao.cliente && sessao.cliente.pedidos) || [];
    if (!pedidos.length) return [msg('sem_pedidos')];
    const linhas = pedidos.map((p) => `*${p.codigo}* ${p.resumo}\n   ${p.situacao}`);
    return [msg('meus_pedidos_titulo') + '\n\n' + linhas.join('\n\n')];
  }

  // ---- Pedido pelo WhatsApp -----------------------------------------------

  function textoPaginaSabores(sessao) {
    const sabores = sessao.cache.sabores;
    const totalPaginas = Math.ceil(sabores.length / SABORES_POR_PAGINA);
    const pagina = Math.min(Math.max(sessao.cache.pagina || 0, 0), totalPaginas - 1);
    sessao.cache.pagina = pagina;
    const ini = pagina * SABORES_POR_PAGINA;
    const linhas = sabores
      .slice(ini, ini + SABORES_POR_PAGINA)
      .map((s, i) => `${ini + i + 1}. ${s.nome} (${formatarMoeda(s.precoPorKg)}/kg)`);
    const dicas = ['Responda com o número do sabor'];
    if (sabores.some((s) => s.imagemUrl)) dicas.push('*foto 3* para ver a foto do sabor 3');
    if (pagina < totalPaginas - 1) dicas.push('*mais* para ver os próximos');
    if (pagina > 0) dicas.push('*voltar* para os anteriores');
    const rodape = totalPaginas > 1 ? `Página ${pagina + 1} de ${totalPaginas}\n` : '';
    return linhas.join('\n') + '\n\n' + rodape + dicas.join(' · ') + '.';
  }

  async function prepararEscolhaSabor(sessao) {
    const sabores = await api.getSabores();
    if (!sabores.length) return [msg('sem_sabores')];
    sessao.cache.sabores = sabores;
    sessao.cache.pagina = 0;
    sessao.dados = {};
    sessao.step = STEPS.PEDIDO_SABOR;
    return [msg('pergunta_sabor') + '\n' + textoPaginaSabores(sessao)];
  }

  function handlePedidoSabor(sessao, texto) {
    const sabores = sessao.cache.sabores || [];
    const t = normalizarResposta(texto);

    if (['mais', 'proximos', 'próximos', 'proxima', 'próxima'].includes(t)) {
      sessao.cache.pagina = (sessao.cache.pagina || 0) + 1;
      return [textoPaginaSabores(sessao)];
    }
    if (['voltar', 'anterior', 'anteriores'].includes(t)) {
      sessao.cache.pagina = (sessao.cache.pagina || 0) - 1;
      return [textoPaginaSabores(sessao)];
    }

    const pedidoFoto = t.match(/^fotos?\s*(\d+)$/);
    if (pedidoFoto) {
      const alvo = sabores[Number(pedidoFoto[1]) - 1];
      if (!alvo) return [msg('opcao_invalida') + '\n' + textoPaginaSabores(sessao)];
      if (!alvo.imagemUrl) return [`Ainda não tenho foto do sabor *${alvo.nome}*. A Nilda pode te mandar uma, é só pedir depois.`];
      return [{ imagem: alvo.imagemUrl, legenda: `${alvo.nome} — ${formatarMoeda(alvo.precoPorKg)}/kg` }];
    }

    const opcao = parseOpcao(texto, 1, sabores.length);
    if (opcao === null) return [msg('opcao_invalida') + '\n' + textoPaginaSabores(sessao)];

    const sabor = sabores[opcao - 1];
    sessao.dados.saborId = sabor.id;
    sessao.dados.saborNome = sabor.nome;
    sessao.step = STEPS.PEDIDO_PESO;
    const resposta = [];
    if (sabor.imagemUrl) resposta.push({ imagem: sabor.imagemUrl, legenda: sabor.nome });
    resposta.push(msg('pergunta_peso'));
    return resposta;
  }

  async function handlePedidoPeso(sessao, texto) {
    const peso = parsePeso(texto);
    if (peso === null) return [msg('peso_invalido')];
    sessao.dados.pesoKg = peso;

    const datas = await api.getDatasDisponiveis(14);
    const disponiveis = datas.filter((d) => d.disponivel);
    if (!disponiveis.length) {
      sessao.step = STEPS.MENU;
      return [msg('sem_datas')];
    }
    sessao.cache.datas = disponiveis;
    sessao.step = STEPS.PEDIDO_DATA;
    return [msg('pergunta_data') + '\n' + textoDatas(disponiveis)];
  }

  function textoDatas(datas) {
    return datas.map((d, i) => `${i + 1}. ${d.texto || d.data}`).join('\n');
  }

  function handlePedidoData(sessao, texto) {
    const datas = sessao.cache.datas || [];
    const opcao = parseOpcao(texto, 1, datas.length);
    if (opcao === null) return [msg('opcao_invalida') + '\n' + textoDatas(datas)];

    sessao.dados.dataEntrega = datas[opcao - 1].data;
    sessao.dados.dataTexto = datas[opcao - 1].texto || datas[opcao - 1].data;
    sessao.step = STEPS.PEDIDO_OBSERVACAO;
    return [msg('pergunta_observacao')];
  }

  function handlePedidoObservacao(sessao, texto) {
    sessao.dados.observacoes = ehNao(texto) ? undefined : texto;
    sessao.step = STEPS.PEDIDO_CONFIRMACAO;

    const d = sessao.dados;
    const peso = String(d.pesoKg).replace('.', ',');
    const resumo =
      msg('resumo_titulo') + '\n' +
      `Bolo: ${d.saborNome}\n` +
      `Tamanho: ${peso} kg\n` +
      `Entrega: ${d.dataTexto}\n` +
      (d.observacoes ? `Obs: ${d.observacoes}\n` : '') +
      '\n' + msg('resumo_confirmacao');
    return [resumo];
  }

  async function handlePedidoConfirmacao(numero, sessao, texto) {
    if (ehNao(texto)) {
      sessao.step = STEPS.MENU;
      sessao.dados = {};
      return [msg('pedido_cancelado'), menu(sessao)];
    }
    if (!ehSim(texto)) return [msg('confirmacao_invalida')];

    const d = sessao.dados;
    const resultado = await api.criarPedido({
      whatsApp: numero,
      nomeCompleto: sessao.cliente ? sessao.cliente.nome : undefined,
      saborId: d.saborId,
      pesoKg: d.pesoKg,
      dataEntrega: d.dataEntrega,
      observacoes: d.observacoes,
    });

    sessao.step = STEPS.MENU;
    sessao.dados = {};
    sessao.cliente = null; // recarrega "meus pedidos" na proxima mensagem

    if (resultado.sucesso) {
      return [msg('pedido_sucesso', { numero: resultado.id, valor: formatarMoeda(resultado.valorTotal) })];
    }
    // 400 — motivo ja pronto em portugues, vindo da API (RN05-RN07 etc.).
    return [msg('pedido_falha', { erro: resultado.erro })];
  }

  // ---- Pedido que veio do site --------------------------------------------

  async function handlePedidoSite(numero, sessao, pedidoId) {
    const pedido = await api.getPedido(pedidoId, numero);
    if (!pedido) {
      return [msg('pedido_site_nao_encontrado', { numero: pedidoId })];
    }

    await buscarCliente(numero, sessao);
    sessao.step = STEPS.PERSONALIZACAO;
    sessao.dados = { pedidoId };
    return [
      msg('pedido_site_recebido', {
        numero: pedidoId,
        resumo: pedido.resumo,
        nome: sessao.cliente ? sessao.cliente.primeiroNome : '',
      }),
      msg('pergunta_personalizacao'),
    ];
  }

  async function handlePersonalizacao(numero, sessao, texto, tipo) {
    if (tipo !== 'imagem' && ehNao(texto)) {
      sessao.step = STEPS.MENU;
      sessao.dados = {};
      return [msg('personalizacao_fim')];
    }

    // Texto (ou legenda da foto) fica anotado no pedido; a foto em si fica na conversa.
    if (texto) {
      await api.adicionarPersonalizacao(sessao.dados.pedidoId, numero, tipo === 'imagem' ? `[foto] ${texto}` : texto);
    }
    return [tipo === 'imagem' ? msg('foto_recebida') : msg('personalizacao_recebida')];
  }

  return { handleMessage, getSessao, STEPS };
}

module.exports = {
  STEPS,
  criarConversa,
  numeroDoJid,
  parsePeso,
};
