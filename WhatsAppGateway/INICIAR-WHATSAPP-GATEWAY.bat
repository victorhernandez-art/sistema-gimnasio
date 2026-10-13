@echo off
chcp 65001 >nul
title GymPro - WhatsApp Gateway (Puerto 5085)
cd /d "%~dp0"
cls
echo ===================================================================
echo     GYMPRO - WHATSAPP GATEWAY EN SEGUNDO PLANO
echo     Servicio de Envíos Masivos Automáticos sin Navegador
echo ===================================================================
echo.
echo Iniciando servicio en http://localhost:5085...
echo.

node server.js
if errorlevel 1 (
    echo.
    echo [ERROR] No se pudo iniciar el servicio de WhatsApp.
    pause
)
