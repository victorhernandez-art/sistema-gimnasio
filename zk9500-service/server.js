/**
 * ══════════════════════════════════════════════════════════════════
 *  ZK9500 Service — Puente local entre el navegador y el lector
 *  de huellas ZKTeco ZK9500 (USB)
 *
 *  Puerto: 4500
 *
 *  Instalación (una sola vez):
 *    npm install
 *
 *  Iniciar servicio:
 *    node server.js
 *
 *  Compatibilidad:
 *   - ZKTeco ZK9500 (USB HID)
 *   - ZKTeco ZK9500-S, ZK9700, SLK20R y similares con misma API
 *   - Cualquier lector que emule teclado (modo HID básico)
 *
 *  Si el SDK nativo no está instalado, el servicio corre en modo
 *  SIMULACIÓN para que puedas probar la UI sin el lector físico.
 * ══════════════════════════════════════════════════════════════════
 */

const express = require('express');
const cors    = require('cors');
const app     = express();

app.use(cors({ origin: '*' }));
app.use(express.json());

// ── Intentar cargar el SDK ZKFinger ─────────────────────────────
let zkLib     = null;
let MODO_SIM  = true;

try {
    // Requiere tener instalado: npm install zkfinger
    // O el SDK oficial de ZKTeco para Node.js
    zkLib    = require('./zklib');
    MODO_SIM = false;
    console.log('[ZK9500] SDK cargado correctamente ✓');
} catch {
    console.log('[ZK9500] SDK no encontrado — modo SIMULACIÓN activo');
    console.log('          Para usar con lector real instala el SDK:');
    console.log('          npm install zkfinger');
}

// ── Estado interno ───────────────────────────────────────────────
let dispositivosConectados = [];
let fingerSSEClients       = [];  // clientes escuchando /finger (SSE)

// ── Descubrir dispositivos al iniciar ───────────────────────────
async function descubrirDispositivos() {
    if (MODO_SIM) {
        dispositivosConectados = [
            { id: 'ZK9500-SIM-01', nombre: 'ZK9500 (Simulado) — Puerta Principal' }
        ];
        return;
    }
    try {
        const devs = await zkLib.getDevices();
        dispositivosConectados = devs.map(d => ({
            id:     d.serialNumber ?? d.id,
            nombre: `ZK9500 — ${d.name ?? d.id}`
        }));
    } catch (e) {
        console.error('[ZK9500] Error al buscar dispositivos:', e.message);
    }
}

descubrirDispositivos();

// ────────────────────────────────────────────────────────────────
//  GET /status  — estado del servicio + dispositivos
// ────────────────────────────────────────────────────────────────
app.get('/status', (_req, res) => {
    res.json({ ok: true, modo: MODO_SIM ? 'simulacion' : 'sdk', dispositivos: dispositivosConectados.length });
});

// ────────────────────────────────────────────────────────────────
//  GET /devices  — lista dispositivos conectados
// ────────────────────────────────────────────────────────────────
app.get('/devices', (_req, res) => {
    res.json({ ok: true, devices: dispositivosConectados });
});

// ────────────────────────────────────────────────────────────────
//  POST /capture  — captura huella (3 escaneos)
//
//  Responde al terminar:
//    { ok: true,  template: "<base64>" }   → captura exitosa
//    { ok: false, msg: "..." }             → error / timeout
// ────────────────────────────────────────────────────────────────
app.post('/capture', async (_req, res) => {
    if (MODO_SIM) {
        // Simular tiempo de captura de 3 escaneos
        await sleep(2500);
        return res.json({
            ok:       true,
            template: 'SIMULATED_TEMPLATE_' + Date.now()
        });
    }

    try {
        const template = await zkLib.captureFingerprint({ scans: 3, timeoutMs: 30000 });
        res.json({ ok: true, template });
    } catch (e) {
        res.json({ ok: false, msg: e.message });
    }
});

