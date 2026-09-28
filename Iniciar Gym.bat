@echo off
setlocal
title Gimnasio
cd /d "%~dp0"

:: Puerto para pruebas locales de desarrollo en esta carpeta
set "PORT=5250"

:: 1. Comprobar si el servidor ya esta respondiendo en el puerto 5250
powershell -NoProfile -Command "try { $r = (Invoke-WebRequest -Uri 'http://localhost:%PORT%' -UseBasicParsing -TimeoutSec 1).StatusCode; exit 0 } catch { exit 1 }" >nul 2>&1
if %errorlevel% equ 0 goto open_app

echo Iniciando sistema del Gimnasio (Nueva Version con Paleta y SQLite)...

:: Levantar el proyecto con dotnet run
start "" /B dotnet run --project GymWeb/GymWeb.csproj --no-build

:: Esperar a que el servidor este listo
for /L %%i in (1,1,12) do (
    powershell -NoProfile -Command "try { $r = (Invoke-WebRequest -Uri 'http://localhost:%PORT%' -UseBasicParsing -TimeoutSec 1).StatusCode; exit 0 } catch { exit 1 }" >nul 2>&1
    if not errorlevel 1 goto open_app
    timeout /t 1 /nobreak >nul
)

:open_app
:: Iniciar BioService si existe y no esta activo
sc query BioService >nul 2>&1
if %errorlevel% equ 0 (
    net start BioService >nul 2>&1
) else if exist "BioService\BioService.exe" (
    start "" /B "BioService\BioService.exe"
)

:: Abrir ventana de escritorio nativa con icono oficial
if exist "%~dp0Gym.exe" (
    start "" "%~dp0Gym.exe"
    exit
)

set "EDGE_PATH="
if exist "%ProgramFiles(x86)%\Microsoft\Edge\Application\msedge.exe" set "EDGE_PATH=%ProgramFiles(x86)%\Microsoft\Edge\Application\msedge.exe"
if exist "%ProgramFiles%\Microsoft\Edge\Application\msedge.exe" set "EDGE_PATH=%ProgramFiles%\Microsoft\Edge\Application\msedge.exe"
if exist "%LocalAppData%\Microsoft\Edge\Application\msedge.exe" set "EDGE_PATH=%LocalAppData%\Microsoft\Edge\Application\msedge.exe"

set "CHROME_PATH="
if exist "%ProgramFiles%\Google\Chrome\Application\chrome.exe" set "CHROME_PATH=%ProgramFiles%\Google\Chrome\Application\chrome.exe"
if exist "%ProgramFiles(x86)%\Google\Chrome\Application\chrome.exe" set "CHROME_PATH=%ProgramFiles(x86)%\Google\Chrome\Application\chrome.exe"

if defined EDGE_PATH (
    start "" "%EDGE_PATH%" --app="http://localhost:%PORT%" --window-size=1366,768
    exit
)
if defined CHROME_PATH (
    start "" "%CHROME_PATH%" --app="http://localhost:%PORT%" --window-size=1366,768
    exit
)
start http://localhost:%PORT%
exit
