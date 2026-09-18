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

/**
 * GET /internal/whatsapp-bot/status
 * @returns {Promise<{ativo: boolean}>}
 */
async function getStatus() {
  const { data } = await http.get('/internal/whatsapp-bot/status');
  return data;
}

/**
 * GET /internal/whatsapp-bot/sabores
 * @returns {Promise<Array<{id: number, nome: string, precoPorKg: number}>>}
 */
async function getSabores() {
  const { data } = await http.get('/internal/whatsapp-bot/sabores');
  return data;
}

/**
 * GET /internal/whatsapp-bot/datas-disponiveis?quantidade=N
 * @param {number} quantidade
 * @returns {Promise<Array<{data: string, disponivel: boolean}>>}
 */
async function getDatasDisponiveis(quantidade = 10) {
  const { data } = await http.get('/internal/whatsapp-bot/datas-disponiveis', {
    params: { quantidade },
  });
  return data;
}

/**
 * POST /internal/whatsapp-bot/pedidos
 *
 * Em caso de sucesso (201) resolve com { id, valorTotal, status }.
 * Em caso de erro de validacao (400) resolve com { erro: string } (nao
 * lanca excecao) para o chamador decidir o que fazer sem precisar de
 * try/catch para o fluxo esperado de negocio.
 * Qualquer outro erro (rede, 401, 5xx) e relancado.
 *
 * @param {{nomeCompleto: string, whatsApp: string, saborId: number, pesoKg: number, dataEntrega: string, observacoes?: string}} payload
 */
async function criarPedido(payload) {
  try {
    const { data } = await http.post('/internal/whatsapp-bot/pedidos', payload);
    return { sucesso: true, ...data };
  } catch (err) {
    if (err.response && err.response.status === 400) {
      return { sucesso: false, erro: err.response.data && err.response.data.erro
        ? err.response.data.erro
        : 'Nao foi possivel criar o pedido. Verifique os dados informados.' };
    }
    throw err;
  }
}

module.exports = {
  getStatus,
  getSabores,
  getDatasDisponiveis,
  criarPedido,
};
