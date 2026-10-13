# ==============================================================================
#  GymWeb - Generador de Micro-Parche Ligero de Código / Vistas (~2 MB)
# ==============================================================================
$ErrorActionPreference = "Stop"

$root = $PSScriptRoot
$dist = Join-Path $root "dist"
$patchOut = Join-Path $dist "Parche-Ligero"
$patchFiles = Join-Path $patchOut "archivos"
$tempBuild = Join-Path $dist "temp_light_build"
$zipOut = Join-Path $dist "Parche_Ligero_GymWeb.zip"

Write-Host "`n============================================================" -ForegroundColor Cyan
Write-Host "   GENERANDO MICRO-PARCHE LIGERO GYMWEB (~2 MB)" -ForegroundColor Cyan
Write-Host "   (Actualizaciones de código C#, vistas Razor y correcciones)" -ForegroundColor Cyan
Write-Host "============================================================`n" -ForegroundColor Cyan

# 1. Limpieza previa
Write-Host " [1/4] Limpiando salidas anteriores..." -ForegroundColor Yellow
if (Test-Path $patchOut) { Remove-Item -Recurse -Force $patchOut }
if (Test-Path $tempBuild) { Remove-Item -Recurse -Force $tempBuild }
New-Item -ItemType Directory -Force -Path $patchFiles | Out-Null

# 2. Compilar GymWeb (Release)
Write-Host " [2/4] Compilando GymWeb (Release)..." -ForegroundColor Yellow
$gymWebProj = Join-Path $root "GymWeb\GymWeb.csproj"
& dotnet publish $gymWebProj -c Release -o $tempBuild --nologo -v quiet

# 3. Copiar GymWeb.dll, vistas y assets modificados
Write-Host " [3/4] Preparando archivos esenciales de actualización..." -ForegroundColor Yellow
Copy-Item -Force (Join-Path $tempBuild "GymWeb.dll") (Join-Path $patchFiles "GymWeb.dll")
Copy-Item -Force (Join-Path $tempBuild "GymWeb.pdb") (Join-Path $patchFiles "GymWeb.pdb")

# Usar el visor nativo autocontenido x64 probado de GymApp_bin
$selfContainedApp = Join-Path $root "GymApp_bin"
if (Test-Path (Join-Path $selfContainedApp "Gym.exe")) {
    Copy-Item -Force (Join-Path $selfContainedApp "Gym.exe") (Join-Path $patchFiles "Gym.exe")
    Copy-Item -Force (Join-Path $selfContainedApp "Gym.dll") (Join-Path $patchFiles "Gym.dll")
    Copy-Item -Force (Join-Path $selfContainedApp "Gym.pdb") (Join-Path $patchFiles "Gym.pdb")
    Copy-Item -Force (Join-Path $selfContainedApp "Gym.runtimeconfig.json") (Join-Path $patchFiles "Gym.runtimeconfig.json")
    
    # Sincronizar también en la raíz y en Publish
    Copy-Item -Force (Join-Path $selfContainedApp "Gym.exe") (Join-Path $root "Gym.exe") -ErrorAction SilentlyContinue
    Copy-Item -Force (Join-Path $selfContainedApp "Gym.dll") (Join-Path $root "Gym.dll") -ErrorAction SilentlyContinue
    Copy-Item -Force (Join-Path $selfContainedApp "Gym.runtimeconfig.json") (Join-Path $root "Gym.runtimeconfig.json") -ErrorAction SilentlyContinue
    Copy-Item -Force (Join-Path $selfContainedApp "Gym.exe") (Join-Path $root "Publish\Gym.exe") -ErrorAction SilentlyContinue
    Copy-Item -Force (Join-Path $selfContainedApp "Gym.dll") (Join-Path $root "Publish\Gym.dll") -ErrorAction SilentlyContinue
    Copy-Item -Force (Join-Path $selfContainedApp "Gym.runtimeconfig.json") (Join-Path $root "Publish\Gym.runtimeconfig.json") -ErrorAction SilentlyContinue
}

