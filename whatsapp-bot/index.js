'use strict';

/**
 * Bot de WhatsApp da Delicias da Nilda.
 *
 * Responsabilidade deste arquivo: SOMENTE conectar ao WhatsApp (Baileys) e
 * encaminhar mensagens recebidas para a maquina de conversa em
 * src/conversation.js, que por sua vez chama a API interna (.NET) para
 * qualquer decisao de negocio. Nenhuma regra de negocio (RN01-RN16) e
 * decidida aqui.
 *
 * Aviso: Baileys e uma biblioteca NAO OFICIAL que se conecta ao WhatsApp
 * simulando o WhatsApp Web. Use um numero secundario/dedicado a loja — NUNCA
 * o numero pessoal — pois o WhatsApp pode banir numeros usados com
 * automacao nao oficial. Veja README.md para mais detalhes.
 */

require('dotenv').config();

const path = require('path');
const {
  default: makeWASocket,
  useMultiFileAuthState,
  DisconnectReason,
  fetchLatestBaileysVersion,
} = require('@whiskeysockets/baileys');
const qrcode = require('qrcode-terminal');
const api = require('./src/apiClient');
const { criarConversa, numeroDoJid } = require('./src/conversation');

const SITE_URL = process.env.SITE_URL || '';
const API_BASE_URL = process.env.API_BASE_URL || 'http://localhost:5138';
const { handleMessage } = criarConversa(api, { siteUrl: SITE_URL });

const espera = (ms) => new Promise((r) => setTimeout(r, ms));

const AUTH_DIR = path.join(__dirname, 'auth_info');

async function iniciarBot() {
  const { state, saveCreds } = await useMultiFileAuthState(AUTH_DIR);
  const { version } = await fetchLatestBaileysVersion();

  const sock = makeWASocket({
    version,
    auth: state,
    printQRInTerminal: false, // exibimos o QR manualmente via qrcode-terminal
    syncFullHistory: false,
  });

  sock.ev.on('connection.update', (update) => {
    const { connection, lastDisconnect, qr } = update;

    if (qr) {
      api.reportarEstado('aguardando_qr', qr);
      console.log('\n[bot] Escaneie o QR code abaixo no WhatsApp (Aparelhos conectados):\n');
      qrcode.generate(qr, { small: true });
    }

    if (connection === 'close') {
      const statusCode = lastDisconnect?.error?.output?.statusCode;
      const deveReconectar = statusCode !== DisconnectReason.loggedOut;
      api.reportarEstado(deveReconectar ? 'desconectado' : 'parado');
      console.log(
        `[bot] Conexao encerrada (codigo ${statusCode ?? 'desconhecido'}). ` +
          `Reconectando: ${deveReconectar}`
      );
      if (deveReconectar) {
        iniciarBot().catch((err) => console.error('[bot] Falha ao reconectar:', err));
      } else {
        console.log(
          '[bot] Sessao encerrada (logout). Apague a pasta auth_info/ e ' +
            'reinicie para parear novamente com um novo QR code.'
        );
      }
    } else if (connection === 'open') {
      console.log('[bot] Conectado ao WhatsApp com sucesso.');
      api.reportarEstado('conectado', null, numeroDoJid(sock.user?.id));
    }
  });

  sock.ev.on('creds.update', saveCreds);

  sock.ev.on('messages.upsert', async ({ messages, type }) => {
    if (type !== 'notify') return;

    for (const msg of messages) {
      try {
        await processarMensagem(sock, msg);
      } catch (err) {
        console.error('[bot] Erro inesperado ao processar mensagem:', err.message);
      }
    }
  });

  return sock;
}

/**
 * @param {import('@whiskeysockets/baileys').WASocket} sock
 * @param {import('@whiskeysockets/baileys').WAMessage} msg
 */