// ────────────────────────────────────────────────────────────────
//  POST /sync  — sincronizar usuario al dispositivo ZKTeco
//
//  Body: { pin: 1001, nombre: "Carlos Aguilar", dispositivo: "ZK9500-01" }
// ────────────────────────────────────────────────────────────────
app.post('/sync', async (req, res) => {
    const { pin, nombre, dispositivo } = req.body ?? {};

    if (!pin || !nombre) {
        return res.json({ ok: false, msg: 'Faltan parámetros pin / nombre.' });
    }

    if (MODO_SIM) {
        await sleep(600);
        console.log(`[ZK9500] (SIM) Sincronizado: PIN ${pin} → ${nombre} en ${dispositivo ?? 'todos'}`);
        return res.json({ ok: true, msg: 'Sincronizado (simulación).' });
    }

    try {
        const dev = dispositivosConectados.find(d => d.id === dispositivo) ?? dispositivosConectados[0];
        if (!dev) return res.json({ ok: false, msg: 'Dispositivo no encontrado.' });

        await zkLib.syncUser({ deviceId: dev.id, pin, nombre });
        console.log(`[ZK9500] Sincronizado: PIN ${pin} → ${nombre}`);
        res.json({ ok: true });
    } catch (e) {
        res.json({ ok: false, msg: e.message });
    }
});

// ────────────────────────────────────────────────────────────────
//  GET /finger  — SSE: emite eventos cuando el lector identifica
//                 una huella (para registro automático de visitas)
//
//  Evento: data: {"id": 1001}
//  El campo id es el PIN del socio registrado en el dispositivo.
// ────────────────────────────────────────────────────────────────
app.get('/finger', (req, res) => {
    res.writeHead(200, {
        'Content-Type':  'text/event-stream',
        'Cache-Control': 'no-cache',
        'Connection':    'keep-alive',
        'Access-Control-Allow-Origin': '*'
    });

    // Heartbeat cada 25 s para mantener la conexión
    const heartbeat = setInterval(() => res.write(':heartbeat\n\n'), 25000);
    fingerSSEClients.push(res);

    req.on('close', () => {
        clearInterval(heartbeat);
        fingerSSEClients = fingerSSEClients.filter(c => c !== res);
    });
});

// ── Función interna: emitir evento de huella a todos los clientes
function emitirHuella(pin) {
    const payload = `data: ${JSON.stringify({ id: pin })}\n\n`;
    fingerSSEClients.forEach(c => c.write(payload));
}

// ── Si el SDK es real, suscribirse a eventos del lector ─────────
if (!MODO_SIM && zkLib && typeof zkLib.onFingerIdentified === 'function') {
    zkLib.onFingerIdentified((pin) => {
        console.log(`[ZK9500] Huella identificada → PIN ${pin}`);
        emitirHuella(pin);
    });
}

// ── Modo simulación: emitir huella de prueba con POST /test ─────
app.post('/test', (req, res) => {
    const pin = req.body?.pin ?? 1001;
    emitirHuella(pin);
    res.json({ ok: true, msg: `Huella simulada: PIN ${pin}` });
});

// ────────────────────────────────────────────────────────────────
//  Helpers
// ────────────────────────────────────────────────────────────────
function sleep(ms) { return new Promise(r => setTimeout(r, ms)); }

// ────────────────────────────────────────────────────────────────
//  Iniciar servidor
// ────────────────────────────────────────────────────────────────
const PORT = 4500;
app.listen(PORT, '127.0.0.1', () => {
    console.log(`\n╔═══════════════════════════════════════════╗`);
    console.log(`║   ZK9500 Service corriendo en :${PORT}       ║`);
    console.log(`║   Modo: ${MODO_SIM ? 'SIMULACIÓN (sin lector físico)' : 'SDK activo              '} ║`);
    console.log(`╚═══════════════════════════════════════════╝\n`);
});