if (Test-Path (Join-Path $tempBuild "wwwroot")) {
    Copy-Item -Recurse -Force (Join-Path $tempBuild "wwwroot") (Join-Path $patchFiles "wwwroot")
    Remove-Item -Force -ErrorAction SilentlyContinue (Join-Path $patchFiles "wwwroot\img\logo-custom.png")
    Remove-Item -Force -ErrorAction SilentlyContinue (Join-Path $patchFiles "wwwroot\img\fondo-custom.jpg")
    Remove-Item -Recurse -Force -ErrorAction SilentlyContinue (Join-Path $patchFiles "wwwroot\img\socios")
    Remove-Item -Force -ErrorAction SilentlyContinue (Join-Path $patchFiles "wwwroot\img\productos\prod_*.*")
    Remove-Item -Recurse -Force -ErrorAction SilentlyContinue (Join-Path $patchFiles "wwwroot\backups")
}

# Incluir Puente Biométrico DigitalPersona (compilado)
$dpSource = Join-Path $root "DigitalPersonaBridge\publish"
if (-not (Test-Path (Join-Path $dpSource "DigitalPersonaBridge.exe"))) {
    Write-Host "       Compilando puente DigitalPersonaBridge..." -ForegroundColor Gray
    & dotnet publish (Join-Path $root "DigitalPersonaBridge\DigitalPersonaBridge.csproj") -c Release -o $dpSource --nologo -v quiet
}
if (Test-Path $dpSource) {
    $dpDest = Join-Path $patchFiles "PuenteBiometrico"
    Copy-Item -Recurse -Force $dpSource $dpDest
    Write-Host "       Puente biometrico DigitalPersona incluido." -ForegroundColor Green
}

# Incluir Modulo WhatsApp Gateway (Envio Masivo 100% Automatico sin navegador)
$waSource = Join-Path $root "WhatsAppGateway"
if (Test-Path $waSource) {
    $waDest = Join-Path $patchFiles "WhatsAppGateway"
    if (-not (Test-Path $waDest)) { New-Item -ItemType Directory -Path $waDest -Force | Out-Null }
    Copy-Item -Force (Join-Path $waSource "server.js") $waDest
    Copy-Item -Force (Join-Path $waSource "package.json") $waDest
    Copy-Item -Force (Join-Path $waSource "INICIAR-WHATSAPP-GATEWAY.bat") $waDest
    if (Test-Path (Join-Path $waSource "node_modules")) {
        Copy-Item -Recurse -Force (Join-Path $waSource "node_modules") (Join-Path $waDest "node_modules")
    }
    Copy-Item -Force (Join-Path $waSource "INICIAR-WHATSAPP-GATEWAY.bat") (Join-Path $patchOut "INICIAR-WHATSAPP-GATEWAY.bat") -ErrorAction SilentlyContinue
    Write-Host "       Modulo WhatsApp Gateway incluido." -ForegroundColor Green
}

# Copiar script de actualización automática y utilitarios
Copy-Item -Force (Join-Path $root "Publish\ACTUALIZAR.bat") (Join-Path $patchOut "ACTUALIZAR.bat")
Copy-Item -Force (Join-Path $root "Publish\actualizar.ps1") (Join-Path $patchOut "actualizar.ps1")
Copy-Item -Force (Join-Path $root "Publish\Iniciar GymWeb.bat") (Join-Path $patchFiles "Iniciar GymWeb.bat") -ErrorAction SilentlyContinue
Copy-Item -Force (Join-Path $root "Publish\AbrirGymWeb.vbs") (Join-Path $patchFiles "AbrirGymWeb.vbs") -ErrorAction SilentlyContinue
Copy-Item -Force (Join-Path $root "HABILITAR-RED-LAN.bat") (Join-Path $patchFiles "HABILITAR-RED-LAN.bat") -ErrorAction SilentlyContinue
Copy-Item -Force (Join-Path $root "INICIAR-PUENTE-BIOMETRICO.bat") (Join-Path $patchFiles "INICIAR-PUENTE-BIOMETRICO.bat") -ErrorAction SilentlyContinue
Copy-Item -Force (Join-Path $root "INICIAR-PUENTE-BIOMETRICO.bat") (Join-Path $patchOut "INICIAR-PUENTE-BIOMETRICO.bat") -ErrorAction SilentlyContinue
Copy-Item -Force (Join-Path $root "PROBAR-LECTOR-USB.bat") (Join-Path $patchFiles "PROBAR-LECTOR-USB.bat") -ErrorAction SilentlyContinue
Copy-Item -Force (Join-Path $root "PROBAR-LECTOR-USB.bat") (Join-Path $patchOut "PROBAR-LECTOR-USB.bat") -ErrorAction SilentlyContinue
Copy-Item -Force (Join-Path $root "PROBAR-LECTOR-USB.ps1") (Join-Path $patchFiles "PROBAR-LECTOR-USB.ps1") -ErrorAction SilentlyContinue

