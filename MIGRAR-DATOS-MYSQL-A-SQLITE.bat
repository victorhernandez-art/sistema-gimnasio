@echo off
setlocal enabledelayedexpansion
title GymWeb - Migrador de Datos MySQL a SQLite (Modo Autonomo)
color 0b

echo ============================================================
echo   GymWeb - Herramienta de Migracion a Modo Autonomo
echo   (Transferir datos de MySQL/XAMPP a SQLite local)
echo ============================================================
echo.
echo Esta herramienta copiara el 100%% de tus datos existentes:
echo   - Socios con fotografias
echo   - Huellas dactilares biometricas
echo   - Membresias, Pagos y Visitas
echo   - Inventario de Productos y Ventas
echo   - Configuracion general y Usuarios
echo.

cd /d "%~dp0"
set "TARGET_DIR="

:: 1. Verificar si esta en la carpeta actual
if exist "GymWeb.exe" (
    set "TARGET_DIR=%~dp0"
    goto found
)

:: 2. Si GymWeb esta en ejecucion en Windows, detectar su ruta exacta del proceso
for /f "usebackq delims=" %%P in (`powershell -NoProfile -Command "$p = Get-Process GymWeb -ErrorAction SilentlyContinue | Select-Object -First 1; if ($p) { Split-Path -Parent $p.Path }"`) do (
    if exist "%%P\GymWeb.exe" (
        set "TARGET_DIR=%%P"
        echo [INFO] Detectada instalacion activa de GymWeb en: %%P
        goto found
    )
)

:: 3. Verificar rutas comunes de instalacion
if exist "Publish\GymWeb.exe" (
    set "TARGET_DIR=%~dp0Publish"
    goto found
)
if exist "..\Publish\GymWeb.exe" (
    set "TARGET_DIR=%~dp0..\Publish"
    goto found
)
if exist "archivos\GymWeb.exe" (
    set "TARGET_DIR=%~dp0archivos"
    goto found
)
if exist "C:\GymWeb\GymWeb.exe" (
    set "TARGET_DIR=C:\GymWeb"
    goto found
)
if exist "%USERPROFILE%\Downloads\Gym\Publish\GymWeb.exe" (
    set "TARGET_DIR=%USERPROFILE%\Downloads\Gym\Publish"
    goto found
)
if exist "%USERPROFILE%\Downloads\Gym\GymWeb.exe" (
    set "TARGET_DIR=%USERPROFILE%\Downloads\Gym"
    goto found
)
if exist "%USERPROFILE%\Desktop\Gym\Publish\GymWeb.exe" (
    set "TARGET_DIR=%USERPROFILE%\Desktop\Gym\Publish"
    goto found
)
if exist "C:\Gym\Publish\GymWeb.exe" (
    set "TARGET_DIR=C:\Gym\Publish"
    goto found
)

:: 4. Buscar mediante PowerShell en Downloads y Desktop
for /f "usebackq delims=" %%F in (`powershell -NoProfile -Command "$f = Get-ChildItem -Path @($env:USERPROFILE + '\Downloads', $env:USERPROFILE + '\Desktop', 'C:\') -Filter 'GymWeb.exe' -Recurse -Depth 4 -ErrorAction SilentlyContinue | Select-Object -First 1; if ($f) { $f.DirectoryName }"`) do (
    if exist "%%F\GymWeb.exe" (
        set "TARGET_DIR=%%F"
        echo [INFO] Encontrado GymWeb en: %%F
        goto found
    )
)

:: 5. Si aun no se encuentra, solicitar la ruta al usuario (nunca cerrarse solo)
:ask_folder
color 0e
echo.
echo ============================================================
echo   [UBICACION DEL SISTEMA GYMWEB]
echo ============================================================
echo No se localizo automaticamente la carpeta del sistema.
echo Por favor, arrastra aqui la carpeta donde esta instalado GymWeb
echo o escribe la ruta donde esta GymWeb.exe y presiona ENTER:
echo.
set /p USER_INPUT="Ruta de la carpeta: "
set "USER_INPUT=!USER_INPUT:"=!"
if exist "!USER_INPUT!\GymWeb.exe" (
    set "TARGET_DIR=!USER_INPUT!"
    goto found
)
if exist "!USER_INPUT!" (
    if exist "!USER_INPUT!\Publish\GymWeb.exe" (
        set "TARGET_DIR=!USER_INPUT!\Publish"
        goto found
    )
)
echo.
echo [ERROR] No se encontro GymWeb.exe en: !USER_INPUT!
echo Intentalo nuevamente.
goto ask_folder

:found
echo.
echo ============================================================
echo Carpeta del sistema: !TARGET_DIR!
echo ============================================================
echo.
echo Presiona cualquier tecla para iniciar la migracion de MySQL a SQLite...
pause >nul
echo.
cd /d "!TARGET_DIR!"
echo Iniciando proceso de transferencia de datos...
GymWeb.exe --migrar-mysql --switch-config

if %errorlevel% neq 0 (
    color 0c
    echo.
    echo ============================================================
    echo   [ERROR DURANTE LA MIGRACION]
    echo   Codigo de salida: %errorlevel%
    echo   Asegurate de que XAMPP / MySQL este activo para transferir.
    echo ============================================================
) else (
    color 0a
    echo.
    echo ============================================================
    echo   MIGRACION FINALIZADA CON EXITO
    echo   Tus datos ya estan en SQLite (gym.db).
    echo   Ya puedes usar el sistema sin necesidad de activar XAMPP.
    echo ============================================================
)
echo.
echo Presiona cualquier tecla para cerrar esta ventana...
pause >nul
exit /b
