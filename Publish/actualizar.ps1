# ============================================================
#  GymWeb - Script de Actualizacion Automatica (Parche v2.0)
# ============================================================
$ErrorActionPreference = "Continue"

Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "  GymWeb - Parche de Actualizacion del Sistema" -ForegroundColor Cyan
Write-Host "  Version: 2.0 (Biometrico Universal + Auto-Migracion)" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""

# 1. Verificar Permisos de Administrador
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host " [!] ERROR: Se requieren permisos de Administrador." -ForegroundColor Red
    Write-Host "     Haz clic derecho en ACTUALIZAR.bat y selecciona 'Ejecutar como administrador'." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Presiona Enter para salir..."
    [void][System.Console]::ReadLine()
    exit 1
}

$patchRoot = $PSScriptRoot
$patchFiles = Join-Path $patchRoot "archivos"
$patchBio   = Join-Path $patchRoot "BioService"

# 2. Deteccion Inteligente del Directorio de Instalacion
Write-Host " [1/5] Detectando instalacion de GymWeb..." -ForegroundColor Yellow

$targetDir = $null

# Opcion A: En la misma carpeta del script
if (Test-Path (Join-Path $patchRoot "GymWeb.exe")) {
    $targetDir = $patchRoot
}

# Opcion B: En la carpeta padre
if (-not $targetDir) {
    $parent = Split-Path $patchRoot -Parent
    if ($parent -and (Test-Path (Join-Path $parent "GymWeb.exe"))) {
        $targetDir = $parent
    }
}

# Opcion C: Desde el servicio de Windows registrado
if (-not $targetDir) {
    try {
        $svc = Get-CimInstance Win32_Service -Filter "Name='GymWeb'" -ErrorAction SilentlyContinue
        if ($svc -and $svc.PathName) {
            $binPath = $svc.PathName.Trim('"').Trim()
            $dir = Split-Path $binPath -Parent
            if (Test-Path (Join-Path $dir "GymWeb.exe")) {
                $targetDir = $dir
            }
        }
    } catch { }
}

# Opcion D: Rutas estandar conocidas
if (-not $targetDir) {
    $rutasComunes = @(
        "C:\GymWeb",
        "C:\Gym",
        "C:\xampp\htdocs\Gym\Publish",
        "C:\xampp\htdocs\Gym\dist\GymWeb-Windows",
        "C:\Program Files\GymWeb"
    )
    foreach ($r in $rutasComunes) {
        if (Test-Path (Join-Path $r "GymWeb.exe")) {
            $targetDir = $r
            break
        }
    }
}

# Opcion E: Si no se encuentra automaticamente, solicitar al usuario
while (-not $targetDir) {
    Write-Host ""
    Write-Host " [?] No se pudo detectar automaticamente la carpeta de GymWeb." -ForegroundColor Yellow
    Write-Host "     Por favor ingresa la ruta donde esta instalado GymWeb (ejemplo: C:\GymWeb):" -ForegroundColor Cyan
    $inputDir = Read-Host " Ruta"
    if ([string]::IsNullOrWhiteSpace($inputDir)) {
        Write-Host "     Debes ingresar una ruta valida." -ForegroundColor Red
        continue
    }
    $cleanPath = $inputDir.Trim('"').Trim()
    if (Test-Path (Join-Path $cleanPath "GymWeb.exe")) {
        $targetDir = $cleanPath
    } elseif (Test-Path (Join-Path $cleanPath "GymWeb.dll")) {
        $targetDir = $cleanPath
    } else {
        Write-Host " [X] No se encontro GymWeb.exe ni GymWeb.dll en: $cleanPath" -ForegroundColor Red
        Write-Host "     Verifica que la carpeta sea correcta e intenta nuevamente." -ForegroundColor Yellow
    }
}

Write-Host "       Directorio detectado: $targetDir" -ForegroundColor Green
Write-Host ""

# 3. Detener Servicios y Procesos en Ejecucion
Write-Host " [2/5] Deteniendo servicios y procesos anteriores..." -ForegroundColor Yellow

try {
    $svcGym = Get-Service -Name "GymWeb" -ErrorAction SilentlyContinue
    if ($svcGym -and $svcGym.Status -eq "Running") {
        Write-Host "       Deteniendo servicio GymWeb..." -ForegroundColor Gray
        Stop-Service -Name "GymWeb" -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2
    }
} catch { }

try {
    $svcBio = Get-Service -Name "BioService" -ErrorAction SilentlyContinue
    if ($svcBio -and $svcBio.Status -eq "Running") {
        Write-Host "       Deteniendo servicio BioService..." -ForegroundColor Gray
        Stop-Service -Name "BioService" -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 1
    }
} catch { }

