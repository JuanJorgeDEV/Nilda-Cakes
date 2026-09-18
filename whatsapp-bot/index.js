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
const { handleMessage } = require('./src/conversation');

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
  // Ignora mensagens enviadas pela propria loja (fromMe) e mensagens sem
  // conteudo de texto util (ex.: figurinhas, reacoes, notificacoes de grupo).
  if (msg.key.fromMe) return;

  const jid = msg.key.remoteJid;
  if (!jid || jid.endsWith('@g.us') || jid === 'status@broadcast') {
    // Ignora grupos e status — o bot atende apenas conversas individuais.
    return;
  }

  const texto = extrairTexto(msg);
  if (texto === null) {
    return;
  }

  console.log(`[bot] Mensagem recebida de ${mascarar(jid)} (${texto.length} caracteres).`);

  const respostas = await handleMessage(jid, texto);

  for (const resposta of respostas) {
    await sock.sendMessage(jid, { text: resposta });
  }
}

function extrairTexto(msg) {
  const conteudo = msg.message;
  if (!conteudo) return null;

  if (conteudo.conversation) return conteudo.conversation;
  if (conteudo.extendedTextMessage?.text) return conteudo.extendedTextMessage.text;
  if (conteudo.buttonsResponseMessage?.selectedDisplayText) {
    return conteudo.buttonsResponseMessage.selectedDisplayText;
  }
  if (conteudo.listResponseMessage?.title) {
    return conteudo.listResponseMessage.title;
  }

  // Outros tipos (imagem, audio, documento, etc.) nao sao suportados pelo
  // fluxo de texto do bot.
  return null;
}

/** Mascara o numero de telefone nos logs, mantendo so os ultimos 4 digitos. */
function mascarar(jid) {
  const numero = jid.split('@')[0];
  if (numero.length <= 4) return '****';
  return `${'*'.repeat(numero.length - 4)}${numero.slice(-4)}`;
}

iniciarBot().catch((err) => {
  console.error('[bot] Falha fatal ao iniciar o bot:', err);
  process.exit(1);
});
