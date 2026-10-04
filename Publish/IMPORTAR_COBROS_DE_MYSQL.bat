@echo off
setlocal
title Sincronizar Cobros de MySQL a SQLite - GymWeb
cd /d "%~dp0"

echo.
echo ============================================================
echo   SINCRONIZAR COBROS RECIENTES: MySQL (XAMPP) -^> SQLite
echo ============================================================
echo.
echo  Este proceso buscara los cobros y membresias que realizaste
echo  en MySQL (XAMPP) y los importara a SQLite de forma 100%% segura
echo  sin borrar ni modificar ningun registro existente en SQLite.
echo.
echo  IMPORTANTE: Asegurate de que MySQL este iniciado en tu XAMPP.
echo.
echo Presiona cualquier tecla para comenzar la sincronizacion...
pause >nul

echo.
echo Sincronizando datos...
echo.

if exist "GymWeb.exe" (
    GymWeb.exe --sincronizar-mysql
) else if exist "Publish\GymWeb.exe" (
    pushd Publish
    GymWeb.exe --sincronizar-mysql
    popd
) else (
    dotnet run --project GymWeb/GymWeb.csproj --no-launch-profile -- --sincronizar-mysql
)

echo.
echo ============================================================
echo  Proceso finalizado. Abre GymWeb para verificar tus cobros.
echo ============================================================
echo.
pause
