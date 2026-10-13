/**
 * ==============================================================================
 *  GYMPRO - WHATSAPP GATEWAY SERVICE
 *  Microservicio en segundo plano para envío masivo de mensajes sin navegador.
 *  Puerto: 5085 | Tecnología: Node.js + @whiskeysockets/baileys + Express
 * ==============================================================================
 */

const {
    default: makeWASocket,
    useMultiFileAuthState,
    makeCacheableSignalKeyStore,
    DisconnectReason,
    fetchLatestBaileysVersion
} = require('@whiskeysockets/baileys');
const express = require('express');
const cors = require('cors');
const QRCode = require('qrcode');
const pino = require('pino');
const path = require('path');
const fs = require('fs');

const PORT = 5085;
const AUTH_DIR = path.join(__dirname, 'auth_session');

// Estado global de la conexión
let sock = null;
let isConnected = false;
let connectedPhone = null;
let currentQRDataURL = null;
let reconnectAttempts = 0;

// Inicializar Express
const app = express();
app.use(cors());
app.use(express.json());

// Logger silencioso para Baileys
const logger = pino({ level: 'error' });

// Almacén persistente en disco y memoria para resolver reintentos de cifrado de WhatsApp
const STORE_FILE = path.join(__dirname, 'messages_store.json');
const messageStore = new Map();
const retryMap = new Map();

// Cargar mensajes previos desde disco si existen
try {
    if (fs.existsSync(STORE_FILE)) {
        const raw = fs.readFileSync(STORE_FILE, 'utf8');
        const parsed = JSON.parse(raw);
        for (const [k, v] of Object.entries(parsed)) {
            messageStore.set(k, v);
        }
        console.log(`[WHATSAPP STORE] ${messageStore.size} mensajes cargados desde caché persistente en disco.`);
    }
} catch (e) {
    console.error('[WHATSAPP STORE ERROR CARGANDO]', e.message);
}

let storeSaveTimeout = null;
function persistirStoreEnDisco() {
    if (storeSaveTimeout) clearTimeout(storeSaveTimeout);
    storeSaveTimeout = setTimeout(() => {
        try {
            const obj = {};
            // Mantener hasta los últimos 1500 mensajes
            const entries = Array.from(messageStore.entries()).slice(-1500);
            for (const [k, v] of entries) {
                obj[k] = v;
            }
            fs.writeFileSync(STORE_FILE, JSON.stringify(obj), 'utf8');
        } catch (_) {}
    }, 1000);
}

const msgRetryCounterCache = {
    get: (key) => retryMap.get(key),
    set: (key, val) => retryMap.set(key, val),
    del: (key) => retryMap.delete(key),
    flushAll: () => retryMap.clear()
};

function guardarEnStore(keyId, compositeKey, messageObj) {
    if (messageStore.size > 2500) {
        const firstKey = messageStore.keys().next().value;
        messageStore.delete(firstKey);
    }
    if (keyId) messageStore.set(keyId, messageObj);
    if (compositeKey) messageStore.set(compositeKey, messageObj);
    persistirStoreEnDisco();
}

/**
 * Conexión principal con los servidores de WhatsApp mediante Baileys
 */
