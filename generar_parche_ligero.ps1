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

# 2. Compilar GymWeb y GymApp (Visor de escritorio)
Write-Host " [2/4] Compilando GymWeb y GymApp (Release)..." -ForegroundColor Yellow
$gymWebProj = Join-Path $root "GymWeb\GymWeb.csproj"
& dotnet publish $gymWebProj -c Release -r win-x64 --no-self-contained -o $tempBuild --nologo -v quiet

$gymAppProj = Join-Path $root "GymApp\GymApp.csproj"
$tempAppBuild = Join-Path $dist "temp_app_build"
if (Test-Path $tempAppBuild) { Remove-Item -Recurse -Force $tempAppBuild }
& dotnet publish $gymAppProj -c Release -r win-x64 --no-self-contained -o $tempAppBuild --nologo -v quiet

# 3. Copiar únicamente GymWeb.dll, Gym.exe, vistas compiladas y assets modificados
Write-Host " [3/4] Preparando archivos esenciales de actualización..." -ForegroundColor Yellow
Copy-Item -Force (Join-Path $tempBuild "GymWeb.dll") (Join-Path $patchFiles "GymWeb.dll")
Copy-Item -Force (Join-Path $tempBuild "GymWeb.pdb") (Join-Path $patchFiles "GymWeb.pdb")

if (Test-Path (Join-Path $tempAppBuild "Gym.exe")) {
    Copy-Item -Force (Join-Path $tempAppBuild "Gym.exe") (Join-Path $patchFiles "Gym.exe")
    Copy-Item -Force (Join-Path $tempAppBuild "Gym.dll") (Join-Path $patchFiles "Gym.dll")
    Copy-Item -Force (Join-Path $tempAppBuild "Gym.pdb") (Join-Path $patchFiles "Gym.pdb")
    
    # Actualizar también binarios locales en la raíz y en Publish
    Copy-Item -Force (Join-Path $tempAppBuild "Gym.exe") (Join-Path $root "Gym.exe") -ErrorAction SilentlyContinue
    Copy-Item -Force (Join-Path $tempAppBuild "Gym.dll") (Join-Path $root "Gym.dll") -ErrorAction SilentlyContinue
    Copy-Item -Force (Join-Path $tempAppBuild "Gym.exe") (Join-Path $root "Publish\Gym.exe") -ErrorAction SilentlyContinue
    Copy-Item -Force (Join-Path $tempAppBuild "Gym.dll") (Join-Path $root "Publish\Gym.dll") -ErrorAction SilentlyContinue
}

if (Test-Path (Join-Path $tempBuild "wwwroot")) {
    Copy-Item -Recurse -Force (Join-Path $tempBuild "wwwroot") (Join-Path $patchFiles "wwwroot")
    Remove-Item -Force -ErrorAction SilentlyContinue (Join-Path $patchFiles "wwwroot\img\logo-custom.png")
    Remove-Item -Force -ErrorAction SilentlyContinue (Join-Path $patchFiles "wwwroot\img\fondo-custom.jpg")
    Remove-Item -Recurse -Force -ErrorAction SilentlyContinue (Join-Path $patchFiles "wwwroot\img\socios")
    Remove-Item -Force -ErrorAction SilentlyContinue (Join-Path $patchFiles "wwwroot\img\productos\prod_*.*")
    Remove-Item -Recurse -Force -ErrorAction SilentlyContinue (Join-Path $patchFiles "wwwroot\backups")
}

# Copiar scripts de actualización automática
Copy-Item -Force (Join-Path $root "Publish\ACTUALIZAR.bat") (Join-Path $patchOut "ACTUALIZAR.bat")
Copy-Item -Force (Join-Path $root "Publish\actualizar.ps1") (Join-Path $patchOut "actualizar.ps1")

try { Remove-Item -Recurse -Force $tempBuild -ErrorAction SilentlyContinue } catch { }
try { Remove-Item -Recurse -Force $tempAppBuild -ErrorAction SilentlyContinue } catch { }

# 4. Empaquetar ZIP ligero
Write-Host " [4/4] Creando archivo ZIP ultraligero..." -ForegroundColor Yellow
if (Test-Path $zipOut) { Remove-Item -Force $zipOut }
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($patchOut, $zipOut, [System.IO.Compression.CompressionLevel]::Optimal, $false)

$zipItem = Get-Item $zipOut
$sizeMb = [math]::Round($zipItem.Length / 1MB, 2)

Write-Host "`n============================================================" -ForegroundColor Green
Write-Host " ¡MICRO-PARCHE LIGERO GENERADO CON ÉXITO!" -ForegroundColor Green
Write-Host " Archivo listo para enviar por WhatsApp / Correo:" -ForegroundColor Green
Write-Host "  Ruta:   $zipOut" -ForegroundColor Cyan
Write-Host "  Tamaño: $sizeMb MB" -ForegroundColor Cyan
Write-Host "============================================================`n" -ForegroundColor Green