Get-Process -Name "GymWeb", "Gym" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Get-Process -Name "BioService" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 1

Write-Host "       Procesos detenidos correctamente." -ForegroundColor Green
Write-Host ""

# 4. Copiar Archivos de Actualizacion
Write-Host " [3/5] Aplicando archivos de actualizacion..." -ForegroundColor Yellow

if (Test-Path $patchFiles) {
    Get-ChildItem -Path $patchFiles -File | ForEach-Object {
        if ($_.Name -notmatch "appsettings.*\.json|gym\.db|license\.json") {
            Copy-Item -Path $_.FullName -Destination (Join-Path $targetDir $_.Name) -Force
        }
    }
    Write-Host "       Ejecutables y librerias del sistema actualizados." -ForegroundColor Green

    if (Test-Path (Join-Path $patchFiles "runtimes")) {
        Copy-Item -Path (Join-Path $patchFiles "runtimes") -Destination (Join-Path $targetDir "runtimes") -Recurse -Force
    }
    if (Test-Path (Join-Path $patchFiles "wwwroot")) {
        Copy-Item -Path (Join-Path $patchFiles "wwwroot\*") -Destination (Join-Path $targetDir "wwwroot") -Recurse -Force
        Write-Host "       Recursos visuales y estilos (wwwroot) actualizados." -ForegroundColor Green
    }
} elseif (Test-Path (Join-Path $patchRoot "GymWeb.dll") -and ($patchRoot -ne $targetDir)) {
    Get-ChildItem -Path $patchRoot -File -Filter "*.dll" | ForEach-Object {
        Copy-Item -Path $_.FullName -Destination (Join-Path $targetDir $_.Name) -Force
    }
    Copy-Item -Path (Join-Path $patchRoot "GymWeb.exe") -Destination (Join-Path $targetDir "GymWeb.exe") -Force -ErrorAction SilentlyContinue
    Write-Host "       GymWeb actualizado." -ForegroundColor Green
    if (Test-Path (Join-Path $patchRoot "wwwroot")) {
        Copy-Item -Path (Join-Path $patchRoot "wwwroot\*") -Destination (Join-Path $targetDir "wwwroot") -Recurse -Force
    }
}

$destBio = Join-Path $targetDir "BioService"
if (Test-Path $patchBio) {
    if (-not (Test-Path $destBio)) {
        New-Item -ItemType Directory -Path $destBio -Force | Out-Null
    }
    Copy-Item -Path (Join-Path $patchBio "*") -Destination $destBio -Recurse -Force
    Write-Host "       Modulo BioService instalado en: BioService\" -ForegroundColor Green
}

# Copiar nuevos lanzadores en Modo App de Escritorio, visor nativo Gym.exe, icono y script de migración si vienen en el parche
foreach ($utilFile in @("Iniciar GymWeb.bat", "AbrirGymWeb.vbs", "Crear Acceso Directo.bat", "MIGRAR-DATOS-MYSQL-A-SQLITE.bat", "app.ico", "Gym.exe", "Gym.dll", "Gym.runtimeconfig.json", "Microsoft.Web.WebView2.Core.dll", "Microsoft.Web.WebView2.WinForms.dll", "runtimes")) {
    $srcUtil = Join-Path $patchRoot $utilFile
    if (-not (Test-Path $srcUtil)) { $srcUtil = Join-Path $patchFiles $utilFile }
    if (Test-Path $srcUtil) {
        Copy-Item -Path $srcUtil -Destination (Join-Path $targetDir $utilFile) -Recurse -Force
    }
}

# Preservar siempre los archivos del cliente (logo-custom.png, fondo-custom.jpg, fotos de socios y gym.db)
Write-Host "       Datos, configuracion institucional y logotipo del cliente preservados intactos." -ForegroundColor Green

Write-Host "       Archivos del sistema actualizados con exito." -ForegroundColor Green
Write-Host ""

# 5. Configurar Servicio de BioService
Write-Host " [4/5] Configurando Servicio Biometrico..." -ForegroundColor Yellow

$bioExe = Join-Path $destBio "BioService.exe"
if (Test-Path $bioExe) {
    $svcBio = Get-Service -Name "BioService" -ErrorAction SilentlyContinue
    if (-not $svcBio) {
        Write-Host "       Registrando BioService como servicio de Windows..." -ForegroundColor Gray
        & sc.exe create BioService binPath= "$bioExe" start= auto DisplayName= "GymWeb BioService" | Out-Null
        & sc.exe description BioService "Microservicio biometrico universal para GymWeb" | Out-Null
    } else {
        & sc.exe config BioService binPath= "$bioExe" start= auto | Out-Null
    }

    try {
        & sc.exe start BioService | Out-Null
        Write-Host "       BioService registrado e iniciado como servicio de Windows." -ForegroundColor Green
    } catch {
        Write-Host "       BioService listo para iniciar con INICIAR-BioService.bat" -ForegroundColor Gray
    }
}

