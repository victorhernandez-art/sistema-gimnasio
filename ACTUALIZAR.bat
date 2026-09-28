@echo off
setlocal
title Actualizador de GymWeb - Parche v2.0
cd /d "%~dp0"

REM 1. Verificar Permisos de Administrador y solicitar elevacion automatica (UAC)
net session >nul 2>&1
if errorlevel 1 (
    echo.
    echo  ============================================================
    echo   Solicitando permisos de Administrador...
    echo  ============================================================
    echo.
    powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)

REM 2. Ejecutar script PowerShell con bypass de politicas
if exist "%~dp0actualizar.ps1" (
    powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0actualizar.ps1"
) else (
    echo.
    echo  ERROR: No se encontro el archivo actualizar.ps1 en esta carpeta.
    echo  Asegurate de haber extraido todos los archivos del ZIP del parche.
    echo.
    pause
    exit /b 1
)

if errorlevel 1 (
    echo.
    echo  ============================================================
    echo   El proceso de actualizacion finalizo con advertencias.
    echo  ============================================================
    echo.
    pause
)
