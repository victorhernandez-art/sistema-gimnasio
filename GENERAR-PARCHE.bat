@echo off
setlocal enabledelayedexpansion
title Generador de Parche de Actualizacion GymWeb

echo.
echo  ============================================================
echo   GymWeb - Generador de Parche Ligero de Actualizacion
echo  ============================================================
echo.

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0generar_parche.ps1"

if errorlevel 1 (
    echo.
    echo  ERROR al generar el parche de actualizacion.
    pause
    exit /b 1
)

explorer "%~dp0dist"
pause