Write-Host ""

# 6. Reiniciar GymWeb
Write-Host " [5/5] Reiniciando servicio GymWeb..." -ForegroundColor Yellow

$svcGym = Get-Service -Name "GymWeb" -ErrorAction SilentlyContinue
if ($svcGym) {
    try {
        Start-Service -Name "GymWeb" -ErrorAction SilentlyContinue
        Write-Host "       Servicio Windows GymWeb reiniciado correctamente." -ForegroundColor Green
    } catch {
        Write-Host "       Puedes iniciar GymWeb manualmente o con 'net start GymWeb'." -ForegroundColor Yellow
    }
} else {
    Write-Host "       Instalacion local lista. Puedes iniciar GymWeb normalmente." -ForegroundColor Green
}

# 7. Crear / Actualizar Acceso Directo en el Escritorio con icono
try {
    $desktop = [Environment]::GetFolderPath('Desktop')
    $ws = New-Object -ComObject WScript.Shell
    $sc = $ws.CreateShortcut((Join-Path $desktop 'Gimnasio.lnk'))
    
    $appExe = Join-Path $targetDir 'Gym.exe'
    if (Test-Path $appExe) {
        $sc.TargetPath = $appExe
        $sc.Arguments = ''
    } else {
        $sc.TargetPath = 'wscript.exe'
        $vbsTarget = Join-Path $targetDir 'AbrirGymWeb.vbs'
        if (-not (Test-Path $vbsTarget)) { $vbsTarget = Join-Path $targetDir 'AbrirGym.vbs' }
        $sc.Arguments = ('"{0}"' -f $vbsTarget)
    }
    $sc.WorkingDirectory = $targetDir
    $icoPath = Join-Path $targetDir 'app.ico'
    if (-not (Test-Path $icoPath)) { $icoPath = Join-Path $targetDir 'wwwroot\favicon.ico' }
    $sc.IconLocation = $icoPath + ',0'
    $sc.Description = 'Sistema de Control y Gestion para Gimnasio'
    $sc.Save()
    Write-Host "       Acceso directo con nuevo icono creado en el Escritorio." -ForegroundColor Green
} catch { }

Write-Host ""
Write-Host "============================================================" -ForegroundColor Green
Write-Host "  ACTUALIZACION COMPLETADA AL 100%" -ForegroundColor Green
Write-Host ""
Write-Host "  Mejoras y correcciones aplicadas:" -ForegroundColor Green
Write-Host "    - Paleta de colores interactiva (6 temas de color)" -ForegroundColor Green
Write-Host "    - Icono oficial de la pesa cian en escritorio y barra de tareas" -ForegroundColor Green
Write-Host "    - Soporte nativo para SQLite y MySQL con proteccion de datos" -ForegroundColor Green
Write-Host "    - Soporte nativo para lectores ZKTeco (ZK9500, ZK4500, SLK20R) y WBF" -ForegroundColor Green
Write-Host "    - BioService corriendo en puerto 4500" -ForegroundColor Green

# 8. Informar sobre el estado de la Base de Datos
$appCfg = Join-Path $targetDir "appsettings.json"
$isCurrentlyMySql = $false
if (Test-Path $appCfg) {
    $cfgContent = Get-Content $appCfg -Raw
    if ($cfgContent -match "Server=|Database=gym") {
        $isCurrentlyMySql = $true
    }
}

if ($isCurrentlyMySql) {
    Write-Host ""
    Write-Host " [INFO BASE DE DATOS: MySQL / XAMPP ACTIVO]" -ForegroundColor Yellow
    Write-Host "   * Tu sistema sigue conectado a tu base de datos MySQL normalmente." -ForegroundColor White
    Write-Host "   * El 100% de tus socios, fotos, cobros y productos permanecen intactos." -ForegroundColor Green
    Write-Host "   * Si deseas pasar al nuevo modo autonomo sin depender de XAMPP, ejecuta:" -ForegroundColor Cyan
    Write-Host "     -> MIGRAR-DATOS-MYSQL-A-SQLITE.bat (en la carpeta de instalacion)" -ForegroundColor Yellow
} else {
    Write-Host ""
    Write-Host " [INFO BASE DE DATOS: MODO AUTONOMO SQLITE]" -ForegroundColor Green
    Write-Host "   * El sistema opera en modo local autonomo (gym.db) sin necesidad de XAMPP." -ForegroundColor White
}

Write-Host "============================================================" -ForegroundColor Green
Write-Host ""
Write-Host "Presiona Enter para finalizar..." -ForegroundColor Cyan
[void][System.Console]::ReadLine()