async function connectToWhatsApp() {
    try {
        if (!fs.existsSync(AUTH_DIR)) {
            fs.mkdirSync(AUTH_DIR, { recursive: true });
        }

        const { state, saveCreds } = await useMultiFileAuthState(AUTH_DIR);
        const { version } = await fetchLatestBaileysVersion();

        sock = makeWASocket({
            version,
            logger,
            printQRInTerminal: false,
            markOnlineOnConnect: false,
            auth: {
                creds: state.creds,
                keys: makeCacheableSignalKeyStore(state.keys, logger)
            },
            browser: ['GymPro Desktop', 'Chrome', '120.0.0.0'],
            syncFullHistory: false,
            msgRetryCounterCache,
            getMessage: async (key) => {
                const id = key?.id;
                const remoteJid = key?.remoteJid;
                if (id && messageStore.has(id)) {
                    console.log(`[WHATSAPP REINTENTO DESCIFRADO] Resuelto por ID: ${id}`);
                    return messageStore.get(id);
                }
                const compositeKey = `${remoteJid}:${id}`;
                if (messageStore.has(compositeKey)) {
                    console.log(`[WHATSAPP REINTENTO DESCIFRADO] Resuelto por JID+ID: ${compositeKey}`);
                    return messageStore.get(compositeKey);
                }
                return undefined;
            }
        });

        sock.ev.on('creds.update', saveCreds);

        sock.ev.on('messages.upsert', async ({ messages }) => {
            if (!Array.isArray(messages)) return;
            for (const msg of messages) {
                if (msg.key && msg.key.id && msg.message) {
                    guardarEnStore(msg.key.id, `${msg.key.remoteJid}:${msg.key.id}`, msg.message);
                }
            }
        });

        sock.ev.on('connection.update', async (update) => {
            const { connection, lastDisconnect, qr } = update;

            if (qr) {
                try {
                    currentQRDataURL = await QRCode.toDataURL(qr, { width: 320, margin: 2 });
                    isConnected = false;
                    console.log('[WHATSAPP GATEWAY] Nuevo código QR generado. Listo para escanear en GymPro.');
                } catch (err) {
                    console.error('[QR ERROR]', err);
                }
            }

            if (connection === 'open') {
                isConnected = true;
                currentQRDataURL = null;
                reconnectAttempts = 0;

                const userJid = sock.user?.id || '';
                connectedPhone = userJid.split(':')[0] || userJid.split('@')[0] || 'Conectado';

                console.log(`\n======================================================`);
                console.log(` [WHATSAPP GATEWAY] ¡CONEXIÓN EXITOSA CON WHATSAPP!`);
                console.log(` Dispositivo vinculado: +${connectedPhone}`);
                console.log(` Servicio listo para envíos masivos en: http://localhost:${PORT}`);
                console.log(`======================================================\n`);
            }

            if (connection === 'close') {
                isConnected = false;
                const statusCode = lastDisconnect?.error?.output?.statusCode;
                const shouldReconnect = statusCode !== DisconnectReason.loggedOut;

                console.log(`[WHATSAPP GATEWAY] Conexión cerrada. Código: ${statusCode}. Reconectar: ${shouldReconnect}`);

                if (shouldReconnect) {
                    reconnectAttempts++;
                    const delay = Math.min(3000 * reconnectAttempts, 15000);
                    console.log(`[WHATSAPP GATEWAY] Reintentando conexión en ${delay / 1000}s...`);
                    setTimeout(connectToWhatsApp, delay);
                } else {
                    console.log('[WHATSAPP GATEWAY] Sesión cerrada por el usuario. Limpiando credenciales...');
                    try {
                        fs.rmSync(AUTH_DIR, { recursive: true, force: true });
                    } catch (_) {}
                    connectedPhone = null;
                    currentQRDataURL = null;
                    setTimeout(connectToWhatsApp, 2000);
                }
            }
        });
    } catch (err) {
        console.error('[WHATSAPP GATEWAY ERROR INICIANDO]', err);
        setTimeout(connectToWhatsApp, 5000);
    }
}

/**
 * Normaliza y resuelve el JID oficial del teléfono en WhatsApp
 */
async function resolverJidWhatsApp(telefono) {
    if (!telefono) return null;

    let clean = telefono.replace(/\D/g, '');
    if (!clean) return null;

    // Si tiene 10 dígitos (México u otros países latinoamericanos)
    let posibles = [];
    if (clean.length === 10) {
        posibles.push(`521${clean}`); // México móvil estándar internacional
        posibles.push(`52${clean}`);
        posibles.push(clean);
    } else {
        posibles.push(clean);
        if (clean.startsWith('52') && !clean.startsWith('521') && clean.length === 12) {
            posibles.unshift(`521${clean.substring(2)}`);
        }
    }

    if (sock && isConnected) {
        for (const num of posibles) {
            try {
                const results = await sock.onWhatsApp(num);
                if (results && results.length > 0 && results[0].exists) {
                    return results[0].jid;
                }
            } catch (_) {}
        }
    }

    // Fallback: usar el primer número limpio
    const fallbackNum = posibles[0];
    return `${fallbackNum}@s.whatsapp.net`;
}

