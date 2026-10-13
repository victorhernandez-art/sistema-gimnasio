@echo off
chcp 65001 >nul
title DigitalPersona U.are.U 4500 - Puente WebSocket
cd /d "%~dp0"
cls
echo ===================================================================
echo     DIGITALPERSONA U.ARE.U 4500 - PUENTE LOCAL WEBSOCKET
echo     Servicio de Hardware Biométrico para GymPro / GymWeb
echo ===================================================================
echo.
echo Iniciando puente en ws://localhost:8080...
echo (Mantén esta ventana abierta mientras captures huellas en GymPro)
echo.

if exist "PuenteBiometrico\DigitalPersonaBridge.exe" (
    cd PuenteBiometrico
    DigitalPersonaBridge.exe
) else if exist "DigitalPersonaBridge\publish\DigitalPersonaBridge.exe" (
    cd DigitalPersonaBridge\publish
    DigitalPersonaBridge.exe
) else if exist "DigitalPersonaBridge.exe" (
    DigitalPersonaBridge.exe
) else (
    echo [ERROR] No se encontro DigitalPersonaBridge.exe
    pause
)
