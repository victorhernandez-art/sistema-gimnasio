/**
 * GymWeb - Sistema de Tickets, Códigos de Barras y WhatsApp Digital
 * Soporta:
 * - Generación de Códigos de Barras CODE128 en SVG para tickets POS
 * - Envío directo de tickets vía WhatsApp Desktop (sin abrir pestañas nuevas)
 * - Conversión de tickets a Imagen JPG / PNG de alta calidad con html2canvas
 * - Difusión masiva de mensajes y promociones a socios
 */

(function () {
    // ── 1. GENERACIÓN DE CÓDIGO DE BARRAS EN SVG ──────────────────────────────
    window.generarBarcodeSvgHtml = function (valor, textoDebajo) {
        if (!valor) return '';
        const cleanVal = valor.toString().trim().replace(/[^a-zA-Z0-9_\-#]/g, '');
        const textoMostrar = textoDebajo || cleanVal;

        // Si JsBarcode está cargado, usamos un contenedor SVG
        const idSvg = 'barcode_' + Math.random().toString(36).substring(2, 9);
        
        // Retornamos el elemento SVG listo para ser renderizado
        setTimeout(() => {
            try {
                if (window.JsBarcode) {
                    const el = document.getElementById(idSvg);
                    if (el) {
                        window.JsBarcode(el, cleanVal, {
                            format: "CODE128",
                            lineColor: "#000000",
                            width: 1.5,
                            height: 38,
                            displayValue: true,
                            text: textoMostrar,
                            fontSize: 11,
                            fontOptions: "bold",
                            font: "Inter, Arial, monospace",
                            textMargin: 3,
                            margin: 0
                        });
                    }
                }
            } catch (e) {
                console.warn('JsBarcode render error:', e);
            }
        }, 10);

        return `<div class="ticket-barcode-wrap" style="text-align:center;margin:6px 0 3px 0;">
            <svg id="${idSvg}" style="max-width:100%;height:auto;display:inline-block;"></svg>
        </div>`;
    };

    // ── 0. GENERADOR VECTORIAL CODE 128 (Idéntico a Taller-GitHub / 100% Offline) ──
    const BarcodeUtil = {
        CODE128_PATTERNS: [
            '212222','222122','222221','121223','121322','131222','122213','122312','132212','221213',
            '221312','231212','112232','122132','122231','113222','123122','123221','223211','221132',
            '221231','213212','223112','312131','311222','321122','321221','312212','322112','322211',
            '212123','212321','232121','111323','131123','131321','112313','132113','132311','211313',
            '231113','231311','112133','112331','132131','113123','113321','133121','313121','211331',
            '231131','213113','213311','213131','311123','311321','331121','312113','312311','332111',
            '314111','221411','431111','111224','111422','121124','121421','141122','141221','112214',
            '112412','122114','122411','142112','142211','241211','221114','413111','241112','134111',
            '111242','121142','121241','114212','124112','124211','411212','421112','421211','212141',
            '214121','412121','111143','111341','131141','114113','114311','411113','411311','113141',
            '114131','311141','411131','211412','211214','211232','2331112'
        ],
        generateCode128Svg: function (text, options) {
            const rawText = String(text || '').trim();
            if (!rawText) return '';
            options = options || {};
            const height = options.height || 36;
            const barWidth = options.barWidth || 1.35;
            const showText = options.showText !== false;
            const textDisplay = options.textDisplay || rawText;
            const fontSize = options.fontSize || 10;
            const color = options.color || '#000000';

            const START_CODE_B = 104;
            const STOP_CODE = 106;
            const codes = [START_CODE_B];
            let checkSum = START_CODE_B;

            for (let i = 0; i < rawText.length; i++) {
                const charCode = rawText.charCodeAt(i);
                const val = charCode - 32;
                if (val >= 0 && val <= 95) {
                    codes.push(val);
                    checkSum += val * (i + 1);
                }
            }

            const checkDigit = checkSum % 103;
            codes.push(checkDigit);
            codes.push(STOP_CODE);

            let modules = '';
            for (let code of codes) {
                const pattern = this.CODE128_PATTERNS[code];
                if (pattern) {
                    let isBar = true;
                    for (let j = 0; j < pattern.length; j++) {
                        const width = parseInt(pattern[j], 10);
                        modules += (isBar ? '1' : '0').repeat(width);
                        isBar = !isBar;
                    }
                }
            }

            const quietZone = 8;
            const totalModules = modules.length;
            const svgWidth = (totalModules * barWidth) + (quietZone * 2);
            const svgHeight = height + (showText ? fontSize + 4 : 0);

            let rects = '';
            let currentX = quietZone;
            for (let i = 0; i < modules.length; i++) {
                if (modules[i] === '1') {
                    let w = 1;
                    while (i + 1 < modules.length && modules[i + 1] === '1') {
                        w++;
                        i++;
                    }
                    rects += `<rect x="${currentX.toFixed(2)}" y="0" width="${(w * barWidth).toFixed(2)}" height="${height}" fill="${color}" />`;
                    currentX += w * barWidth;
                } else {
                    currentX += barWidth;
                }
            }

            const textSvg = showText
                ? `<text x="${(svgWidth / 2).toFixed(2)}" y="${(height + fontSize + 1).toFixed(2)}" text-anchor="middle" font-family="-apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif" font-size="${fontSize}px" font-weight="bold" fill="${color}">${textDisplay}</text>`
                : '';

            return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${svgWidth.toFixed(2)} ${svgHeight.toFixed(2)}" width="100%" height="${svgHeight}px" style="max-width:${Math.min(svgWidth, 260)}px;display:inline-block;margin:0 auto;">${rects}${textSvg}</svg>`;
        }
    };
    window.BarcodeUtil = BarcodeUtil;

    // ── 1. GENERACIÓN DE CÓDIGO DE BARRAS EN SVG ──────────────────────────────
    window.generarBarcodeSvgHtml = function (valor, textoDebajo) {
        if (!valor) return '';
        const cleanVal = valor.toString().trim().replace(/[^a-zA-Z0-9_\-#]/g, '');
        const textoMostrar = textoDebajo || cleanVal;
        const svg = BarcodeUtil.generateCode128Svg(cleanVal, {
            height: 36,
            barWidth: 1.35,
            showText: true,
            textDisplay: textoMostrar,
            fontSize: 10
        });
        return `<div class="ticket-barcode-wrap" style="text-align:center;margin:6px 0 3px 0;">${svg}</div>`;
    };

    // Función síncrona para inyectar SVG en tickets térmicos (idéntica a Taller-GitHub)
    window.generarBarcodeSvgDirecto = function (valor, textoDebajo) {
        if (!valor) return '';
        const cleanVal = valor.toString().trim().replace(/[^a-zA-Z0-9_\-#]/g, '');
        const texto = textoDebajo || cleanVal;

        try {
            const svg = BarcodeUtil.generateCode128Svg(cleanVal, {
                height: 34,
                barWidth: 1.3,
                showText: true,
                textDisplay: texto,
                fontSize: 9.5
            });
            if (svg) {
                return `<div class="ticket-barcode-wrap" style="text-align:center;margin:8px 0 4px 0;">${svg}</div>`;
            }
        } catch (e) {
            console.warn('Error en BarcodeUtil:', e);
        }

        try {
            if (window.JsBarcode) {
                const tempSvg = document.createElementNS("http://www.w3.org/2000/svg", "svg");
                window.JsBarcode(tempSvg, cleanVal, {
                    format: "CODE128",
                    lineColor: "#000000",
                    width: 1.4,
                    height: 34,
                    displayValue: true,
                    text: texto,
                    fontSize: 10,
                    fontOptions: "bold",
                    font: "Arial, sans-serif",
                    textMargin: 3,
                    margin: 2
                });
                return `<div class="ticket-barcode-wrap" style="text-align:center;margin:8px 0 4px 0;">${tempSvg.outerHTML}</div>`;
            }
        } catch (e) {}

        return `<div class="ticket-barcode-wrap" style="text-align:center;margin:8px 0 4px 0;letter-spacing:3px;font-family:monospace;font-weight:900;font-size:16px;">
            ||| | |||| | || | ||| |||| |
            <div style="font-size:10px;font-weight:700;letter-spacing:0.5px;margin-top:2px;">${texto}</div>
        </div>`;
    };

    // ── 1.1 MOTOR DE IMPRESIÓN PROBADO EN PC (IDÉNTICO A TALLER-GITHUB) ────────
    window.GymPrintManager = {
        ejecutarImpresionNativa: function (fullHtml) {
            // Método A: Ventana emergente limpia de impresión (Flujo probado y comprobado en Taller-GitHub para PC)
            try {
                const printWindow = window.open('', '_blank', 'width=450,height=600,scrollbars=yes');
                if (printWindow && !printWindow.closed) {
                    printWindow.document.open();
                    printWindow.document.write(fullHtml);
                    printWindow.document.close();
                    return;
                }
            } catch (err) {
                console.warn('[GymPrintManager] Fallback a iframe por restricción de ventana emergente:', err);
            }

            // Método B: Fallback en iframe por si el navegador bloquea ventanas emergentes
            let iframe = document.getElementById('iframeGymPrintEngine');
            if (!iframe) {
                iframe = document.createElement('iframe');
                iframe.id = 'iframeGymPrintEngine';
                iframe.style.position = 'fixed';
                iframe.style.top = '-9999px';
                iframe.style.left = '-9999px';
                iframe.style.width = '0px';
                iframe.style.height = '0px';
                iframe.style.border = 'none';
                document.body.appendChild(iframe);
            }
            const doc = iframe.contentWindow || iframe.contentDocument;
            const d = doc.document || doc;
            d.open();
            d.write(fullHtml);
            d.close();
        },

        imprimirTicket: function (cuerpoHtml, titulo, extraCss) {
            if (typeof cuerpoHtml === 'string' && (cuerpoHtml.includes('<!DOCTYPE html>') || cuerpoHtml.includes('<html'))) {
                this.ejecutarImpresionNativa(cuerpoHtml);
                return;
            }

            const docTitle = titulo || 'Comprobante';
            const cssStyles = `
                @media print {
                    @page {
                        margin: 0 !important;
                        padding: 0 !important;
                        size: auto;
                    }
                    html, body {
                        margin: 0 !important;
                        padding: 0 !important;
                        background: #FFFFFF !important;
                        color: #000000 !important;
                        font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif !important;
                    }
                    .ticket-80mm, .ticket-container, .ticket-wrapper, #printable-ticket { 
                        width: 72mm !important;
                        max-width: 72mm !important;
                        font-size: 11.5px !important;
                        box-sizing: border-box !important;
                        overflow-wrap: break-word !important;
                        word-wrap: break-word !important;
                        hyphens: auto !important;
                        margin: 0 0 0 1.5mm !important;
                        padding: 2mm 2.5mm 10mm 2.5mm !important;
                        page-break-inside: avoid !important;
                        break-inside: avoid !important;
                    }
                    .ticket-section {
                        page-break-inside: avoid !important;
                        break-inside: avoid !important;
                        margin-bottom: 3px !important;
                    }
                    .ticket-content {
                        overflow: visible !important;
                        height: auto !important;
                    }
                    * {
                        box-sizing: border-box !important;
                        -webkit-print-color-adjust: exact !important;
                        print-color-adjust: exact !important;
                        color: #000000 !important;
                    }
                }
                body {
                    margin: 0;
                    padding: 8px;
                    background: #f1f5f9;
                    display: flex;
                    justify-content: center;
                    font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
                }
                .ticket-wrapper, .ticket-80mm {
                    width: 72mm;
                    max-width: 72mm;
                    margin: 0 auto;
                    padding: 2mm 2.5mm 10mm 2.5mm;
                    background: #fff;
                    color: #000;
                    font-size: 11.5px;
                    line-height: 1.3;
                    box-sizing: border-box;
                }
                ${extraCss || ''}
            `;

            const fullHtml = `<!DOCTYPE html>
<html lang="es">
<head>
    <meta charset="utf-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>${docTitle}</title>
    <style>${cssStyles}</style>
</head>
<body>
    ${cuerpoHtml}
    <script>
        window.addEventListener('load', function() {
            var el = document.querySelector('.ticket-wrapper') || document.querySelector('.ticket-80mm') || document.body;
            if (el) void el.offsetHeight;
            setTimeout(function() {
                window.focus();
                window.print();
                setTimeout(function() {
                    try { window.close(); } catch(e){}
                }, 2000);
            }, 250);
        });
    <\/script>
</body>
</html>`;

            this.ejecutarImpresionNativa(fullHtml);
        }
    };

    // ── 2. MODAL Y CONTROL DE ENVÍO DE TICKET POR WHATSAPP ───────────────────
    let currentWhatsPayload = null;

    window.abrirModalWhatsAppTicket = function (payload) {
        currentWhatsPayload = payload;
        crearModalWhatsAppSiNoExiste();

        const inputTel = document.getElementById('whatsModalTelefono');
        const previewText = document.getElementById('whatsModalPreview');
        const alertBox = document.getElementById('whatsModalAlert');

        alertBox.style.display = 'none';

        // Prellenar teléfono si viene en el payload
        let tel = (payload.telefono || payload.socioTelefono || '').toString().trim();
        // Limpiar caracteres no numéricos
        tel = tel.replace(/[^0-9]/g, '');
        inputTel.value = tel;

        // Generar texto estructurado y vistoso
        const mensajeFormateado = generarTextoTicketWhatsApp(payload);
        previewText.value = mensajeFormateado;

        document.getElementById('modalWhatsAppTicket').style.display = 'flex';
        if (!tel) {
            inputTel.focus();
        }
    };

    window.cerrarModalWhatsAppTicket = function () {
        const modal = document.getElementById('modalWhatsAppTicket');
        if (modal) modal.style.display = 'none';
        currentWhatsPayload = null;
    };

    // Formateador de texto con diseño formal y profesional para WhatsApp
    function generarTextoTicketWhatsApp(data) {
        const gymNom = (data.gymNombre || 'GIMNASIO').toUpperCase();
        const folio  = data.folio || ('#' + (data.idSalida || data.idPago || '0'));
        const fecha  = data.fecha || new Date().toLocaleString();
        const socio  = data.socio || data.socioNombre || 'Cliente';
        const clave  = data.socioId || data.idSocio ? `(Clave: ${data.socioId || data.idSocio})` : '';
        const tipo   = data.tipoTicket || 'VENTA'; // 'VENTA' | 'MEMBRESIA' | 'ABONO'

        let encabezado = '';
        let cuerpo = '';

        if (tipo === 'VENTA') {
            encabezado = `*COMPROBANTE DE COMPRA*`;
            let itemsTxt = '';
            if (data.items && data.items.length > 0) {
                data.items.forEach(it => {
                    const cant = it.cantidad || 1;
                    const nom = it.nombre || 'Producto';
                    const sub = Number(it.subtotal || (cant * it.precioUnitario) || 0).toFixed(2);
                    itemsTxt += `  - ${cant}x ${nom}: $${sub}\n`;
                });
            } else {
                itemsTxt = `  - Consumo en mostrador\n`;
            }

            const total = Number(data.total || 0).toFixed(2);
            const metodo = data.esCredito ? 'Crédito' : (data.metodo || data.metodoPago || 'Efectivo');

            cuerpo = `DETALLE DE ARTÍCULOS:
${itemsTxt}----------------------------------------
TOTAL: $${total}
FORMA DE PAGO: ${metodo}
${data.esCredito && data.saldoPendiente != null ? `SALDO PENDIENTE: $${Number(data.saldoPendiente).toFixed(2)}\n` : ''}`;

        } else if (tipo === 'MEMBRESIA') {
            encabezado = `*COMPROBANTE DE PAGO DE MEMBRESÍA*`;
            const plan = data.plan || 'Cuota de Membresía';
            const vigencia = data.vigencia ? `\nVigencia: Hasta ${data.vigencia}` : '';
            const monto = Number(data.monto || 0).toFixed(2);
            const metodo = data.metodoPago || data.metodo || 'Efectivo';

            cuerpo = `CONCEPTO: ${plan}${vigencia}
TOTAL PAGADO: $${monto}
FORMA DE PAGO: ${metodo}
${data.notas ? `NOTAS: ${data.notas}\n` : ''}`;

        } else if (tipo === 'ABONO') {
            encabezado = `*COMPROBANTE DE ABONO A CUENTA*`;
            const monto = Number(data.monto || 0).toFixed(2);
            const metodo = data.metodoPago || 'Efectivo';
            const saldo = data.saldoRestante != null ? `\nSALDO RESTANTE: $${Number(data.saldoRestante).toFixed(2)}` : '';

            cuerpo = `MONTO ABONADO: $${monto}
FORMA DE PAGO: ${metodo}${saldo}
${data.notas ? `CONCEPTO: ${data.notas}\n` : ''}`;
        }

        const pie = data.gymPieTicket || 'Gracias por su preferencia.';
        const telContacto = data.gymTelefono ? `\nTeléfono: ${data.gymTelefono}` : '';

        return `*${gymNom}*
----------------------------------------
${encabezado}
Folio: ${folio}
Fecha: ${fecha}
Socio: ${socio} ${clave}
----------------------------------------
${cuerpo}----------------------------------------
${pie}${telContacto}`;
    }

    // ── 3. GENERADOR DE TICKET HTML IDÉNTICO A TALLER-GITHUB (80mm) CON LOGO A COLOR ──
    window.generarTicketHtmlEstiloTaller = function (data) {
        if (!data) return '';
        const gymNom = data.gymNombre || window.GYM_CONFIG?.nombre || 'GIMNASIO';
        const gymDom = data.gymDomicilio || window.GYM_CONFIG?.domicilio || '';
        const gymTel = data.gymTelefono || window.GYM_CONFIG?.telefono || '';
        const pie    = data.gymPieTicket || '¡Gracias por su preferencia!';
        const folio  = data.folio || ('#' + (data.idSalida || data.idPago || '0').toString().padStart(6, '0'));
        const fecha  = data.fecha || new Date().toLocaleString();
        const socio  = data.socio || data.socioNombre || 'Cliente Mostrador';
        const clave  = (data.socioId || data.idSocio) ? `(Clave: ${data.socioId || data.idSocio})` : '';
        const metodo = data.esCredito ? 'A Crédito' : (data.metodo || data.metodoPago || 'Efectivo');
        const total  = Number(data.total || data.monto || 0);
        const subtotal = Number(data.subtotal || total);
        const descuento = Number(data.descuento || 0);
        const pagaCon = Number(data.montoRecibido || 0);
        const cambio  = Number(data.cambio || 0);
        const esCredito = Boolean(data.esCredito);
        const saldoPendiente = Number(data.saldoPendiente || (esCredito ? total : 0));
        const logoUrl = data.gymLogo || '/img/logo-custom.png';

        let itemsHtml = '';
        if (data.items && data.items.length > 0) {
            itemsHtml = data.items.map(it => {
                const cant = it.cantidad || 1;
                const nom  = it.nombre || 'Producto';
                const pu   = Number(it.precioUnitario || 0);
                const sub  = Number(it.subtotal || (cant * pu));
                return `
                  <div style="display:flex;justify-content:space-between;align-items:flex-start;margin-bottom:3px;gap:4px;">
                    <div style="flex:1;min-width:0;word-break:break-word;">
                      <div style="font-weight:800;color:#000000;font-size:10px;">${nom}</div>
                      <div style="font-size:8.5px;color:#333333;font-weight:600;">${cant} x $${pu.toFixed(2)}</div>
                    </div>
                    <div style="font-weight:800;color:#000000;text-align:right;white-space:nowrap;font-size:10px;">$${sub.toFixed(2)}</div>
                  </div>`;
            }).join('');
        } else if (data.plan) {
            itemsHtml = `
              <div style="display:flex;justify-content:space-between;align-items:flex-start;margin-bottom:3px;gap:4px;">
                <div style="flex:1;min-width:0;word-break:break-word;">
                  <div style="font-weight:800;color:#000000;font-size:10px;">${data.plan}</div>
                  ${data.vigencia ? `<div style="font-size:8.5px;color:#333333;font-weight:600;">Vigencia: Hasta ${data.vigencia}</div>` : ''}
                </div>
                <div style="font-weight:800;color:#000000;text-align:right;white-space:nowrap;font-size:10px;">$${total.toFixed(2)}</div>
              </div>`;
        } else {
            itemsHtml = `
              <div style="display:flex;justify-content:space-between;align-items:flex-start;margin-bottom:3px;gap:4px;">
                <div style="flex:1;min-width:0;word-break:break-word;">
                  <div style="font-weight:800;color:#000000;font-size:10px;">${data.notas || 'Consumo en mostrador'}</div>
                </div>
                <div style="font-weight:800;color:#000000;text-align:right;white-space:nowrap;font-size:10px;">$${total.toFixed(2)}</div>
              </div>`;
        }

        const barcodeVal = (data.socioId || data.idSocio || folio).toString().replace(/[^a-zA-Z0-9_\-#]/g, '');
        const barcodeSvg = window.generarBarcodeSvgDirecto(barcodeVal, `Folio: ${folio} | ${socio}`);

        return `
          <div class="ticket-wrapper ticket-80mm" style="background:#FFFFFF;color:#000000;padding:12px 14px;border-radius:6px;box-shadow:0 4px 18px rgba(0,0,0,0.35);max-width:320px;width:100%;margin:0 auto 14px auto;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Arial,sans-serif;font-size:11px;line-height:1.3;text-align:left;box-sizing:border-box;">
            <!-- Cabecera y Logo a Color -->
            <div style="text-align:center;border-bottom:1px dashed #000;padding-bottom:6px;margin-bottom:6px;">
              <div style="margin-bottom:5px;">
                <img src="${logoUrl}" alt="Logo" style="max-height:55px;max-width:140px;object-fit:contain;margin:0 auto;display:block;" onerror="this.onerror=null;this.src='/img/gym.jpeg';this.onerror=function(){this.style.display='none';};" />
              </div>
              <div style="font-size:14px;font-weight:900;text-transform:uppercase;letter-spacing:0.5px;color:#000000;">${gymNom}</div>
              ${gymDom ? `<div style="font-size:9px;color:#333;">${gymDom}</div>` : ''}
              ${gymTel ? `<div style="font-size:9px;color:#333;">Tel: ${gymTel}</div>` : ''}
              <div style="border-top:1px dashed #000;margin:4px 0;"></div>
              <div style="font-size:12px;font-weight:900;color:#000;margin:2px 0;">COMPROBANTE: ${folio}</div>
              <div style="font-size:9px;color:#222;font-weight:600;">${fecha}</div>
              <div style="font-size:9.5px;color:#000;margin-top:3px;padding-top:3px;border-top:1px dashed #000;">
                <strong>Cliente:</strong> ${socio} ${clave}
              </div>
            </div>

            <!-- Artículos -->
            <div style="font-size:8.5px;font-weight:800;border-bottom:1px solid #000;padding-bottom:2px;margin:4px 0 3px;text-transform:uppercase;letter-spacing:0.5px;">ARTÍCULOS / CONCEPTOS</div>
            <div style="margin-bottom:6px;">
              ${itemsHtml}
            </div>

            <div style="border-top:2px dashed #000;margin:4px 0;"></div>

            <!-- Totales -->
            ${descuento > 0 ? `
              <div style="display:flex;justify-content:space-between;font-size:9.5px;color:#000;margin:1px 0;">
                <span>Subtotal:</span>
                <span>$${subtotal.toFixed(2)}</span>
              </div>
              <div style="display:flex;justify-content:space-between;font-size:9.5px;color:#000;font-weight:700;margin:1px 0;">
                <span>Descuento:</span>
                <span>-$${descuento.toFixed(2)}</span>
              </div>
            ` : ''}
            <div style="display:flex;justify-content:space-between;font-size:12px;font-weight:900;color:#000;margin:3px 0;">
              <span>TOTAL PAGADO:</span>
              <span>$${total.toFixed(2)}</span>
            </div>

            <div style="display:flex;justify-content:space-between;font-size:9.5px;color:#000;margin-top:2px;">
              <span>Método de pago:</span>
              <span style="font-weight:800;">${metodo}</span>
            </div>

            ${!esCredito && metodo === 'Efectivo' && pagaCon > 0 ? `
              <div style="display:flex;justify-content:space-between;font-size:9.5px;color:#000;margin:1px 0;">
                <span>Paga con:</span>
                <span>$${pagaCon.toFixed(2)}</span>
              </div>
              <div style="display:flex;justify-content:space-between;font-size:10px;color:#000;font-weight:800;margin:1px 0;">
                <span>Cambio:</span>
                <span>$${cambio.toFixed(2)}</span>
              </div>
            ` : ''}

            ${esCredito ? `
              <div style="display:flex;justify-content:space-between;font-size:10.5px;color:#b91c1c;font-weight:900;margin-top:3px;">
                <span>Saldo pendiente:</span>
                <span>$${saldoPendiente.toFixed(2)}</span>
              </div>
            ` : ''}

            <div style="border-top:1px dashed #000;margin:5px 0 4px 0;"></div>

            <div style="text-align:center;padding:3px;background:#f3f4f6;border-radius:3px;border:1px solid #000;margin-bottom:4px;">
              <span style="font-weight:900;color:#000;font-size:9.5px;">
                ${esCredito ? '⚠️ VENTA REGISTRADA A CRÉDITO' : '✅ COMPROBANTE OFICIAL / LIQUIDADO'}
              </span>
            </div>

            <!-- Términos -->
            <div style="text-align:center;font-size:8px;line-height:1.2;color:#333;margin-top:4px;">
              ${pie}
            </div>

            <!-- Código de barras CODE128 -->
            <div style="margin-top:6px;padding-top:4px;border-top:1px dashed #000;text-align:center;">
              ${barcodeSvg}
            </div>
          </div>
        `;
    };

    // ── 3.1 GENERAR TICKET EN FORMATO PDF DE 80mm CON ULTRA NITIDEZ ──────────
    window.generarTicketPdfBlob = async function (data) {
        let ticketEl = document.querySelector('.ticket-wrapper.ticket-80mm');
        let tempContainer = null;

        if (!ticketEl) {
            tempContainer = document.createElement('div');
            tempContainer.id = 'ticketPdfTempGen';
            tempContainer.style.position = 'fixed';
            tempContainer.style.top = '-9999px';
            tempContainer.style.left = '-9999px';
            tempContainer.style.width = '320px';
            tempContainer.style.background = '#FFFFFF';
            tempContainer.innerHTML = window.generarTicketHtmlEstiloTaller(data);
            document.body.appendChild(tempContainer);
            ticketEl = tempContainer.querySelector('.ticket-wrapper') || tempContainer;
        }

        // Esperar a que las imágenes (logo) carguen
        const imgs = ticketEl.querySelectorAll('img');
        await Promise.all(Array.from(imgs).map(img => {
            if (img.complete) return Promise.resolve();
            return new Promise(resolve => {
                img.onload = resolve;
                img.onerror = resolve;
            });
        }));

        const canvas = await window.html2canvas(ticketEl, {
            backgroundColor: '#ffffff',
            scale: 2.5,
            useCORS: true,
            logging: false
        });

        if (tempContainer) {
            tempContainer.remove();
        }

        const imgData = canvas.toDataURL('image/png', 1.0);
        const imgWidth = 80; // 80 mm ancho estándar
        const pageHeight = Math.max(80, (canvas.height * imgWidth) / canvas.width);

        const { jsPDF } = window.jspdf || {};
        if (!jsPDF) {
            throw new Error('Librería jsPDF no disponible.');
        }

        const pdf = new jsPDF({
            orientation: 'portrait',
            unit: 'mm',
            format: [imgWidth, pageHeight]
        });

        pdf.addImage(imgData, 'PNG', 0, 0, imgWidth, pageHeight);

        const base64 = pdf.output('datauristring');
        return { pdf, base64 };
    };

    // Referencia global para ventana única (Singleton) de WhatsApp Web
    window._gymWhatsAppVentanaUnica = null;

    // ── 3.2 APERTURA DE VENTANA WHATSAPP WEB CENTRADA (VENTANA ÚNICA / SINGLETON) ──
    window.abrirVentanaWhatsAppCentrada = function (telefono, mensajeTexto) {
        let cleanPhone = String(telefono || '').replace(/[^\d]/g, '');
        if (cleanPhone.length === 10) cleanPhone = '52' + cleanPhone;

        const encodedText = encodeURIComponent(mensajeTexto || '');
        const url = `https://web.whatsapp.com/send/?phone=${cleanPhone}&text=${encodedText}`;

        // 1. Si la ventana previa sigue abierta, reutilizarla y traerla al frente sin abrir una nueva
        if (window._gymWhatsAppVentanaUnica && !window._gymWhatsAppVentanaUnica.closed) {
            try {
                window._gymWhatsAppVentanaUnica.location.href = url;
                window._gymWhatsAppVentanaUnica.focus();
                return window._gymWhatsAppVentanaUnica;
            } catch (err) {
                console.warn('Reutilizando ventana singleton de WhatsApp:', err);
                try {
                    window._gymWhatsAppVentanaUnica = window.open(url, 'whatsapp_taller_win');
                    if (window._gymWhatsAppVentanaUnica) {
                        window._gymWhatsAppVentanaUnica.focus();
                    }
                    return window._gymWhatsAppVentanaUnica;
                } catch (_) {}
            }
        }

        // 2. Si no existe o fue cerrada, crear la ventana popup centrada y guardar la referencia
        const w = Math.min(1080, window.screen.availWidth - 40);
        const h = Math.min(760, window.screen.availHeight - 60);
        const left = Math.max(0, Math.round((window.screen.availWidth - w) / 2));
        const top = Math.max(0, Math.round((window.screen.availHeight - h) / 2));

        const features = `width=${w},height=${h},top=${top},left=${left},status=no,toolbar=no,menubar=no,location=no,scrollbars=yes,resizable=yes`;
        window._gymWhatsAppVentanaUnica = window.open(url, 'whatsapp_taller_win', features);

        if (window._gymWhatsAppVentanaUnica) {
            try {
                window._gymWhatsAppVentanaUnica.focus();
            } catch (_) {}
        }
        return window._gymWhatsAppVentanaUnica;
    };

    // ── 3.3 PROCESAR Y ENVIAR COMPROBANTE PDF POR WHATSAPP (Mecánica Taller) ──
    window.enviarComprobantePdfWhatsApp = async function (data, telManual) {
        if (!data) return;

        let tel = (telManual || data.socioTelefono || data.telefono || '').toString().replace(/\D/g, '');
        if (!tel || tel.length < 10) {
            window.abrirModalWhatsAppTicket(data);
            return;
        }

        const folioClean = (data.folio || ('#' + (data.idSalida || data.idPago || '0'))).replace(/[^a-zA-Z0-9_\-]/g, '');
        const filename = `Comprobante_${folioClean}_ticket_80mm.pdf`;
        const gymNom = data.gymNombre || window.GYM_CONFIG?.nombre || 'GIMNASIO';
        const socio = data.socio || data.socioNombre || 'Cliente';
        const concepto = data.plan || (data.tipoTicket === 'VENTA' ? 'compra en tienda' : 'pago oficial');
        const msgBreve = `¡Hola ${socio}! Le compartimos el comprobante oficial de su ${concepto} en ${gymNom}:`;

        window.mostrarToastNotificacion('📄 Generando comprobante PDF para WhatsApp...', 'info');

        try {
            const { pdf, base64 } = await window.generarTicketPdfBlob(data);

            // 1. Descarga del archivo PDF oficial
            pdf.save(filename);

            // 2. Guardar en descargas y copiar al portapapeles de Windows (Set-Clipboard -Path)
            try {
                await fetch('/Home/GuardarYCopiarPdf', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ filename, base64 })
                });
            } catch (e) {
                console.warn('Copiado a portapapeles Windows:', e);
            }

            // 3. Abrir ventana emergente centrada de WhatsApp Web
            window.abrirVentanaWhatsAppCentrada(tel, msgBreve);

            window.cerrarModalWhatsAppTicket();
            window.mostrarToastNotificacion('💬 WhatsApp Web abierto. Se adjuntará el PDF automáticamente (o presiona Ctrl+V para adjuntarlo).', 'success');
        } catch (err) {
            console.error('Error al generar PDF para WhatsApp:', err);
            window.abrirVentanaWhatsAppCentrada(tel, msgBreve);
        }
    };

    // ── 3.4 DISPATCH DEL BOTÓN ENVIAR DESDE EL MODAL MANUAL ──────────────────
    window.enviarWhatsAppProtocoloDirecto = function () {
        const inputTel = document.getElementById('whatsModalTelefono');
        const alertBox = document.getElementById('whatsModalAlert');

        let tel = inputTel.value.trim().replace(/[^0-9]/g, '');
        if (!tel || tel.length < 10) {
            alertBox.textContent = 'Por favor ingresa un número de WhatsApp válido (10 dígitos).';
            alertBox.style.display = 'block';
            inputTel.focus();
            return;
        }

        window.enviarComprobantePdfWhatsApp(currentWhatsPayload, tel);
    };

    // ── 3.5 NOTIFICACIONES TOAST MINIMALISTAS ─────────────────────────────────
    window.mostrarToastNotificacion = function (mensaje, tipo = 'info') {
        let toast = document.getElementById('gymToastNotif');
        if (!toast) {
            toast = document.createElement('div');
            toast.id = 'gymToastNotif';
            toast.style.position = 'fixed';
            toast.style.bottom = '24px';
            toast.style.right = '24px';
            toast.style.zIndex = '9999999';
            toast.style.padding = '12px 20px';
            toast.style.borderRadius = '12px';
            toast.style.fontSize = '13.5px';
            toast.style.fontWeight = '700';
            toast.style.boxShadow = '0 10px 30px rgba(0,0,0,0.5)';
            toast.style.transition = 'all 0.3s ease';
            toast.style.display = 'flex';
            toast.style.alignItems = 'center';
            toast.style.gap = '10px';
            toast.style.pointerEvents = 'none';
            document.body.appendChild(toast);
        }
        const isSuccess = tipo === 'success';
        toast.style.background = isSuccess ? '#059669' : '#0284c7';
        toast.style.color = '#FFFFFF';
        toast.style.border = isSuccess ? '1px solid #10b981' : '1px solid #38bdf8';
        toast.innerHTML = `<i class="fa-solid ${isSuccess ? 'fa-circle-check' : 'fa-info-circle'}"></i> <span>${mensaje}</span>`;
        toast.style.opacity = '1';
        toast.style.transform = 'translateY(0)';
        setTimeout(() => {
            if (toast) {
                toast.style.opacity = '0';
                toast.style.transform = 'translateY(10px)';
            }
        }, 4000);
    };

    window.copiarTextoWhats = function () {
        const previewText = document.getElementById('whatsModalPreview');
        previewText.select();
        navigator.clipboard.writeText(previewText.value).then(() => {
            const alertBox = document.getElementById('whatsModalAlert');
            alertBox.style.background = 'rgba(56,189,248,0.15)';
            alertBox.style.borderColor = '#38bdf8';
            alertBox.style.color = '#38bdf8';
            alertBox.innerHTML = '✓ <strong>Texto del ticket copiado al portapapeles</strong>.';
            alertBox.style.display = 'block';
        });
    };

    function crearContenedorTemporalTicket(data) {
        const div = document.createElement('div');
        div.id = 'ticketTempRender';
        div.style.position = 'fixed';
        div.style.top = '-9999px';
        div.style.left = '-9999px';
        div.style.width = '340px';
        div.style.padding = '18px';
        div.style.background = '#ffffff';
        div.style.color = '#000000';
        div.style.fontFamily = 'Inter, Arial, sans-serif';
        div.style.fontSize = '12px';
        div.style.boxSizing = 'border-box';

        const gymNom = data?.gymNombre || 'MI GIMNASIO';
        const folio = data?.folio || '#000000';
        const socio = data?.socio || data?.socioNombre || 'Cliente';
        const total = Number(data?.total || data?.monto || 0).toFixed(2);
        const barcodeHtml = window.generarBarcodeSvgDirecto(folio, `${socio} | ${folio}`);

        div.innerHTML = `
            <div style="text-align:center;border-bottom:1px dashed #000;padding-bottom:10px;margin-bottom:10px;">
                <h2 style="margin:0;font-size:16px;font-weight:900;text-transform:uppercase;">${gymNom}</h2>
                <div style="font-size:11px;color:#333;">COMPROBANTE DIGITAL</div>
                <div style="font-size:13px;font-weight:800;margin-top:4px;">${folio}</div>
            </div>
            <div style="margin-bottom:10px;font-size:11px;">
                <div><strong>Socio:</strong> ${socio}</div>
                <div><strong>Fecha:</strong> ${data?.fecha || new Date().toLocaleString()}</div>
                <div><strong>Total:</strong> $${total}</div>
            </div>
            ${barcodeHtml}
            <div style="text-align:center;font-size:10px;margin-top:10px;color:#555;">
                ¡Gracias por su preferencia!
            </div>
        `;
        return div;
    }

    function configurarModalOverlayBase(modal) {
        modal.className = 'pos-modal-overlay';
        modal.style.display = 'none';
        modal.style.position = 'fixed';
        modal.style.top = '0';
        modal.style.left = '0';
        modal.style.right = '0';
        modal.style.bottom = '0';
        modal.style.width = '100vw';
        modal.style.height = '100vh';
        modal.style.background = 'rgba(7, 13, 26, 0.85)';
        modal.style.backdropFilter = 'blur(6px)';
        modal.style.webkitBackdropFilter = 'blur(6px)';
        modal.style.zIndex = '999999';
        modal.style.justifyContent = 'center';
        modal.style.alignItems = 'center';
        modal.style.padding = '16px';
        modal.style.boxSizing = 'border-box';
    }

    // ── 5. CREACIÓN DEL MODAL DOM PARA WHATSAPP TICKET ───────────────────────
    function crearModalWhatsAppSiNoExiste() {
        if (document.getElementById('modalWhatsAppTicket')) return;

        const modal = document.createElement('div');
        modal.id = 'modalWhatsAppTicket';
        configurarModalOverlayBase(modal);

        modal.innerHTML = `
        <div class="pos-modal-card" style="max-width:520px;width:95%;padding:24px;border-radius:18px;background:var(--card-bg, #0f172a);border:1px solid rgba(255,255,255,0.12);box-shadow:0 25px 60px rgba(0,0,0,0.6);text-align:left;position:relative;z-index:1000000;">
            <div style="display:flex;justify-content:space-between;align-items:center;margin-bottom:16px;">
                <div style="display:flex;align-items:center;gap:10px;">
                    <div style="width:40px;height:40px;border-radius:10px;background:#25D366;display:flex;align-items:center;justify-content:center;color:#0B1120;font-size:22px;box-shadow:0 4px 14px rgba(37,211,102,0.4);">
                        <i class="fa-brands fa-whatsapp"></i>
                    </div>
                    <div>
                        <div style="font-size:16px;font-weight:800;color:var(--text, #f8fafc);">Enviar Comprobante PDF por WhatsApp</div>
                        <div style="font-size:12px;color:var(--muted, #94a3b8);">Genera el archivo PDF oficial y abre WhatsApp Web centrado</div>
                    </div>
                </div>
                <button type="button" onclick="cerrarModalWhatsAppTicket()" style="background:none;border:none;color:var(--muted,#94a3b8);font-size:20px;cursor:pointer;">&times;</button>
            </div>

            <div id="whatsModalAlert" style="display:none;padding:10px 14px;border-radius:8px;font-size:12px;margin-bottom:14px;background:rgba(239,68,68,0.15);border:1px solid #ef4444;color:#ef4444;"></div>

            <div style="margin-bottom:14px;">
                <label style="display:block;font-size:12px;font-weight:700;margin-bottom:6px;color:var(--text,#f8fafc);">
                    <i class="fa-solid fa-mobile-screen" style="color:#25D366;"></i> Número de WhatsApp del Socio (10 dígitos):
                </label>
                <div style="display:flex;gap:8px;">
                    <span style="display:flex;align-items:center;padding:0 12px;background:rgba(255,255,255,0.06);border:1px solid rgba(255,255,255,0.15);border-radius:8px;font-size:13px;font-weight:700;color:#94a3b8;">+52</span>
                    <input type="tel" id="whatsModalTelefono" class="form-control" placeholder="Ej: 9611234567" maxlength="15"
                           style="flex:1;background:rgba(255,255,255,0.05);border:1px solid rgba(255,255,255,0.18);border-radius:8px;padding:10px 12px;color:#fff;font-size:14px;font-weight:700;" />
                </div>
            </div>

            <div style="margin-bottom:16px;">
                <label style="display:block;font-size:12px;font-weight:700;margin-bottom:6px;color:var(--text,#f8fafc);">
                    <i class="fa-solid fa-file-pdf" style="color:#38bdf8;"></i> Mensaje y Comprobante PDF Adjunto:
                </label>
                <textarea id="whatsModalPreview" rows="6" class="form-control"
                          style="width:100%;box-sizing:border-box;background:rgba(0,0,0,0.25);border:1px solid rgba(255,255,255,0.15);border-radius:8px;padding:10px;color:#e2e8f0;font-size:11.5px;font-family:monospace;resize:vertical;"></textarea>
            </div>

            <div style="display:flex;flex-direction:column;gap:10px;">
                <button type="button" onclick="enviarWhatsAppProtocoloDirecto()" class="btn"
                        style="background:#25D366;border:none;color:#0B1120;font-weight:900;font-size:14.5px;padding:12px 18px;border-radius:10px;cursor:pointer;display:flex;align-items:center;justify-content:center;gap:8px;box-shadow:0 4px 14px rgba(37,211,102,0.35);transition:all 0.2s;">
                    <i class="fa-brands fa-whatsapp" style="font-size:19px;"></i> Generar PDF y Abrir WhatsApp
                </button>

                <div style="display:flex;justify-content:space-between;align-items:center;margin-top:4px;">
                    <button type="button" onclick="copiarTextoWhats()" class="btn btn-sm"
                            style="background:rgba(255,255,255,0.06);border:1px solid rgba(255,255,255,0.15);color:#cbd5e1;border-radius:6px;font-size:11px;padding:6px 12px;cursor:pointer;">
                        <i class="fa-regular fa-copy"></i> Copiar Texto
                    </button>
                    <button type="button" onclick="cerrarModalWhatsAppTicket()" class="btn btn-sm"
                            style="background:transparent;border:none;color:#94a3b8;font-size:12px;cursor:pointer;">
                        Cerrar
                    </button>
                </div>
            </div>
        </div>`;

        document.body.appendChild(modal);
    }

    // ── 6. MENSAJE DIRECTO INDIVIDUAL A SOCIO ─────────────────────────────────
    let currentDirectoSocio = null;

    window.enviarMensajeDirectoSocio = function (telefono, nombre, idSocio) {
        currentDirectoSocio = { telefono: telefono || '', nombre: nombre || 'Socio', idSocio: idSocio };
        crearModalMensajeDirectoSiNoExiste();

        const inputTel = document.getElementById('directoModalTelefono');
        const inputNombre = document.getElementById('directoModalNombre');
        const textArea = document.getElementById('directoModalTexto');
        const alertBox = document.getElementById('directoModalAlert');

        alertBox.style.display = 'none';
        inputTel.value = (telefono || '').replace(/\D/g, '');
        inputNombre.textContent = nombre || 'Socio';

        const gymNombre = window.GYM_CONFIG?.nombre || 'Gimnasio';
        textArea.value = `Estimado(a) ${nombre}, le saludamos de *${gymNombre}*. Esperamos contar con su asistencia el día de hoy.`;

        const modal = document.getElementById('modalWhatsAppDirecto');
        modal.style.display = 'flex';
    };

    window.cerrarModalWhatsAppDirecto = function () {
        const modal = document.getElementById('modalWhatsAppDirecto');
        if (modal) modal.style.display = 'none';
    };

    window.aplicarPlantillaDirecto = function (tipo) {
        const gymNombre = window.GYM_CONFIG?.nombre || 'Gimnasio';
        const nombre = currentDirectoSocio?.nombre || 'Socio';
        const textArea = document.getElementById('directoModalTexto');

        if (tipo === 'saludo') {
            textArea.value = `Estimado(a) ${nombre}, le saludamos de *${gymNombre}*. Esperamos que tenga un excelente día y le esperamos para su entrenamiento.`;
        } else if (tipo === 'aviso') {
            textArea.value = `Estimado(a) ${nombre}, le compartimos un aviso importante de *${gymNombre}* sobre nuestros horarios e instalaciones. Agradecemos su atención.`;
        } else if (tipo === 'pago') {
            textArea.value = `Estimado(a) ${nombre}, le saludamos cordialmente de *${gymNombre}* para recordarle que presenta una cuota pendiente en su cuenta. Puede pasar al área de recepción para regularizarla. Gracias por su preferencia.`;
        } else if (tipo === 'promo') {
            textArea.value = `Estimado(a) ${nombre}, en *${gymNombre}* contamos con promociones vigentes en planes y servicios. Puede consultar los detalles en recepción. Le esperamos.`;
        }
    };

    window.ejecutarEnvioDirecto = function () {
        const tel = document.getElementById('directoModalTelefono').value.replace(/\D/g, '');
        const texto = document.getElementById('directoModalTexto').value.trim();
        const alertBox = document.getElementById('directoModalAlert');

        if (!tel || tel.length < 10) {
            alertBox.style.display = 'block';
            alertBox.textContent = 'Ingresa un número telefónico válido de 10 dígitos.';
            return;
        }
        if (!texto) {
            alertBox.style.display = 'block';
            alertBox.textContent = 'Escribe un mensaje antes de enviar.';
            return;
        }

        const phoneWithCountry = tel.length === 10 ? ('52' + tel) : tel;
        const encodedText = encodeURIComponent(texto);

        // Envío directo reutilizando la misma ventana única de WhatsApp sin duplicar
        window.abrirVentanaWhatsAppCentrada(phoneWithCountry, texto);
        cerrarModalWhatsAppDirecto();
    };

    function crearModalMensajeDirectoSiNoExiste() {
        if (document.getElementById('modalWhatsAppDirecto')) return;

        const modal = document.createElement('div');
        modal.id = 'modalWhatsAppDirecto';
        configurarModalOverlayBase(modal);

        modal.innerHTML = `
        <div class="pos-modal-card" style="max-width:500px;width:95%;padding:24px;border-radius:18px;background:var(--card-bg, #0f172a);border:1px solid rgba(255,255,255,0.12);box-shadow:0 25px 60px rgba(0,0,0,0.6);text-align:left;position:relative;z-index:1000000;">
            <div style="display:flex;justify-content:space-between;align-items:center;margin-bottom:14px;">
                <div style="display:flex;align-items:center;gap:10px;">
                    <div style="width:38px;height:38px;border-radius:10px;background:#25D366;display:flex;align-items:center;justify-content:center;color:#0B1120;font-size:20px;box-shadow:0 4px 14px rgba(37,211,102,0.4);">
                        <i class="fa-brands fa-whatsapp"></i>
                    </div>
                    <div>
                        <div style="font-size:15px;font-weight:800;color:var(--text, #f8fafc);">WhatsApp a <span id="directoModalNombre" style="color:#25D366;">Socio</span></div>
                        <div style="font-size:11.5px;color:var(--muted, #94a3b8);">Envío de mensaje directo al socio</div>
                    </div>
                </div>
                <button type="button" onclick="cerrarModalWhatsAppDirecto()" style="background:none;border:none;color:var(--muted,#94a3b8);font-size:20px;cursor:pointer;">&times;</button>
            </div>

            <div id="directoModalAlert" style="display:none;padding:8px 12px;border-radius:8px;font-size:12px;margin-bottom:12px;background:rgba(239,68,68,0.15);border:1px solid #ef4444;color:#ef4444;"></div>

            <div style="margin-bottom:12px;">
                <label style="display:block;font-size:11.5px;font-weight:700;margin-bottom:4px;color:var(--text,#f8fafc);">Número de WhatsApp:</label>
                <div style="display:flex;gap:8px;">
                    <span style="display:flex;align-items:center;padding:0 10px;background:rgba(255,255,255,0.06);border:1px solid rgba(255,255,255,0.15);border-radius:8px;font-size:12px;font-weight:700;color:#94a3b8;">+52</span>
                    <input type="tel" id="directoModalTelefono" class="form-control" placeholder="10 dígitos" maxlength="15"
                           style="flex:1;background:rgba(255,255,255,0.05);border:1px solid rgba(255,255,255,0.18);border-radius:8px;padding:8px 10px;color:#fff;font-size:13px;font-weight:700;" />
                </div>
            </div>

            <!-- Plantillas rápidas -->
            <div style="margin-bottom:10px;">
                <div style="font-size:11px;font-weight:700;color:var(--muted,#94a3b8);margin-bottom:5px;">Plantillas rápidas:</div>
                <div style="display:flex;gap:5px;flex-wrap:wrap;">
                    <button type="button" onclick="aplicarPlantillaDirecto('saludo')" class="btn btn-sm" style="background:rgba(255,255,255,0.06);border:1px solid rgba(255,255,255,0.12);color:#cbd5e1;font-size:11px;padding:4px 8px;border-radius:6px;">Saludo</button>
                    <button type="button" onclick="aplicarPlantillaDirecto('pago')" class="btn btn-sm" style="background:rgba(239,68,68,0.1);border:1px solid rgba(239,68,68,0.3);color:#f87171;font-size:11px;padding:4px 8px;border-radius:6px;">Cobro / Cuota</button>
                    <button type="button" onclick="aplicarPlantillaDirecto('promo')" class="btn btn-sm" style="background:rgba(251,191,36,0.1);border:1px solid rgba(251,191,36,0.3);color:#fbbf24;font-size:11px;padding:4px 8px;border-radius:6px;">Promoción</button>
                    <button type="button" onclick="aplicarPlantillaDirecto('aviso')" class="btn btn-sm" style="background:rgba(56,189,248,0.1);border:1px solid rgba(56,189,248,0.3);color:#38bdf8;font-size:11px;padding:4px 8px;border-radius:6px;">Aviso</button>
                </div>
            </div>

            <div style="margin-bottom:16px;">
                <label style="display:block;font-size:11.5px;font-weight:700;margin-bottom:4px;color:var(--text,#f8fafc);">Mensaje:</label>
                <textarea id="directoModalTexto" rows="5" class="form-control"
                          style="width:100%;box-sizing:border-box;background:rgba(0,0,0,0.25);border:1px solid rgba(255,255,255,0.15);border-radius:8px;padding:10px;color:#e2e8f0;font-size:12px;font-family:inherit;resize:vertical;"></textarea>
            </div>

            <div style="display:flex;flex-direction:column;gap:8px;">
                <button type="button" onclick="ejecutarEnvioDirecto()" class="btn"
                        style="background:#25D366;border:none;color:#0B1120;font-weight:900;font-size:14px;padding:11px 16px;border-radius:9px;cursor:pointer;display:flex;align-items:center;justify-content:center;gap:8px;box-shadow:0 3px 12px rgba(37,211,102,0.35);">
                    <i class="fa-brands fa-whatsapp" style="font-size:18px;"></i> Enviar
                </button>
                <div style="display:flex;justify-content:flex-end;align-items:center;margin-top:4px;">
                    <button type="button" onclick="cerrarModalWhatsAppDirecto()" class="btn btn-sm"
                            style="background:transparent;border:none;color:#94a3b8;font-size:11.5px;cursor:pointer;">
                        Cancelar
                    </button>
                </div>
            </div>
        </div>`;

        document.body.appendChild(modal);
    }

    // ── 7. DIFUSIÓN MASIVA POR WHATSAPP ───────────────────────────────────────
    let difusionSocios = [];
    let difusionIndiceActual = 0;
    let difusionEnEjecucion = false;
    let difusionTimer = null;

    window.abrirModalDifusionMasiva = async function () {
        crearModalDifusionMasivaSiNoExiste();
        const modal = document.getElementById('modalDifusionMasiva');
        modal.style.display = 'flex';
        await cargarSociosParaDifusion('todos');
    };

    window.cerrarModalDifusionMasiva = function () {
        detenerDifusion();
        const modal = document.getElementById('modalDifusionMasiva');
        if (modal) modal.style.display = 'none';
    };

    window.cargarSociosParaDifusion = async function (filtro) {
        const statusEl = document.getElementById('difusionStatusSocios');
        const btnIniciar = document.getElementById('btnIniciarDifusion');
        statusEl.innerHTML = '<i class="fa-solid fa-circle-notch fa-spin"></i> Consultando socios con teléfono...';
        btnIniciar.disabled = true;

        try {
            const resp = await fetch(`/Socios/GetSociosParaDifusion?filtro=${encodeURIComponent(filtro)}`);
            const data = await resp.json();

            if (data.ok) {
                difusionSocios = data.socios || [];
                statusEl.innerHTML = `<span style="color:#00f264;font-weight:700;"><i class="fa-solid fa-users"></i> ${difusionSocios.length} socio(s)</span> listos con número de WhatsApp registrado.`;
                btnIniciar.disabled = difusionSocios.length === 0;
                actualizarPreviewDifusion();
            } else {
                statusEl.innerHTML = `<span style="color:#f87171;">${data.msg || 'Error al obtener socios'}</span>`;
            }
        } catch (e) {
            statusEl.innerHTML = '<span style="color:#f87171;">Error de conexión al cargar socios.</span>';
        }
    };

    window.insertarVariableDifusion = function (variable) {
        const text = document.getElementById('difusionMensaje');
        const pos = text.selectionStart || text.value.length;
        text.value = text.value.substring(0, pos) + variable + text.value.substring(pos);
        text.focus();
        actualizarPreviewDifusion();
    };

    window.aplicarPlantillaDifusion = function (tipo) {
        const gym = window.GYM_CONFIG?.nombre || 'Gimnasio';
        const text = document.getElementById('difusionMensaje');

        if (tipo === 'promo') {
            text.value = `*PROMOCIÓN EN ${gym.toUpperCase()}*\n\nEstimado(a) {nombre}, le compartimos que tenemos promociones especiales en nuestras membresías y servicios.\n\nLe invitamos a pasar a recepción para consultar los beneficios y descuentos disponibles este mes.`;
        } else if (tipo === 'aviso') {
            text.value = `*AVISO IMPORTANTE - ${gym.toUpperCase()}*\n\nEstimado(a) {nombre},\nLe informamos que tendremos horarios especiales y avisos para nuestros socios.\n\nAgradecemos su atención y preferencia.`;
        } else if (tipo === 'deuda') {
            text.value = `*RECORDATORIO DE CUOTA - ${gym.toUpperCase()}*\n\nEstimado(a) {nombre}, le saludamos cordialmente para recordarle que presenta un saldo pendiente de *{deuda}*.\n\nPuede pasar al área de recepción para regularizar su cuenta. Agradecemos su puntualidad.`;
        } else if (tipo === 'motivacion') {
            text.value = `*RECORDATORIO DE ENTRENAMIENTO - ${gym.toUpperCase()}*\n\nEstimado(a) {nombre}, le recordamos que nuestras instalaciones y áreas de entrenamiento se encuentran disponibles para usted hoy.\n\nLe esperamos en recepción.`;
        }
        actualizarPreviewDifusion();
    };

    window.actualizarPreviewDifusion = function () {
        const template = document.getElementById('difusionMensaje')?.value || '';
        const previewEl = document.getElementById('difusionPreviewBubble');
        if (!previewEl) return;

        const socioEjemplo = difusionSocios.length > 0 ? difusionSocios[0] : { nombre: 'Carlos', nombreCompleto: 'Carlos Mendoza', deuda: 350.00 };
        const gym = window.GYM_CONFIG?.nombre || 'GIMNASIO';

        let formateado = template
            .replace(/{nombre}/g, socioEjemplo.nombre || 'Socio')
            .replace(/{socio}/g, socioEjemplo.nombreCompleto || socioEjemplo.nombre || 'Socio')
            .replace(/{gimnasio}/g, gym)
            .replace(/{deuda}/g, '$' + Number(socioEjemplo.deuda || 0).toFixed(2));

        // Markdown visual básico para el preview
        const htmlPreview = formateado
            .replace(/\*(.*?)\*/g, '<strong>$1</strong>')
            .replace(/\n/g, '<br/>');

        previewEl.innerHTML = htmlPreview || '<em style="color:#64748b;">Escribe un mensaje para previsualizar aquí...</em>';
    };

    window.iniciarDifusionMasiva = function () {
        if (!difusionSocios || difusionSocios.length === 0) {
            alert('No hay socios en el filtro seleccionado.');
            return;
        }

        const msg = document.getElementById('difusionMensaje').value.trim();
        if (!msg) {
            alert('Por favor escribe el mensaje que deseas enviar.');
            return;
        }

        // Mostrar panel de envío secuencial
        document.getElementById('difusionPanelConfig').style.display = 'none';
        document.getElementById('difusionPanelEjecucion').style.display = 'block';

        difusionIndiceActual = 0;
        difusionEnEjecucion = true;
        ejecutarEnvioSocioActual();
    };

    window.ejecutarEnvioSocioActual = function () {
        if (!difusionEnEjecucion) return;

        if (difusionIndiceActual >= difusionSocios.length) {
            finalizarDifusion();
            return;
        }

        const s = difusionSocios[difusionIndiceActual];
        const template = document.getElementById('difusionMensaje').value;
        const gym = window.GYM_CONFIG?.nombre || 'Gimnasio';

        const textoFinal = template
            .replace(/{nombre}/g, s.nombre || 'Socio')
            .replace(/{socio}/g, s.nombreCompleto || s.nombre || 'Socio')
            .replace(/{gimnasio}/g, gym)
            .replace(/{deuda}/g, '$' + Number(s.deuda || 0).toFixed(2));

        const tel = s.telefono.replace(/\D/g, '');
        const phoneWithCountry = tel.length === 10 ? ('52' + tel) : tel;
        const encodedText = encodeURIComponent(textoFinal);

        // Actualizar UI de ejecución
        const total = difusionSocios.length;
        const pct = Math.round(((difusionIndiceActual + 1) / total) * 100);
        document.getElementById('difusionBarraProgreso').style.width = pct + '%';
        document.getElementById('difusionBarraProgreso').textContent = pct + '%';
        document.getElementById('difusionContador').textContent = `Enviando ${difusionIndiceActual + 1} de ${total}`;
        document.getElementById('difusionNombreActual').textContent = `${s.nombreCompleto} (${s.telefono})`;

        // Envío directo reutilizando la misma ventana única de WhatsApp sin duplicar
        window.abrirVentanaWhatsAppCentrada(phoneWithCountry, textoFinal);

        // Verificar si auto-avance está activo
        const auto = document.getElementById('difusionCheckAuto').checked;
        const intervaloSegundos = parseInt(document.getElementById('difusionIntervalo').value) || 3;

        if (auto) {
            let tiempoRestante = intervaloSegundos;
            const btnSiguiente = document.getElementById('btnDifusionSiguiente');
            btnSiguiente.disabled = true;

            difusionTimer = setInterval(() => {
                tiempoRestante--;
                btnSiguiente.textContent = `Auto-siguiente en ${tiempoRestante}s...`;
                if (tiempoRestante <= 0) {
                    clearInterval(difusionTimer);
                    btnSiguiente.disabled = false;
                    btnSiguiente.innerHTML = '<i class="fa-solid fa-forward-step"></i> Enviar Siguiente Socio';
                    difusionIndiceActual++;
                    ejecutarEnvioSocioActual();
                }
            }, 1000);
        } else {
            const btnSiguiente = document.getElementById('btnDifusionSiguiente');
            btnSiguiente.disabled = false;
            btnSiguiente.innerHTML = '<i class="fa-solid fa-forward-step"></i> Enviar Siguiente Socio';
        }
    };

    window.avanzarManualDifusion = function () {
        if (difusionTimer) clearInterval(difusionTimer);
        difusionIndiceActual++;
        ejecutarEnvioSocioActual();
    };

    window.detenerDifusion = function () {
        difusionEnEjecucion = false;
        if (difusionTimer) clearInterval(difusionTimer);
        const panelEj = document.getElementById('difusionPanelEjecucion');
        const panelCfg = document.getElementById('difusionPanelConfig');
        if (panelEj) panelEj.style.display = 'none';
        if (panelCfg) panelCfg.style.display = 'block';
    };

    function finalizarDifusion() {
        difusionEnEjecucion = false;
        if (difusionTimer) clearInterval(difusionTimer);
        alert(`✓ ¡Difusión completada con éxito!\n\nSe enviaron mensajes a ${difusionSocios.length} socio(s).`);
        detenerDifusion();
        cerrarModalDifusionMasiva();
    }

    window.copiarListaTelefonosDifusion = function () {
        if (!difusionSocios || difusionSocios.length === 0) {
            alert('No hay socios para copiar teléfonos.');
            return;
        }
        const tels = difusionSocios.map(s => s.telefono.replace(/\D/g, '')).join(', ');
        navigator.clipboard.writeText(tels).then(() => {
            alert(`✓ Se copiaron ${difusionSocios.length} números de teléfono al portapapeles (separados por coma). Puedes pegarlos directamente en una lista de difusión de WhatsApp.`);
        });
    };

    function crearModalDifusionMasivaSiNoExiste() {
        if (document.getElementById('modalDifusionMasiva')) return;

        const modal = document.createElement('div');
        modal.id = 'modalDifusionMasiva';
        configurarModalOverlayBase(modal);

        modal.innerHTML = `
        <div class="pos-modal-card" style="max-width:700px;width:95%;padding:24px;border-radius:18px;background:var(--card-bg, #0f172a);border:1px solid rgba(255,255,255,0.12);box-shadow:0 25px 60px rgba(0,0,0,0.6);text-align:left;max-height:90vh;overflow-y:auto;position:relative;z-index:1000000;">
            <!-- Header -->
            <div style="display:flex;justify-content:space-between;align-items:center;margin-bottom:18px;">
                <div style="display:flex;align-items:center;gap:12px;">
                    <div style="width:44px;height:44px;border-radius:12px;background:#25D366;display:flex;align-items:center;justify-content:center;color:#0B1120;font-size:24px;box-shadow:0 4px 16px rgba(37,211,102,0.4);">
                        <i class="fa-brands fa-whatsapp"></i>
                    </div>
                    <div>
                        <div style="font-size:17px;font-weight:900;color:var(--text, #f8fafc);">Difusión Masiva por WhatsApp</div>
                        <div style="font-size:12px;color:var(--muted, #94a3b8);">Envía avisos, promociones y recordatorios a tus socios con 1 solo clic</div>
                    </div>
                </div>
                <button type="button" onclick="cerrarModalDifusionMasiva()" style="background:none;border:none;color:var(--muted,#94a3b8);font-size:24px;cursor:pointer;">&times;</button>
            </div>

            <!-- Panel de Configuración -->
            <div id="difusionPanelConfig">
                <!-- Filtro de Destinatarios -->
                <div style="background:rgba(255,255,255,0.03);border:1px solid rgba(255,255,255,0.08);border-radius:12px;padding:14px;margin-bottom:16px;">
                    <label style="display:block;font-size:12px;font-weight:700;margin-bottom:8px;color:var(--text,#f8fafc);">
                        <i class="fa-solid fa-filter" style="color:#38bdf8;"></i> Audiencia / Filtro de Socios:
                    </label>
                    <div style="display:flex;gap:8px;flex-wrap:wrap;align-items:center;">
                        <button type="button" class="btn btn-sm btn-outline active" onclick="cambiarFiltroDifusion('todos', this)" style="border-radius:20px;font-size:11.5px;">Todos con Celular</button>
                        <button type="button" class="btn btn-sm btn-outline" onclick="cambiarFiltroDifusion('activos', this)" style="border-radius:20px;font-size:11.5px;color:#00f264;border-color:rgba(0,242,100,0.3);">Solo Activos</button>
                        <button type="button" class="btn btn-sm btn-outline" onclick="cambiarFiltroDifusion('inactivos', this)" style="border-radius:20px;font-size:11.5px;color:#f87171;border-color:rgba(239,68,68,0.3);">Solo Inactivos</button>
                        <button type="button" class="btn btn-sm btn-outline" onclick="cambiarFiltroDifusion('deudores', this)" style="border-radius:20px;font-size:11.5px;color:#fbbf24;border-color:rgba(251,191,36,0.3);">Con Deuda</button>
                    </div>
                    <div id="difusionStatusSocios" style="margin-top:10px;font-size:12px;color:var(--muted,#94a3b8);"></div>
                </div>

                <!-- Plantillas -->
                <div style="margin-bottom:12px;">
                    <div style="font-size:12px;font-weight:700;margin-bottom:6px;color:var(--text,#f8fafc);">
                        <i class="fa-solid fa-list-check" style="color:#fbbf24;"></i> Plantillas Rápidas:
                    </div>
                    <div style="display:flex;gap:6px;flex-wrap:wrap;">
                        <button type="button" onclick="aplicarPlantillaDifusion('promo')" class="btn btn-sm" style="background:rgba(251,191,36,0.12);border:1px solid rgba(251,191,36,0.3);color:#fbbf24;font-size:11px;border-radius:6px;">Promoción</button>
                        <button type="button" onclick="aplicarPlantillaDifusion('aviso')" class="btn btn-sm" style="background:rgba(56,189,248,0.12);border:1px solid rgba(56,189,248,0.3);color:#38bdf8;font-size:11px;border-radius:6px;">Aviso General</button>
                        <button type="button" onclick="aplicarPlantillaDifusion('deuda')" class="btn btn-sm" style="background:rgba(239,68,68,0.12);border:1px solid rgba(239,68,68,0.3);color:#f87171;font-size:11px;border-radius:6px;">Recordatorio Saldo</button>
                        <button type="button" onclick="aplicarPlantillaDifusion('motivacion')" class="btn btn-sm" style="background:rgba(0,242,100,0.12);border:1px solid rgba(0,242,100,0.3);color:#00f264;font-size:11px;border-radius:6px;">Recordatorio General</button>
                    </div>
                </div>

                <!-- Editor de Mensaje -->
                <div style="margin-bottom:14px;">
                    <div style="display:flex;justify-content:space-between;align-items:center;margin-bottom:6px;">
                        <label style="font-size:12px;font-weight:700;color:var(--text,#f8fafc);margin:0;">
                            <i class="fa-solid fa-pen-to-square"></i> Mensaje a Enviar:
                        </label>
                        <div style="display:flex;gap:4px;">
                            <button type="button" onclick="insertarVariableDifusion('{nombre}')" class="btn btn-sm" style="background:rgba(255,255,255,0.06);color:#94a3b8;font-size:10.5px;padding:2px 6px;border-radius:4px;" title="Inserta el primer nombre del socio">+{nombre}</button>
                            <button type="button" onclick="insertarVariableDifusion('{socio}')" class="btn btn-sm" style="background:rgba(255,255,255,0.06);color:#94a3b8;font-size:10.5px;padding:2px 6px;border-radius:4px;" title="Inserta el nombre completo">+{socio}</button>
                            <button type="button" onclick="insertarVariableDifusion('{deuda}')" class="btn btn-sm" style="background:rgba(255,255,255,0.06);color:#94a3b8;font-size:10.5px;padding:2px 6px;border-radius:4px;" title="Inserta la deuda del socio">+{deuda}</button>
                            <button type="button" onclick="insertarVariableDifusion('{gimnasio}')" class="btn btn-sm" style="background:rgba(255,255,255,0.06);color:#94a3b8;font-size:10.5px;padding:2px 6px;border-radius:4px;" title="Inserta el nombre del gimnasio">+{gimnasio}</button>
                        </div>
                    </div>
                    <textarea id="difusionMensaje" rows="5" class="form-control" onkeyup="actualizarPreviewDifusion()"
                              style="width:100%;box-sizing:border-box;background:rgba(0,0,0,0.3);border:1px solid rgba(255,255,255,0.18);border-radius:10px;padding:12px;color:#f8fafc;font-size:12.5px;line-height:1.4;resize:vertical;"
                              placeholder="Escribe tu mensaje aquí. Puedes usar negritas con *texto* y las variables {nombre}, {deuda}, {gimnasio}..."></textarea>
                </div>

                <!-- Preview Chat Bubble -->
                <div style="margin-bottom:18px;">
                    <div style="font-size:11px;font-weight:700;color:var(--muted,#94a3b8);margin-bottom:6px;">
                        <i class="fa-solid fa-eye"></i> Vista Previa:
                    </div>
                    <div style="background:#0b141a;border-radius:12px;padding:12px;border:1px solid rgba(255,255,255,0.06);">
                        <div id="difusionPreviewBubble" style="background:#005c4b;color:#e9edef;padding:8px 12px;border-radius:8px;border-top-left-radius:2px;max-width:85%;font-size:12px;line-height:1.35;word-break:break-word;display:inline-block;box-shadow:0 1px 2px rgba(0,0,0,0.3);">
                            Escribe un mensaje para previsualizar aquí...
                        </div>
                    </div>
                </div>

                <!-- Botones Acción -->
                <div style="display:flex;gap:10px;flex-wrap:wrap;justify-content:space-between;align-items:center;">
                    <button type="button" onclick="copiarListaTelefonosDifusion()" class="btn btn-sm" style="background:rgba(255,255,255,0.06);border:1px solid rgba(255,255,255,0.15);color:#cbd5e1;padding:8px 12px;border-radius:8px;font-size:11.5px;">
                        <i class="fa-solid fa-copy"></i> Copiar Teléfonos
                    </button>
                    <div style="display:flex;gap:10px;">
                        <button type="button" onclick="cerrarModalDifusionMasiva()" class="btn btn-sm" style="background:transparent;border:none;color:#94a3b8;font-size:12px;">
                            Cancelar
                        </button>
                        <button type="button" id="btnIniciarDifusion" onclick="iniciarDifusionMasiva()" class="btn"
                                style="background:#25D366;color:#0B1120;font-weight:900;font-size:13.5px;padding:11px 22px;border-radius:10px;border:none;box-shadow:0 4px 16px rgba(37,211,102,0.4);display:flex;align-items:center;gap:8px;cursor:pointer;">
                            <i class="fa-brands fa-whatsapp" style="font-size:16px;"></i> Iniciar Envío
                        </button>
                    </div>
                </div>
            </div>

            <!-- Panel de Ejecución Secuencial -->
            <div id="difusionPanelEjecucion" style="display:none;text-align:center;padding:16px 0;">
                <div style="font-size:14px;font-weight:800;color:var(--text,#f8fafc);margin-bottom:4px;" id="difusionContador">
                    Enviando 1 de 10
                </div>
                <div style="font-size:13px;color:#25D366;font-weight:700;margin-bottom:14px;" id="difusionNombreActual">
                    Juan Pérez (961-xxx-xxxx)
                </div>

                <!-- Barra de progreso -->
                <div style="background:rgba(255,255,255,0.08);border-radius:10px;height:20px;overflow:hidden;margin-bottom:18px;position:relative;">
                    <div id="difusionBarraProgreso" style="background:linear-gradient(90deg, #25D366, #128C7E);height:100%;width:0%;transition:width 0.3s ease;font-size:11px;font-weight:800;color:#0B1120;line-height:20px;">
                        0%
                    </div>
                </div>

                <div style="background:rgba(37,211,102,0.08);border:1px solid rgba(37,211,102,0.25);border-radius:10px;padding:12px;margin-bottom:18px;font-size:12px;color:#cbd5e1;text-align:left;">
                    <i class="fa-solid fa-circle-info" style="color:#25D366;"></i> <strong>Envío directo:</strong> Cada mensaje se transfiere automáticamente a WhatsApp sin abrir pestañas en Chrome. Puedes usar el modo automático para avanzar solo o pulsar <em>Enviar Siguiente</em> a tu propio ritmo.
                </div>

                <!-- Configuración de intervalo y controles -->
                <div style="display:flex;gap:12px;justify-content:center;align-items:center;margin-bottom:20px;flex-wrap:wrap;">
                    <label style="display:flex;align-items:center;gap:6px;font-size:12px;color:var(--text,#f8fafc);cursor:pointer;">
                        <input type="checkbox" id="difusionCheckAuto" checked style="width:16px;height:16px;accent-color:#25D366;" />
                        <span>Avanzar automáticamente cada</span>
                    </label>
                    <select id="difusionIntervalo" class="form-control" style="width:80px;display:inline-block;padding:4px 8px;font-size:12px;background:rgba(255,255,255,0.06);color:#fff;border-radius:6px;">
                        <option value="2">2 seg</option>
                        <option value="3" selected>3 seg</option>
                        <option value="4">4 seg</option>
                        <option value="6">6 seg</option>
                    </select>
                </div>

                <div style="display:flex;gap:12px;justify-content:center;">
                    <button type="button" onclick="detenerDifusion()" class="btn btn-outline" style="border-color:#ef4444;color:#ef4444;padding:10px 18px;border-radius:9px;font-size:12px;font-weight:700;">
                        <i class="fa-solid fa-stop"></i> Detener
                    </button>
                    <button type="button" id="btnDifusionSiguiente" onclick="avanzarManualDifusion()" class="btn" style="background:#25D366;color:#0B1120;font-weight:800;padding:10px 22px;border-radius:9px;font-size:13px;border:none;">
                        <i class="fa-solid fa-forward-step"></i> Enviar Siguiente Socio
                    </button>
                </div>
            </div>
        </div>`;

        document.body.appendChild(modal);
    }

    window.cambiarFiltroDifusion = function (filtro, btn) {
        document.querySelectorAll('#modalDifusionMasiva .btn-outline').forEach(b => b.classList.remove('active'));
        if (btn) btn.classList.add('active');
        window.cargarSociosParaDifusion(filtro);
    };

    // ── 8. MODAL DE CONFIRMACIÓN POST-ABONO (IMPRIMIR / WHATSAPP) ─────────────
    window.abrirModalPostAbono = function (idPago, mensajeExito) {
        crearModalPostAbonoSiNoExiste();
        const modal = document.getElementById('modalPostAbono');
        document.getElementById('postAbonoMensaje').textContent = mensajeExito || 'Abono registrado con éxito en Caja.';
        document.getElementById('postAbonoBtnPrint').onclick = function () {
            modal.style.display = 'none';
            if (window.imprimirComprobanteAbono) {
                window.imprimirComprobanteAbono(idPago);
            }
        };
        document.getElementById('postAbonoBtnWhats').onclick = function () {
            modal.style.display = 'none';
            if (window.enviarTicketAbonoWhatsApp) {
                window.enviarTicketAbonoWhatsApp(idPago);
            }
        };
        modal.style.display = 'flex';
    };

    window.cerrarModalPostAbono = function () {
        const modal = document.getElementById('modalPostAbono');
        if (modal) modal.style.display = 'none';
        setTimeout(() => { window.location.reload(); }, 300);
    };

    function crearModalPostAbonoSiNoExiste() {
        if (document.getElementById('modalPostAbono')) return;

        const modal = document.createElement('div');
        modal.id = 'modalPostAbono';
        configurarModalOverlayBase(modal);

        modal.innerHTML = `
        <div class="pos-modal-card" style="max-width:440px;width:95%;padding:26px;border-radius:18px;background:var(--card-bg, #0f172a);border:1px solid rgba(255,255,255,0.12);box-shadow:0 25px 60px rgba(0,0,0,0.6);text-align:center;position:relative;z-index:1000000;">
            <div style="width:54px;height:54px;border-radius:50%;background:rgba(0,242,100,0.12);border:2px solid #00f264;display:flex;align-items:center;justify-content:center;color:#00f264;font-size:26px;margin:0 auto 16px auto;">
                <i class="fa-solid fa-check"></i>
            </div>
            <div style="font-size:17px;font-weight:900;color:var(--text, #f8fafc);margin-bottom:6px;">¡Abono Registrado!</div>
            <div id="postAbonoMensaje" style="font-size:12.5px;color:var(--muted, #94a3b8);margin-bottom:20px;line-height:1.4;">
                El abono se ingresó correctamente a la caja.
            </div>

            <div style="display:flex;flex-direction:column;gap:10px;">
                <button type="button" id="postAbonoBtnWhats" class="btn"
                        style="background:#25D366;border:none;color:#0B1120;font-weight:800;font-size:13.5px;padding:12px 18px;border-radius:10px;cursor:pointer;display:flex;align-items:center;justify-content:center;gap:8px;box-shadow:0 4px 14px rgba(37,211,102,0.35);">
                    <i class="fa-brands fa-whatsapp" style="font-size:18px;"></i> Enviar Ticket por WhatsApp
                </button>

                <button type="button" id="postAbonoBtnPrint" class="btn btn-outline"
                        style="border:1px solid rgba(56,189,248,0.4);color:#38bdf8;padding:11px 18px;border-radius:10px;font-size:13px;font-weight:700;cursor:pointer;display:flex;align-items:center;justify-content:center;gap:8px;">
                    <i class="fa-solid fa-print"></i> Imprimir Ticket Físico
                </button>

                <button type="button" onclick="cerrarModalPostAbono()" class="btn btn-sm"
                        style="background:transparent;border:none;color:#94a3b8;font-size:12px;margin-top:6px;cursor:pointer;">
                    Listo, terminar
                </button>
            </div>
        </div>`;

        document.body.appendChild(modal);
    }

})();

