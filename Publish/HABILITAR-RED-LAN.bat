@echo off
chcp 65001 >nul
title Habilitar Acceso por Red LAN - GymPro

echo ======================================================================
echo           GYMPRO - CONFIGURADOR DE ACCESO POR RED LOCAL (LAN)
echo ======================================================================
echo.
echo Este asistente abrirá el puerto 5080 en el Firewall de Windows para
echo permitir que celulares, tablets u otras computadoras en tu misma red
echo Wi-Fi se conecten al sistema en tiempo real.
echo.

:: Verificar permisos de Administrador
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo [INFO] Solicitando permisos de Administrador...
    powershell -Command "Start-Process cmd -ArgumentList '/c \"\"%~f0\"\"' -Verb RunAs"
    exit /b
)

echo [1/2] Eliminando reglas anteriores si existían...
netsh advfirewall firewall delete rule name="GymPro Web Server (Puerto 5080)" >nul 2>&1

echo [2/2] Creando regla de entrada en el Firewall para el puerto 5080 TCP...
netsh advfirewall firewall add rule name="GymPro Web Server (Puerto 5080)" dir=in action=allow protocol=TCP localport=5080 profile=any >nul

if %errorlevel% equ 0 (
    echo.
    echo ======================================================================
    echo  [EXITO] ¡EL PUERTO 5080 HA SIDO AUTORIZADO CORRECTAMENTE!
    echo ======================================================================
    echo.
    echo Ahora tu celular y otros dispositivos podrán conectarse usando el
    echo código QR o la dirección IP mostrada en Configuración -^> Red LAN (Móvil).
    echo.
) else (
    echo.
    echo [ERROR] No se pudo configurar la regla del firewall.
    echo Por favor asegúrate de ejecutar este archivo como Administrador.
    echo.
)

echo Presiona cualquier tecla para salir...
pause >nul
