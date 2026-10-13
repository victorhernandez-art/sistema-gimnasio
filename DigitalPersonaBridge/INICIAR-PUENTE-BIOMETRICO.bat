@echo off
chcp 65001 >nul
title DigitalPersona U.are.U 4500 - Puente WebSocket
cls
echo ===================================================================
echo     DIGITALPERSONA U.ARE.U 4500 - PUENTE LOCAL WEBSOCKET
echo     Servicio de Hardware Biometrico para GymPro / GymWeb
echo ===================================================================
echo.
echo Iniciando puente en ws://localhost:8080...
echo (Manten esta ventana abierta mientras captures huellas en GymPro)
echo.

if exist "DigitalPersonaBridge.exe" (
    DigitalPersonaBridge.exe
) else (
    dotnet run --project "%~dp0DigitalPersonaBridge.csproj"
)

echo.
pause
