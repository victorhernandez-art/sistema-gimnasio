@echo off
setlocal
title Gimnasio
cd /d "%~dp0"

powershell -NoProfile -Command "try { $r = (Invoke-WebRequest -Uri 'http://localhost:5080' -UseBasicParsing -TimeoutSec 1).StatusCode; exit 0 } catch { exit 1 }" >nul 2>&1
if %errorlevel% equ 0 goto check_bio

echo Iniciando sistema del Gimnasio...

sc query GymWeb >nul 2>&1
if %errorlevel% equ 0 (
    net start GymWeb >nul 2>&1
    goto wait_server
)

if exist "GymWeb.exe" start "" /B GymWeb.exe & goto wait_server

:wait_server
for /L %%i in (1,1,10) do (
    powershell -NoProfile -Command "try { $r = (Invoke-WebRequest -Uri 'http://localhost:5080' -UseBasicParsing -TimeoutSec 1).StatusCode; exit 0 } catch { exit 1 }" >nul 2>&1
    if not errorlevel 1 goto check_bio
    timeout /t 1 /nobreak >nul
)

:check_bio
sc query BioService >nul 2>&1
if %errorlevel% equ 0 (
    net start BioService >nul 2>&1
    goto open_app
)
if exist "BioService\BioService.exe" start "" /B "BioService\BioService.exe"

:open_app
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
    start "" "%EDGE_PATH%" --app="http://localhost:5080" --window-size=1366,768
    exit
)
if defined CHROME_PATH (
    start "" "%CHROME_PATH%" --app="http://localhost:5080" --window-size=1366,768
    exit
)
start http://localhost:5080
exit
