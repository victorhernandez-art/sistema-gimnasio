@echo off
chcp 65001 >nul
title Crear Acceso Directo de Gimnasio
cd /d "%~dp0"

powershell -NoProfile -ExecutionPolicy Bypass -Command "$desktop = [Environment]::GetFolderPath('Desktop'); $ws = New-Object -ComObject WScript.Shell; $s = $ws.CreateShortcut((Join-Path $desktop 'Gimnasio.lnk')); $gymExe = Join-Path (Get-Location).Path 'Gym.exe'; if (Test-Path $gymExe) { $s.TargetPath = $gymExe; $s.Arguments = ''; } else { $s.TargetPath = 'wscript.exe'; $target = Join-Path (Get-Location).Path 'AbrirGymWeb.vbs'; if (-not (Test-Path $target)) { $target = Join-Path (Get-Location).Path 'AbrirGym.vbs'; } $s.Arguments = [char]34 + $target + [char]34; } $s.WorkingDirectory = (Get-Location).Path; $ico = Join-Path (Get-Location).Path 'app.ico'; if (-not (Test-Path $ico)) { $ico = Join-Path (Get-Location).Path 'wwwroot\favicon.ico'; } if (-not (Test-Path $ico)) { $ico = Join-Path (Get-Location).Path 'GymWeb\wwwroot\favicon.ico'; } $s.IconLocation = $ico + ',0'; $s.Description = 'Sistema de Control y Gestion para Gimnasio'; $s.Save();"

echo.
echo ============================================================
echo   Acceso directo "Gimnasio" creado con exito en tu Escritorio.
echo ============================================================
echo.
pause
