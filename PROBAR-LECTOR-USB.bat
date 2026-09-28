@echo off
title Diagnostico de Lector de Huella USB
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0PROBAR-LECTOR-USB.ps1"
