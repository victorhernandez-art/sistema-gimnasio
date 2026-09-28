# ==============================================================================
#  GymWeb v2.1 - Generador del Paquete Instalador Completo para Windows
# ==============================================================================
$ErrorActionPreference = "Stop"

$root = $PSScriptRoot
$dist = Join-Path $root "dist"
$winOut = Join-Path $dist "GymWeb-Windows"
$bioWinOut = Join-Path $winOut "BioService"
$zipOut = Join-Path $dist "Instalador_GymWeb_Windows_v2.1.zip"
$tempBuild = Join-Path $dist "temp_installer_build"

Write-Host "`n============================================================" -ForegroundColor Cyan
Write-Host "   GENERANDO INSTALADOR COMPLETO GYMWEB v2.1 (WINDOWS)" -ForegroundColor Cyan
Write-Host "============================================================`n" -ForegroundColor Cyan

# 1. Limpieza previa
Write-Host " [1/6] Limpiando salidas anteriores..." -ForegroundColor Yellow
if (Test-Path $winOut) { Remove-Item -Recurse -Force $winOut }
if (Test-Path $tempBuild) { Remove-Item -Recurse -Force $tempBuild }
New-Item -ItemType Directory -Force -Path $winOut | Out-Null
New-Item -ItemType Directory -Force -Path $bioWinOut | Out-Null

# 2. Compilar GymWeb autocontenido x64
Write-Host " [2/6] Compilando GymWeb (win-x64, Self-Contained, Release)..." -ForegroundColor Yellow
$gymWebProj = Join-Path $root "GymWeb\GymWeb.csproj"
& dotnet publish $gymWebProj -r win-x64 -c Release --self-contained true -o $winOut /p:PublishSingleFile=false --nologo -v quiet

# 3. Compilar BioService autocontenido x64
Write-Host " [3/6] Compilando BioService para lectores biometricos..." -ForegroundColor Yellow
$bioProj = Join-Path $root "BioService\BioService.csproj"
& dotnet publish $bioProj -r win-x64 -c Release --self-contained true -o $bioWinOut /p:PublishSingleFile=false --nologo -v quiet
Copy-Item -Force (Join-Path $root "BioService\INICIAR-BioService.bat") (Join-Path $bioWinOut "INICIAR-BioService.bat")
Copy-Item -Force (Join-Path $root "BioService\PROBAR-DIAGNOSTICO.bat") (Join-Path $bioWinOut "PROBAR-DIAGNOSTICO.bat")

# 4. Compilar visor nativo de escritorio GymApp (Gym.exe, WebView2, Desktop Runtime x64)
Write-Host " [4/6] Compilando visor nativo de escritorio Gym.exe (Self-Contained x64)..." -ForegroundColor Yellow
$gymAppProj = Join-Path $root "GymApp\GymApp.csproj"
& dotnet publish $gymAppProj -r win-x64 -c Release --self-contained true -o $winOut /p:PublishSingleFile=false --nologo -v quiet

# Sincronizar binarios autocontenidos en GymApp_bin y la raiz
$gymAppBin = Join-Path $root "GymApp_bin"
if (-not (Test-Path $gymAppBin)) { New-Item -ItemType Directory -Force -Path $gymAppBin | Out-Null }
Copy-Item -Force (Join-Path $winOut "Gym.exe") (Join-Path $gymAppBin "Gym.exe")
Copy-Item -Force (Join-Path $winOut "Gym.dll") (Join-Path $gymAppBin "Gym.dll")
Copy-Item -Force (Join-Path $winOut "Gym.runtimeconfig.json") (Join-Path $gymAppBin "Gym.runtimeconfig.json")
Copy-Item -Force (Join-Path $winOut "Gym.deps.json") (Join-Path $gymAppBin "Gym.deps.json")

