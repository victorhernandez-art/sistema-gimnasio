@echo off
chcp 65001 > nul
title Reparar Fechas SQLite - GymWeb
echo ============================================================
echo   GymWeb - Herramienta de Reparacion de Fechas SQLite
echo ============================================================
echo.

set "TARGET_DIR=%~dp0"
set "DB_FILE="

:: 1. Deteccion automatica en rutas comunes
if exist "%TARGET_DIR%gym.db" set "DB_FILE=%TARGET_DIR%gym.db"
if not defined DB_FILE if exist "%TARGET_DIR%Publish\gym.db" set "DB_FILE=%TARGET_DIR%Publish\gym.db"
if not defined DB_FILE if exist "%TARGET_DIR%..\Publish\gym.db" set "DB_FILE=%TARGET_DIR%..\Publish\gym.db"
if not defined DB_FILE if exist "%TARGET_DIR%..\gym.db" set "DB_FILE=%TARGET_DIR%..\gym.db"
if not defined DB_FILE if exist "C:\GymWeb\gym.db" set "DB_FILE=C:\GymWeb\gym.db"
if not defined DB_FILE if exist "C:\Gym\gym.db" set "DB_FILE=C:\Gym\gym.db"
if not defined DB_FILE if exist "C:\xampp\htdocs\Gym\Publish\gym.db" set "DB_FILE=C:\xampp\htdocs\Gym\Publish\gym.db"

:: 2. Busqueda inteligente si no esta en las anteriores
if not defined DB_FILE (
    for /f "delims=" %%F in ('powershell -NoProfile -Command "Get-ChildItem -Path '%TARGET_DIR%' -Filter 'gym.db' -Recurse -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName -First 1"') do set "DB_FILE=%%F"
)

:: 3. Si aun no se detecta, solicitar al usuario
if not defined DB_FILE (
    echo [?] No se detecto gym.db automaticamente.
    echo     Arrastra el archivo gym.db a esta ventana o escribe su ruta:
    set /p "DB_FILE=> "
)

:: Limpiar comillas si el usuario arrastro el archivo
if defined DB_FILE set "DB_FILE=%DB_FILE:"=%"

if not exist "%DB_FILE%" (
    echo [ERROR] No se encontro el archivo: %DB_FILE%
    pause
    exit /b 1
)

echo [OK] Base de datos detectada: %DB_FILE%
echo Reparando fechas con hora en la tabla 'pago'...
echo.

powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$dbPath = '%DB_FILE%';" ^
  "$dbDir = Split-Path $dbPath -Parent;" ^
  "$sqliteDll = Get-ChildItem -Path '%~dp0', $dbDir, (Join-Path '%~dp0' 'Publish'), 'C:\GymWeb' -Filter 'Microsoft.Data.Sqlite.dll' -Recurse -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName -First 1;" ^
  "if ($sqliteDll) {" ^
  "  try { Unblock-File -Path $sqliteDll -ErrorAction SilentlyContinue } catch {};" ^
  "  try { $bytes = [System.IO.File]::ReadAllBytes($sqliteDll); [System.Reflection.Assembly]::Load($bytes) | Out-Null } catch { Add-Type -Path $sqliteDll };" ^
  "  $deps = Get-ChildItem -Path (Split-Path $sqliteDll -Parent) -Filter '*.dll' -ErrorAction SilentlyContinue;" ^
  "  foreach ($d in $deps) { try { Unblock-File -Path $d.FullName -ErrorAction SilentlyContinue } catch {} };" ^
  "} else { Add-Type -AssemblyName 'System.Data' };" ^
  "try {" ^
  "  $conn = New-Object Microsoft.Data.Sqlite.SqliteConnection(\"Data Source=$dbPath\");" ^
  "  $conn.Open();" ^
  "  $cmd = $conn.CreateCommand();" ^
  "  $cmd.CommandText = 'UPDATE pago SET fechaPago = substr(fechaPago, 1, 10) WHERE fechaPago IS NOT NULL AND length(fechaPago) > 10;';" ^
  "  $affected = $cmd.ExecuteNonQuery();" ^
  "  $conn.Close();" ^
  "  Write-Host \" [EXITO] Se normalizaron $affected pagos en la base de datos.\" -ForegroundColor Green;" ^
  "} catch {" ^
  "  Write-Host \" [AVISO] \" $_.Exception.Message -ForegroundColor Yellow;" ^
  "}"

echo.
echo ============================================================
echo   REPARACION COMPLETADA. Ya puedes abrir GymWeb.
echo ============================================================
pause
