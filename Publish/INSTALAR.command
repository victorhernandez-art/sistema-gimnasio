#!/bin/bash
# ============================================================
#  GymWeb - Sistema de Administracion de Gimnasio
#  INSTALADOR para macOS
#  Doble clic para ejecutar
# ============================================================

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
APP="$SCRIPT_DIR/GymWeb"
APPSETTINGS="$SCRIPT_DIR/appsettings.json"
INICIAR_SH="$SCRIPT_DIR/IniciarGymWeb.sh"
DESKTOP="$HOME/Desktop"
LAUNCH_LABEL="com.gymweb.service"
PLIST="$HOME/Library/LaunchAgents/$LAUNCH_LABEL.plist"
PUERTO=5080

echo ""
echo "  ============================================================"
echo "   GymWeb - Sistema de Administracion de Gimnasio"
echo "   INSTALADOR macOS"
echo "  ============================================================"
echo ""

# ---- Verificar que GymWeb existe ----
if [ ! -f "$APP" ]; then
    echo "  ERROR: No se encontro el archivo GymWeb en esta carpeta."
    echo ""
    echo "  Nota: Para Mac necesitas la version compilada para macOS."
    echo "  Ejecuta en la carpeta del proyecto:"
    echo "    dotnet publish -r osx-arm64 -c Release --self-contained"
    echo "  (usa osx-x64 si tu Mac es Intel)"
    echo ""
    read -rp "  Presiona Enter para salir..."
    exit 1
fi

chmod +x "$APP"

# ---- Paso 1: Limpiar instalacion anterior ----
echo "  [1/5] Limpiando instalacion anterior..."
launchctl stop "$LAUNCH_LABEL" 2>/dev/null
launchctl bootout "gui/$(id -u)/$LAUNCH_LABEL" 2>/dev/null
launchctl unload "$PLIST" 2>/dev/null
sleep 2
[ -f "$PLIST" ] && rm -f "$PLIST"
echo "         Listo."
echo ""

# ---- Paso 2: Verificar puerto ----
echo "  [2/5] Verificando disponibilidad del puerto $PUERTO..."

puerto_ocupado() {
    lsof -i ":$1" -sTCP:LISTEN -t &>/dev/null
}

if puerto_ocupado $PUERTO; then
    PID=$(lsof -i ":$PUERTO" -sTCP:LISTEN -t 2>/dev/null | head -1)
    PROCESO=$(ps -p "$PID" -o comm= 2>/dev/null)
    echo "         Puerto $PUERTO ocupado por: $PROCESO (PID $PID)"

    # No matar MySQL (puerto 3306 es diferente, pero por si acaso verificamos el nombre)
    if [[ "$PROCESO" != *"mysql"* ]] && [[ "$PROCESO" != *"mysqld"* ]]; then
        echo "         Liberando proceso..."
        kill -9 "$PID" 2>/dev/null
        sleep 2
    fi

    # Verificar si quedo libre
    if puerto_ocupado $PUERTO; then
        echo "         No se pudo liberar. Buscando puerto alternativo..."
        for P in 5081 5082 5083 5084 5085; do
            if ! puerto_ocupado $P; then
                PUERTO=$P
                break
            fi
        done
        echo "         Usando puerto alternativo: $PUERTO"
        # Actualizar appsettings.json
        sed -i '' "s|\"Urls\":.*|\"Urls\": \"http://localhost:$PUERTO\",|g" "$APPSETTINGS"
    else
        echo "         Puerto $PUERTO liberado."
    fi
else
    echo "         Puerto $PUERTO disponible."
fi
echo ""

# ---- Paso 3: Registrar como servicio de inicio (LaunchAgent) ----
echo "  [3/5] Instalando servicio de inicio automatico..."
mkdir -p "$HOME/Library/LaunchAgents"

cat > "$PLIST" << PLISTEOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>Label</key>
    <string>$LAUNCH_LABEL</string>
    <key>ProgramArguments</key>
    <array>
        <string>$APP</string>
    </array>
    <key>RunAtLoad</key>
    <true/>
    <key>KeepAlive</key>
    <true/>
    <key>WorkingDirectory</key>
    <string>$SCRIPT_DIR</string>
    <key>StandardOutPath</key>
    <string>$SCRIPT_DIR/gymweb.log</string>
    <key>StandardErrorPath</key>
    <string>$SCRIPT_DIR/gymweb-error.log</string>
</dict>
</plist>
PLISTEOF

launchctl load "$PLIST" 2>/dev/null || launchctl bootstrap "gui/$(id -u)" "$PLIST" 2>/dev/null
echo "         Servicio registrado (inicia automaticamente al encender el Mac)."
echo ""

# ---- Paso 4: Iniciar el servicio ----
echo "  [4/5] Iniciando GymWeb..."
launchctl start "$LAUNCH_LABEL" 2>/dev/null
sleep 4

if puerto_ocupado $PUERTO; then
    echo "         Servicio corriendo en http://localhost:$PUERTO"
else
    echo ""
    echo "  ADVERTENCIA: El servicio podria no haber iniciado."
    echo "  Causas comunes:"
    echo "    - MySQL no esta corriendo"
    echo "    - Revisa el log: $SCRIPT_DIR/gymweb-error.log"
    echo ""
fi
echo ""

# ---- Paso 5: Crear acceso directo en el Escritorio ----
echo "  [5/5] Creando acceso directo en el Escritorio..."

cat > "$INICIAR_SH" << SHEOF
#!/bin/bash
open "http://localhost:$PUERTO"
SHEOF
chmod +x "$INICIAR_SH"

# Crear el .command en el Escritorio (doble clic abre el navegador)
DESKTOP_CMD="$DESKTOP/GymWeb.command"
cat > "$DESKTOP_CMD" << CMDEOF
#!/bin/bash
open "http://localhost:$PUERTO"
CMDEOF
chmod +x "$DESKTOP_CMD"

if [ -f "$DESKTOP_CMD" ]; then
    echo "         Acceso directo 'GymWeb.command' creado en el Escritorio."
else
    echo "         No se pudo crear el acceso directo (no critico)."
fi
echo ""

echo "  ============================================================"
echo "   INSTALACION COMPLETADA"
echo "  ============================================================"
echo ""
echo "    Acceso:     http://localhost:$PUERTO"
echo "    GymWeb inicia automaticamente al encender el Mac."
echo "    Doble clic en 'GymWeb.command' en el Escritorio para abrir."
echo ""
echo "    IMPORTANTE: MySQL debe estar corriendo para que"
echo "    la aplicacion funcione correctamente."
echo "  ============================================================"
echo ""

sleep 3
open "http://localhost:$PUERTO"

read -rp "  Presiona Enter para cerrar..."