# Generar archivo de instrucciones amigable para el cliente
$readmePath = Join-Path $patchOut "LEEME_INSTRUCCIONES.txt"
$readmeText = @"
========================================================================
   GYMPRO / GYMWEB - PARCHE DE ACTUALIZACION v2.5
   Red Local (Celular/Tablet) + Soporte Lector Huella + WhatsApp Masivo
========================================================================

¿COMO INSTALAR EL PARCHE? (SUPER FACIL - 1 PASO)
------------------------------------------------------------------------
1. Extrae (descomprime) este archivo ZIP en cualquier carpeta.
2. Cierra el sistema GymPro si lo tienes abierto.
3. Haz clic derecho en 'ACTUALIZAR.bat' y selecciona:
   -> 'Ejecutar como administrador'.
4. Espera a que termine (toma unos 5 segundos). ¡Y LISTO!

NOTA DE SEGURIDAD PARA TU BASE DE DATOS:
Tus socios, cobros, fotos, membresias y pagos NO se tocan ni se borran.
Tu base de datos queda 100% intacta y segura.

NOVEDADES INCLUIDAS EN ESTE PARCHE:
------------------------------------------------------------------------
1. ACCESO POR RED LOCAL Y SINCRONIZACION EN TIEMPO REAL:
   - Conectate desde cualquier celular o tablet escaneando el codigo QR.
   - Todo cambio que hagas en el celular (ej. editar socio) se actualiza
     al instante en la PC principal sin recargar la pantalla.

2. LECTOR DE HUELLA DIGITALPERSONA U.ARE.U 4500:
   - Soporte nativo para enrolar y leer huellas dactilares.
   - El puente biométrico inicia automáticamente en segundo plano junto con el sistema.
   - En el sistema verás el indicador 'Lector conectado' en color verde.

3. DIFUSION MASIVA POR WHATSAPP 100% AUTOMATICA:
   - Modo 1 (Recomendado): Vincula tu WhatsApp escaneando el código QR
     desde el sistema (Bot en segundo plano). El envío se realiza 100%
     desatendido directamente por internet sin abrir pestañas ni mover el mouse.
   - Modo 2: Envío por navegador con Auto-Enter automático de Windows.
========================================================================
"@
[System.IO.File]::WriteAllText($readmePath, $readmeText, [System.Text.Encoding]::UTF8)

(Get-Item (Join-Path $patchOut "ACTUALIZAR.bat")).LastWriteTime = Get-Date
(Get-Item (Join-Path $patchOut "actualizar.ps1")).LastWriteTime = Get-Date

try { Remove-Item -Recurse -Force $tempBuild -ErrorAction SilentlyContinue } catch { }
try { Remove-Item -Recurse -Force $tempAppBuild -ErrorAction SilentlyContinue } catch { }

# 4. Empaquetar ZIP ligero
Write-Host " [4/4] Creando archivo ZIP ultraligero..." -ForegroundColor Yellow
if (Test-Path $zipOut) { Remove-Item -Force $zipOut }
$zipV25 = Join-Path $dist "Parche_GymWeb_v2.5.zip"
if (Test-Path $zipV25) { Remove-Item -Force $zipV25 }
$zipV20 = Join-Path $dist "Parche_GymWeb_v2.0.zip"
if (Test-Path $zipV20) { Remove-Item -Force $zipV20 }
$zipV21 = Join-Path $dist "Parche_GymWeb_v2.1.zip"
if (Test-Path $zipV21) { Remove-Item -Force $zipV21 }

Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($patchOut, $zipV25, [System.IO.Compression.CompressionLevel]::Optimal, $false)
Copy-Item -Force $zipV25 $zipOut

$zipItem = Get-Item $zipV25
$sizeMb = [math]::Round($zipItem.Length / 1MB, 2)

Write-Host "`n============================================================" -ForegroundColor Green
Write-Host " ¡MICRO-PARCHE LIGERO GENERADO CON ÉXITO!" -ForegroundColor Green
Write-Host " Archivo listo para enviar por WhatsApp / Correo:" -ForegroundColor Green
Write-Host "  Ruta:   $zipOut" -ForegroundColor Cyan
Write-Host "  Tamaño: $sizeMb MB" -ForegroundColor Cyan
Write-Host "============================================================`n" -ForegroundColor Green
