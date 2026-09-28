@echo off
setlocal enabledelayedexpansion
title Publicando GymWeb para Windows y Mac...

echo.
echo  ============================================================
echo   GymWeb - Generador de Paquetes de Distribucion
echo  ============================================================
echo.

:: ---- Verificar que dotnet esta instalado ----
where dotnet >nul 2>&1
if errorlevel 1 (
    echo  ERROR: .NET SDK no esta instalado.
    echo  Descargalo en: https://dotnet.microsoft.com/download
    pause
    exit /b 1
)

set "PROYECTO=%~dp0GymWeb\GymWeb.csproj"
set "BIO_PROYECTO=%~dp0BioService\BioService.csproj"
set "DIST=%~dp0dist"
set "WIN_OUT=%DIST%\GymWeb-Windows"
set "BIO_WIN_OUT=%WIN_OUT%\BioService"
set "MAC_ARM_OUT=%DIST%\GymWeb-Mac-AppleSilicon"
set "MAC_X64_OUT=%DIST%\GymWeb-Mac-Intel"
set "PUB=%~dp0Publish"

:: ---- Limpiar salidas anteriores ----
echo  Limpiando salidas anteriores...
if exist "%WIN_OUT%"     rd /s /q "%WIN_OUT%"
if exist "%MAC_ARM_OUT%" rd /s /q "%MAC_ARM_OUT%"
if exist "%MAC_X64_OUT%" rd /s /q "%MAC_X64_OUT%"
mkdir "%DIST%" >nul 2>&1
echo         Listo.
echo.

:: ============================================================
:: WINDOWS
:: ============================================================
echo  [1/3] Compilando GymWeb para Windows (x64)...
dotnet publish "%PROYECTO%" ^
    -r win-x64 ^
    -c Release ^
    --self-contained true ^
    -o "%WIN_OUT%" ^
    /p:PublishSingleFile=false ^
    --nologo -v quiet

if errorlevel 1 (
    echo  ERROR al compilar GymWeb para Windows.
    pause
    exit /b 1
)

echo  Compilando BioService para Windows (x64)...
dotnet publish "%BIO_PROYECTO%" ^
    -r win-x64 ^
    -c Release ^
    --self-contained true ^
    -o "%BIO_WIN_OUT%" ^
    /p:PublishSingleFile=false ^
    --nologo -v quiet

if errorlevel 1 (
    echo  ERROR al compilar BioService para Windows.
    pause
    exit /b 1
)

:: Copiar scripts de instalacion
copy /y "%PUB%\INSTALAR.bat"               "%WIN_OUT%\INSTALAR.bat"         >nul
copy /y "%PUB%\2_Desinstalar Servicio.bat" "%WIN_OUT%\DESINSTALAR.bat"      >nul
copy /y "%PUB%\Iniciar GymWeb.bat"         "%WIN_OUT%\Iniciar GymWeb.bat"   >nul
copy /y "%~dp0BioService\INICIAR-BioService.bat" "%BIO_WIN_OUT%\INICIAR-BioService.bat" >nul
copy /y "%~dp0BioService\PROBAR-DIAGNOSTICO.bat" "%BIO_WIN_OUT%\PROBAR-DIAGNOSTICO.bat" >nul

:: Copiar base de datos de referencia
xcopy /s /i /q "%~dp0BD" "%WIN_OUT%\BD" >nul

echo         Windows listo en: dist\GymWeb-Windows\ (incluye BioService)
echo.

:: ============================================================
:: MAC - Apple Silicon (M1/M2/M3/M4)
:: ============================================================
echo  [2/3] Compilando para Mac Apple Silicon (osx-arm64)...
dotnet publish "%PROYECTO%" ^
    -r osx-arm64 ^
    -c Release ^
    --self-contained true ^
    -o "%MAC_ARM_OUT%" ^
    /p:PublishSingleFile=false ^
    --nologo -v quiet

if errorlevel 1 (
    echo  ERROR al compilar para Mac ARM64.
    pause
    exit /b 1
)

:: Copiar script de instalacion Mac
copy /y "%PUB%\INSTALAR.command" "%MAC_ARM_OUT%\INSTALAR.command" >nul

:: Copiar base de datos de referencia
xcopy /s /i /q "%~dp0BD" "%MAC_ARM_OUT%\BD" >nul

echo         Mac Apple Silicon listo en: dist\GymWeb-Mac-AppleSilicon\
echo.

:: ============================================================
:: MAC - Intel
:: ============================================================
echo  [3/3] Compilando para Mac Intel (osx-x64)...
dotnet publish "%PROYECTO%" ^
    -r osx-x64 ^
    -c Release ^
    --self-contained true ^
    -o "%MAC_X64_OUT%" ^
    /p:PublishSingleFile=false ^
    --nologo -v quiet

if errorlevel 1 (
    echo  ERROR al compilar para Mac Intel.
    pause
    exit /b 1
)

:: Copiar script de instalacion Mac
copy /y "%PUB%\INSTALAR.command" "%MAC_X64_OUT%\INSTALAR.command" >nul

:: Copiar base de datos de referencia
xcopy /s /i /q "%~dp0BD" "%MAC_X64_OUT%\BD" >nul

echo         Mac Intel listo en: dist\GymWeb-Mac-Intel\
echo.

:: ============================================================
:: RESUMEN FINAL
:: ============================================================
echo  ============================================================
echo   TODO LISTO - Carpetas de distribucion:
echo  ============================================================
echo.
echo   dist\GymWeb-Windows\
echo     ^> Copiar al PC con Windows
echo     ^> Clic derecho en INSTALAR.bat
echo     ^> Ejecutar como administrador
echo.
echo   dist\GymWeb-Mac-AppleSilicon\
echo     ^> Copiar al Mac con chip M1/M2/M3/M4
echo     ^> Doble clic en INSTALAR.command
echo.
echo   dist\GymWeb-Mac-Intel\
echo     ^> Copiar al Mac con procesador Intel
echo     ^> Doble clic en INSTALAR.command
echo.
echo   Cada carpeta incluye:
echo     - GymWeb ejecutable + todas las librerias
echo     - appsettings.json con puerto 5080 configurado
echo     - INSTALAR (detecta puerto libre automaticamente)
echo     - BD\ con los scripts SQL de la base de datos
echo  ============================================================
echo.

explorer "%DIST%"
pause
endlocal
