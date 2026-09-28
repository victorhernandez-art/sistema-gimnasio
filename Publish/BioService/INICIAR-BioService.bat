@echo off
title BioService — Servicio Biométrico Universal
echo.
echo  ╔═══════════════════════════════════════════════╗
echo  ║   Iniciando BioService...                     ║
echo  ║   Puerto: 4500                                ║
echo  ╚═══════════════════════════════════════════════╝
echo.

cd /d "%~dp0"
dotnet run
pause