// ── ENDPOINTS DE LA API REST ───────────────────────────────────────────────────

/**
 * GET /api/status - Consulta estado de conexión y código QR
 */
app.get('/api/status', (req, res) => {
    res.json({
        ok: true,
        connected: isConnected,
        phone: connectedPhone,
        qr: isConnected ? null : currentQRDataURL,
        service: 'GymPro WhatsApp Gateway',
        port: PORT
    });
});

app.get('/api/check-phone/:phone', async (req, res) => {
    try {
        const phone = req.params.phone.replace(/\D/g, '');
        const posibles = [`521${phone}`, `52${phone}`, phone];
        const checks = [];
        if (sock && isConnected) {
            for (const p of posibles) {
                try {
                    const r = await sock.onWhatsApp(p);
                    checks.push({ query: p, result: r });
                } catch (e) {
                    checks.push({ query: p, error: e.message });
                }
            }
        }
        res.json({ phone, checks });
    } catch (err) {
        res.status(500).json({ error: err.message });
    }
});

/**
 * POST /api/send - Envío de mensaje individual directo por internet
 * Body: { phone: "9611209361", message: "Hola socio..." }
 */
app.post('/api/send', async (req, res) => {
    const { phone, message } = req.body;

    if (!isConnected || !sock) {
        return res.status(503).json({
            ok: false,
            msg: 'El Gateway de WhatsApp no está conectado. Por favor escanea el código QR primero.'
        });
    }

    if (!phone || !message) {
        return res.status(400).json({
            ok: false,
            msg: 'Faltan parámetros requeridos: "phone" y "message".'
        });
    }

    try {
        const jid = await resolverJidWhatsApp(phone);
        if (!jid) {
            return res.status(400).json({
                ok: false,
                msg: `El número ${phone} no tiene un formato válido.`
            });
        }

        console.log(`[WHATSAPP ENVIANDO] -> ${jid}`);
        try {
            await sock.sendPresenceUpdate('composing', jid);
        } catch (_) {}
        const sentResult = await sock.sendMessage(jid, { text: message.trim() });
        try {
            await sock.sendPresenceUpdate('paused', jid);
        } catch (_) {}
        if (sentResult && sentResult.key && sentResult.key.id && sentResult.message) {
            guardarEnStore(sentResult.key.id, `${jid}:${sentResult.key.id}`, sentResult.message);
        }
        console.log(`[WHATSAPP ENVIADO OK] -> ${jid}`);

        return res.json({
            ok: true,
            msg: 'Mensaje enviado con éxito por WhatsApp.',
            target: jid
        });
    } catch (err) {
        console.error(`[ERROR ENVIANDO A ${phone}]`, err);
        return res.status(500).json({
            ok: false,
            msg: `Error al enviar mensaje: ${err.message || 'Error desconocido'}`
        });
    }
});

/**
 * POST /api/logout - Cierra sesión y elimina credenciales para vincular otro número
 */
app.post('/api/logout', async (req, res) => {
    try {
        if (sock) {
            await sock.logout();
        }
        try {
            fs.rmSync(AUTH_DIR, { recursive: true, force: true });
        } catch (_) {}

        isConnected = false;
        connectedPhone = null;
        currentQRDataURL = null;

        setTimeout(connectToWhatsApp, 1500);

        res.json({ ok: true, msg: 'Sesión de WhatsApp cerrada exitosamente.' });
    } catch (err) {
        res.status(500).json({ ok: false, msg: err.message });
    }
});

// Iniciar servidor HTTP y conexión con WhatsApp
app.listen(PORT, '127.0.0.1', () => {
    console.log(`======================================================`);
    console.log(`  GYMPRO - WHATSAPP GATEWAY ACTIVO EN PUERTO ${PORT}`);
    console.log(`  URL: http://localhost:${PORT}/api/status`);
    console.log(`======================================================`);
    connectToWhatsApp();
});
