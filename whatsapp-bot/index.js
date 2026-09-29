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

const { handleMessage } = criarConversa(api);

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
      console.log('\n[bot] Escaneie o QR code abaixo no WhatsApp (Aparelhos conectados):\n');
      qrcode.generate(qr, { small: true });
    }

    if (connection === 'close') {
      const statusCode = lastDisconnect?.error?.output?.statusCode;
      const deveReconectar = statusCode !== DisconnectReason.loggedOut;
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

  const respostas = await handleMessage(numero, conteudo.texto, { tipo: conteudo.tipo });

  for (const resposta of respostas) {
    if (resposta) await sock.sendMessage(jid, { text: resposta });
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
