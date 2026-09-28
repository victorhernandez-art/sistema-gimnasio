@echo off
setlocal
title Generador de Micro-Parche Ligero GymWeb (~2 MB)
cd /d "%~dp0"

echo.
echo ============================================================
echo   GymWeb - Generador de Micro-Parche Ligero (~2 MB)
echo   (Para actualizaciones de código C#, vistas Razor y reportes)
echo ============================================================
echo.

powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0generar_parche_ligero.ps1"

if errorlevel 1 (
    echo.
    echo  ERROR al generar el micro-parche.
    pause
    exit /b 1
)

pause
