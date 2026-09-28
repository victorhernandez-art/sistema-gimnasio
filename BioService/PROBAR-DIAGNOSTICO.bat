@echo off
title BioService — Diagnostico de Motor Biometrico
echo.
echo  ╔═══════════════════════════════════════════════╗
echo  ║   Ejecutando Diagnostico Biometrico...       ║
echo  ╚═══════════════════════════════════════════════╝
echo.

cd /d "%~dp0"
dotnet run -- --test
echo.
pause