async function processarMensagem(sock, msg) {
  // Ignora mensagens enviadas pela propria loja (fromMe).
  if (msg.key.fromMe) return;

  const jid = msg.key.remoteJid;
  if (!jid || jid.endsWith('@g.us') || jid === 'status@broadcast') {
    // Ignora grupos e status — o bot atende apenas conversas individuais.
    return;
  }

  const conteudo = extrairConteudo(msg);
  if (conteudo === null) {
    return;
  }

  // O telefone e a identificacao do cliente (RN01). Em contas novas o
  // WhatsApp pode mandar um identificador anonimo (@lid) no remoteJid; nesse
  // caso o numero real vem em senderPn / remoteJidAlt.
  const jidTelefone = [msg.key.senderPn, msg.key.remoteJidAlt, jid]
    .find((j) => j && j.endsWith('@s.whatsapp.net')) || jid;
  const numero = numeroDoJid(jidTelefone);

  console.log(`[bot] Mensagem (${conteudo.tipo}) de ${mascarar(numero)}.`);

  // "Digitando..." desde que a mensagem chega e renovado enquanto a resposta demora
  // (o WhatsApp apaga o indicador em ~10s), para a cliente nao achar que o bot parou.
  const digitando = async () => {
    try {
      await sock.presenceSubscribe(jid);
      await sock.sendPresenceUpdate('composing', jid);
    } catch (_) { /* indicador e so conforto */ }
  };
  await digitando();
  const renovar = setInterval(digitando, 8000);
  let respostas;
  try {
    respostas = await handleMessage(numero, conteudo.texto, { tipo: conteudo.tipo });
  } finally {
    clearInterval(renovar);
  }

  // Ritmo humano: "digitando..." proporcional ao texto e pausa entre mensagens.
  // Reduz o risco de o WhatsApp tratar o numero como robo.
  await espera(800 + Math.random() * 700);
  for (const resposta of respostas) {
    if (!resposta) continue;
    const ehFoto = typeof resposta !== 'string';
    const tamanho = ehFoto ? 40 : resposta.length;
    await sock.presenceSubscribe(jid);
    await sock.sendPresenceUpdate('composing', jid);
    await espera(Math.min(400 + tamanho * 12, 3500));
    if (!ehFoto) await sock.sendPresenceUpdate('paused', jid);

    if (ehFoto) {
      const url = resposta.imagem.startsWith('/') ? API_BASE_URL + resposta.imagem : resposta.imagem;
      // A foto pode demorar a baixar/enviar: mantem o "digitando" ate sair.
      const manter = setInterval(digitando, 8000);
      try {
        await sock.sendMessage(jid, { image: { url }, caption: resposta.legenda });
      } catch (err) {
        console.error('[bot] Falha ao enviar foto:', err.message);
        await sock.sendMessage(jid, { text: resposta.legenda });
      } finally {
        clearInterval(manter);
        await sock.sendPresenceUpdate('paused', jid);
      }
    } else {
      await sock.sendMessage(jid, { text: resposta });
    }
    await espera(500 + Math.random() * 500);
  }
}

/** Texto da mensagem (ou legenda da foto) e o tipo; null para o que o bot ignora. */
function extrairConteudo(msg) {
  const conteudo = msg.message;
  if (!conteudo) return null;

  if (conteudo.conversation) return { texto: conteudo.conversation, tipo: 'texto' };
  if (conteudo.extendedTextMessage?.text) return { texto: conteudo.extendedTextMessage.text, tipo: 'texto' };
  if (conteudo.imageMessage) return { texto: conteudo.imageMessage.caption || '', tipo: 'imagem' };
  if (conteudo.buttonsResponseMessage?.selectedDisplayText) {
    return { texto: conteudo.buttonsResponseMessage.selectedDisplayText, tipo: 'texto' };
  }
  if (conteudo.listResponseMessage?.title) {
    return { texto: conteudo.listResponseMessage.title, tipo: 'texto' };
  }

  // Audio, documento, figurinha etc. ficam para a Nilda ver na conversa.
  return null;
}

/** Mascara o numero de telefone nos logs, mantendo so os ultimos 4 digitos. */
function mascarar(numero) {
  if (numero.length <= 4) return '****';
  return `${'*'.repeat(numero.length - 4)}${numero.slice(-4)}`;
}

iniciarBot().catch((err) => {
  console.error('[bot] Falha fatal ao iniciar o bot:', err);
  process.exit(1);
});