Copy-Item -Force (Join-Path $winOut "Gym.exe") (Join-Path $root "Gym.exe")
Copy-Item -Force (Join-Path $winOut "Gym.dll") (Join-Path $root "Gym.dll")
Copy-Item -Force (Join-Path $winOut "Gym.runtimeconfig.json") (Join-Path $root "Gym.runtimeconfig.json")
Copy-Item -Force (Join-Path $winOut "Gym.deps.json") (Join-Path $root "Gym.deps.json")

# 5. Copiar scripts de instalacion, administracion y esquemas
Write-Host " [5/6] Copiando scripts, BD y herramientas de configuracion..." -ForegroundColor Yellow
Copy-Item -Force (Join-Path $root "Publish\INSTALAR.bat") (Join-Path $winOut "INSTALAR.bat")

$desinstalarContent = @"
@echo off
net session >nul 2>&1
if errorlevel 1 (
    powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)
echo ============================================================
echo   Desinstalando Servicios de GymWeb y BioService...
echo ============================================================
sc stop GymWeb >nul 2>&1
sc stop BioService >nul 2>&1
timeout /t 2 /nobreak >nul
sc delete GymWeb >nul 2>&1
sc delete BioService >nul 2>&1
echo Servicios removidos exitosamente de Windows.
pause
"@
Set-Content -Path (Join-Path $winOut "DESINSTALAR.bat") -Value $desinstalarContent -Encoding ASCII

Copy-Item -Force (Join-Path $root "Publish\Iniciar GymWeb.bat") (Join-Path $winOut "Iniciar GymWeb.bat")
Copy-Item -Force (Join-Path $root "Publish\AbrirGymWeb.vbs") (Join-Path $winOut "AbrirGymWeb.vbs")
Copy-Item -Force (Join-Path $root "Publish\Crear Acceso Directo.bat") (Join-Path $winOut "Crear Acceso Directo.bat")
Copy-Item -Force (Join-Path $root "app.ico") (Join-Path $winOut "app.ico")

# Limpieza estricta de archivos de pruebas previas en el instalador
Write-Host "       Asegurando instalacion limpia (sin fotos de prueba ni respaldos temporales)..." -ForegroundColor Gray
Remove-Item -Force -ErrorAction SilentlyContinue (Join-Path $winOut "wwwroot\img\logo-custom.png")
Remove-Item -Force -ErrorAction SilentlyContinue (Join-Path $winOut "wwwroot\img\fondo-custom.jpg")
Remove-Item -Recurse -Force -ErrorAction SilentlyContinue (Join-Path $winOut "wwwroot\img\socios")
Remove-Item -Recurse -Force -ErrorAction SilentlyContinue (Join-Path $winOut "wwwroot\backups")
Remove-Item -Force -ErrorAction SilentlyContinue (Join-Path $winOut "license.json")

# Crear carpetas vacias necesarias
New-Item -ItemType Directory -Force -Path (Join-Path $winOut "wwwroot\img\socios") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $winOut "wwwroot\backups") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $winOut "logs") | Out-Null

# 6. Empaquetar en ZIP final
Write-Host " [6/6] Creando archivo ZIP final: Instalador_GymWeb_Windows_v2.1.zip..." -ForegroundColor Yellow
if (Test-Path $zipOut) { Remove-Item -Force $zipOut }
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($winOut, $zipOut, [System.IO.Compression.CompressionLevel]::Fastest, $false)

$zipItem = Get-Item $zipOut
$sizeMb = [math]::Round($zipItem.Length / 1MB, 2)

Write-Host "`n============================================================" -ForegroundColor Green
Write-Host " ¡INSTALADOR COMPLETO GENERADO CON EXITO!" -ForegroundColor Green
Write-Host " Archivo listo para enviar al cliente:" -ForegroundColor Green
Write-Host "  Ruta:   $zipOut" -ForegroundColor Cyan
Write-Host "  Tamano: $sizeMb MB" -ForegroundColor Cyan
Write-Host "============================================================`n" -ForegroundColor Green
