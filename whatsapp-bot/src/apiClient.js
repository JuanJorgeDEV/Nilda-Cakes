'use strict';

/**
 * Cliente HTTP fino para a API interna do Delicias da Nilda (.NET).
 *
 * Este modulo NAO contem nenhuma regra de negocio (RN01-RN16) — ele apenas
 * chama os endpoints em src/DeliciasDaNilda/Endpoints/WhatsAppBotEndpoints.cs
 * e repassa as respostas (incluindo mensagens de erro em portugues, ja
 * prontas para o cliente final) para quem chamou.
 */

const axios = require('axios');

const API_BASE_URL = process.env.API_BASE_URL || 'http://localhost:5138';
const API_KEY = process.env.API_KEY || '';

if (!API_KEY) {
  console.warn(
    '[apiClient] Aviso: API_KEY nao configurada no .env. Todas as chamadas ' +
      'a API interna vao retornar 401.'
  );
}

const http = axios.create({
  baseURL: API_BASE_URL,
  timeout: 10_000,
  headers: {
    'X-Internal-Api-Key': API_KEY,
    'Content-Type': 'application/json',
  },
});

/** Erro 400 da API vira { sucesso: false, erro } em vez de excecao. */
function erroDeNegocio(err, padrao) {
  if (err.response && err.response.status === 400) {
    return { sucesso: false, erro: (err.response.data && err.response.data.erro) || padrao };
  }
  throw err;
}

/** POST /estado: informa o painel (QR code, conectado...). Falhas aqui nao derrubam o bot. */
async function reportarEstado(estado, qr, numero) {
  try {
    await http.post('/internal/whatsapp-bot/estado', { estado, qr, numero });
  } catch (err) {
    console.error('[apiClient] Nao consegui informar o estado ao site:', err.message);
  }
}

/** GET /status → { ativo } */
async function getStatus() {
  const { data } = await http.get('/internal/whatsapp-bot/status');
  return data;
}

/** GET /mensagens → { chave: texto } (textos editaveis no painel) */
async function getMensagens() {
  const { data } = await http.get('/internal/whatsapp-bot/mensagens');
  return data;
}

/** GET /sabores → [{ id, nome, precoPorKg }] */
async function getSabores() {
  const { data } = await http.get('/internal/whatsapp-bot/sabores');
  return data;
}

/** GET /datas-disponiveis?quantidade=N → [{ data, texto, disponivel }] */
async function getDatasDisponiveis(quantidade = 10) {
  const { data } = await http.get('/internal/whatsapp-bot/datas-disponiveis', {
    params: { quantidade },
  });
  return data;
}

/**
 * GET /clientes/{numero} → { id, nome, primeiroNome, temCep, pedidos }.
 * Lanca erro com response.status 404 se o numero nao tem cadastro.
 */
async function getCliente(numero) {
  const { data } = await http.get(`/internal/whatsapp-bot/clientes/${encodeURIComponent(numero)}`);
  return data;
}

/** POST /clientes → { sucesso: true, cliente } ou { sucesso: false, erro } */
async function cadastrarCliente(payload) {
  try {
    const { data } = await http.post('/internal/whatsapp-bot/clientes', payload);
    return { sucesso: true, cliente: data };
  } catch (err) {
    return erroDeNegocio(err, 'Nao foi possivel concluir o cadastro. Confira os dados.');
  }
}

/** GET /pedidos/{id}?whatsapp= → resumo do pedido, ou null se nao for deste numero. */
async function getPedido(id, numero) {
  try {
    const { data } = await http.get(`/internal/whatsapp-bot/pedidos/${id}`, { params: { whatsapp: numero } });
    return data;
  } catch (err) {
    if (err.response && err.response.status === 404) return null;
    throw err;
  }
}

/** POST /pedidos/{id}/personalizacao */
async function adicionarPersonalizacao(id, numero, texto) {
  await http.post(`/internal/whatsapp-bot/pedidos/${id}/personalizacao`, { whatsApp: numero, texto });
}

/**
 * POST /pedidos → { sucesso: true, id, valorTotal, status } ou { sucesso: false, erro }.
 * @param {{whatsApp: string, nomeCompleto?: string, saborId: number, pesoKg: number, dataEntrega: string, observacoes?: string}} payload
 */
async function criarPedido(payload) {
  try {
    const { data } = await http.post('/internal/whatsapp-bot/pedidos', payload);
    return { sucesso: true, ...data };
  } catch (err) {
    return erroDeNegocio(err, 'Nao foi possivel criar o pedido. Verifique os dados informados.');
  }
}

/**
 * Bairro e cidade do CEP pelo ViaCEP (servico publico). Opcional: devolve
 * null se falhar, e o cadastro segue so com o CEP.
 */
async function buscarCep(cep) {
  try {
    const { data } = await axios.get(`https://viacep.com.br/ws/${cep}/json/`, { timeout: 4_000 });
    if (!data || data.erro) return null;
    return { bairro: data.bairro || null, cidade: data.localidade || null };
  } catch {
    return null;
  }
}

module.exports = {
  getStatus,
  reportarEstado,
  getMensagens,
  getSabores,
  getDatasDisponiveis,
  getCliente,
  cadastrarCliente,
  getPedido,
  adicionarPersonalizacao,
  criarPedido,
  buscarCep,
};
