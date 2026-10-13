/**
 * GymPro - Live Sync Engine
 * Sincronización automática silenciosa en tiempo real entre celulares, tablets y PC principal.
 */
(function () {
    let currentVersion = null;
    let isUpdating = false;
    let pollInterval = null;

    const path = window.location.pathname.toLowerCase();
    let modulo = "global";
    if (path.includes("/socios")) modulo = "socios";
    else if (path.includes("/registro") || path.includes("/visitas")) modulo = "visitas";
    else if (path.includes("/pagos") || path.includes("/ventas")) modulo = "ventas";
    else if (path === "/" || path.includes("/home")) modulo = "dashboard";

    async function checkSync() {
        if (isUpdating) return;

        // No actualizar si el usuario tiene un modal abierto o está escribiendo en un formulario
        if (document.querySelector('.modal.show, .modal[style*="display: block"], .modal-backdrop')) return;
        const activeElem = document.activeElement;
        if (activeElem && (activeElem.tagName === 'TEXTAREA' || (activeElem.tagName === 'INPUT' && activeElem.id !== 'tableSearch' && activeElem.id !== 'buscarSocioVisitas'))) {
            return;
        }

        try {
            const resp = await fetch(`/api/livesync/version?modulo=${encodeURIComponent(modulo)}&_t=${Date.now()}`, { cache: "no-store" });
            if (!resp.ok) return;
            const data = await resp.json();
            if (!data || !data.version) return;

            if (currentVersion === null) {
                currentVersion = data.version;
                return;
            }

            if (data.version !== currentVersion) {
                currentVersion = data.version;
                await ejecutarSincronizacionSilenciosa();
            }
        } catch (err) {
            // Silencioso ante pérdida temporal de conexión Wi-Fi
        }
    }

    async function ejecutarSincronizacionSilenciosa() {
        if (isUpdating) return;
        isUpdating = true;

        try {
            if (modulo === "socios") {
                const resp = await fetch(window.location.href, { cache: "no-store" });
                if (!resp.ok) return;
                const html = await resp.text();
                const parser = new DOMParser();
                const doc = parser.parseFromString(html, "text/html");

                // Actualizar tbody de tablaSocios
                const newTbody = doc.querySelector("#tablaSocios tbody");
                const curTbody = document.querySelector("#tablaSocios tbody");
                if (newTbody && curTbody) {
                    curTbody.innerHTML = newTbody.innerHTML;
                }

                // Actualizar contadores y píldoras de filtros rápidos
                const newContador = doc.querySelector("#labelContadorSocios");
                const curContador = document.querySelector("#labelContadorSocios");
                if (newContador && curContador) curContador.innerHTML = newContador.innerHTML;

                const newFiltros = doc.querySelectorAll(".filtro-btn");
                const curFiltros = document.querySelectorAll(".filtro-btn");
                if (newFiltros.length === curFiltros.length) {
                    newFiltros.forEach((nf, idx) => {
                        curFiltros[idx].innerHTML = nf.innerHTML;
                    });
                }

                // Reaplicar búsqueda y filtro activo sin perder el texto ingresado
                if (typeof window.ejecutarBusquedaYFiltro === "function") {
                    window.ejecutarBusquedaYFiltro();
                } else if (typeof window.filtrarSociosActuales === "function") {
                    window.filtrarSociosActuales();
                }

                mostrarBadgeSync("✓ Socios sincronizados en tiempo real");
            } else if (modulo === "visitas") {
                const resp = await fetch(window.location.href, { cache: "no-store" });
                if (!resp.ok) return;
                const html = await resp.text();
                const parser = new DOMParser();
                const doc = parser.parseFromString(html, "text/html");

                const newTbody = doc.querySelector(".gym-table tbody");
                const curTbody = document.querySelector(".gym-table tbody");
                if (newTbody && curTbody) {
                    curTbody.innerHTML = newTbody.innerHTML;
                }

                if (typeof window.ejecutarBusquedaVisitas === "function") {
                    window.ejecutarBusquedaVisitas();
                }

                mostrarBadgeSync("✓ Visitas sincronizadas en tiempo real");
            }
        } catch (ex) {
            console.warn("Auto-sync error:", ex);
        } finally {
            isUpdating = false;
        }
    }

    function mostrarBadgeSync(texto) {
        let badge = document.getElementById("liveSyncToastBadge");
        if (!badge) {
            badge = document.createElement("div");
            badge.id = "liveSyncToastBadge";
            badge.style.cssText = "position:fixed;bottom:22px;right:22px;background:rgba(15,23,42,0.92);backdrop-filter:blur(10px);border:1px solid rgba(0,242,100,0.45);color:#00f264;padding:9px 18px;border-radius:30px;font-size:12.5px;font-weight:700;box-shadow:0 8px 24px rgba(0,0,0,0.5);display:flex;align-items:center;gap:8px;z-index:999999;transition:opacity 0.3s ease, transform 0.3s cubic-bezier(0.175,0.885,0.32,1.275);opacity:0;transform:translateY(15px);pointer-events:none;";
            document.body.appendChild(badge);
        }

        badge.innerHTML = `<i class="fa-solid fa-arrows-rotate fa-spin" style="color:#00f264;font-size:13px;"></i> <span>${texto}</span>`;
        badge.style.opacity = "1";
        badge.style.transform = "translateY(0)";

        setTimeout(() => {
            badge.style.opacity = "0";
            badge.style.transform = "translateY(15px)";
        }, 2400);
    }

    // Consultar cada 4 segundos
    pollInterval = setInterval(checkSync, 4000);

    // Iniciar chequeo inmediato al ganar foco en la ventana
    window.addEventListener("focus", () => {
        setTimeout(checkSync, 200);
    });

    // Primer chequeo al cargar
    setTimeout(checkSync, 1000);
})();
