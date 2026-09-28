@echo off
setlocal enabledelayedexpansion
title Instalador GymWeb

echo.
echo  ============================================================
echo   GymWeb - Sistema de Administracion de Gimnasio
echo   INSTALADOR
echo  ============================================================
echo.

:: ---- Verificar permisos de administrador y auto-elevar (UAC) ----
net session >nul 2>&1
if errorlevel 1 (
    echo.
    echo  ============================================================
    echo   Solicitando permisos de Administrador...
    echo  ============================================================
    echo.
    powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)

:: ---- Verificar que GymWeb.exe existe ----
if not exist "%~dp0GymWeb.exe" (
    echo  ERROR: No se encontro GymWeb.exe en esta carpeta.
    echo  Asegurate de ejecutar este archivo desde la carpeta de instalacion.
    echo.
    pause
    exit /b 1
)

set PUERTO=5080
set EXE="%~dp0GymWeb.exe"

echo  [1/5] Limpiando instalacion anterior...
sc stop GymWeb >nul 2>&1
sc stop BioService >nul 2>&1
timeout /t 2 /nobreak >nul
sc delete GymWeb >nul 2>&1
sc delete BioService >nul 2>&1
timeout /t 2 /nobreak >nul
echo         Listo.
echo.

echo  [2/5] Verificando disponibilidad del puerto !PUERTO!...
netstat -ano | findstr /R "[: ]!PUERTO! " | findstr "LISTENING" >nul 2>&1
if not errorlevel 1 (
    echo         Puerto !PUERTO! ocupado. Buscando puerto libre...
    for %%N in (5081 5082 5083 5084 5085 5086 5087 5088 5089 5090) do (
        if "!PUERTO!"=="5080" (
            netstat -ano | findstr /R "[: ]%%N " | findstr "LISTENING" >nul 2>&1
            if errorlevel 1 (
                set PUERTO=%%N
            )
        )
    )
    echo         Usando puerto: !PUERTO!
    powershell -NoProfile -Command "(Get-Content '%~dp0appsettings.json' -Raw) -replace 'localhost:\d+', 'localhost:!PUERTO!' | Set-Content '%~dp0appsettings.json'"
    if exist "%~dp0appsettings.Production.json" (
        powershell -NoProfile -Command "(Get-Content '%~dp0appsettings.Production.json' -Raw) -replace 'localhost:\d+', 'localhost:!PUERTO!' | Set-Content '%~dp0appsettings.Production.json'"
    )
) else (
    echo         Puerto !PUERTO! disponible.
)
echo.

echo  [3/6] Verificando base de datos...
powershell -NoProfile -Command "$cfg = Get-Content '%~dp0appsettings.json' -Raw; if ($cfg -match 'gym\.db') { exit 0 } else { exit 1 }" >nul 2>&1
if not errorlevel 1 (
    echo         Base de datos autonoma SQLite habilitada (gym.db).
    echo         Listo. El sistema es 100%% autonomo y no requiere XAMPP ni MySQL externo.
    goto :SALTAR_BD
)

echo         Buscando servidor MySQL local...
:: Buscar mysql.exe de XAMPP o MySQL standalone
set MYSQL=
if exist "C:\xampp\mysql\bin\mysql.exe"       set MYSQL=C:\xampp\mysql\bin\mysql.exe
if exist "C:\xampp8\mysql\bin\mysql.exe"      set MYSQL=C:\xampp8\mysql\bin\mysql.exe
if exist "C:\Program Files\MySQL\MySQL Server 8.0\bin\mysql.exe" set MYSQL=C:\Program Files\MySQL\MySQL Server 8.0\bin\mysql.exe
if exist "C:\Program Files\MySQL\MySQL Server 8.4\bin\mysql.exe" set MYSQL=C:\Program Files\MySQL\MySQL Server 8.4\bin\mysql.exe
if exist "C:\Program Files\MariaDB 10.4\bin\mysql.exe"           set MYSQL=C:\Program Files\MariaDB 10.4\bin\mysql.exe
if exist "C:\Program Files\MariaDB 11.0\bin\mysql.exe"           set MYSQL=C:\Program Files\MariaDB 11.0\bin\mysql.exe

:: Si no se encontro, buscar en PATH
if not defined MYSQL (
    where mysql >nul 2>&1
    if not errorlevel 1 set MYSQL=mysql
)

if not defined MYSQL (
    echo.
    echo  NOTA: No se detecto MySQL. La app usara almacenamiento SQLite local autonomo.
    goto :SALTAR_BD
)

:: Verificar que MySQL esta corriendo
"%MYSQL%" -uroot -e "SELECT 1;" >nul 2>&1
if errorlevel 1 (
    echo.
    echo  NOTA: MySQL no esta corriendo. La app usara almacenamiento local.
    goto :SALTAR_BD
)

:: Crear la base de datos e importar el esquema
echo         MySQL encontrado. Creando base de datos 'gym'...
"%MYSQL%" -uroot -e "CREATE DATABASE IF NOT EXISTS gym CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;" >nul 2>&1
if errorlevel 1 (
    echo         Error creando la base de datos MySQL.
    goto :SALTAR_BD
)

:: Importar tablas solo si no existen aun
"%MYSQL%" -uroot gym -e "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='gym';" 2>nul | findstr "^0" >nul
if not errorlevel 1 (
    echo         Importando tablas...
    "%MYSQL%" -uroot gym < "%~dp0BD\gym.sql" >nul 2>&1
    echo         Base de datos lista.
) else (
    echo         La base de datos ya tenia tablas, no se sobreescribio.
)

:SALTAR_BD
echo.

echo  [4/6] Instalando servicios de Windows y permisos de red...
netsh advfirewall firewall add rule name="GymWeb" dir=in action=allow program="%~dp0GymWeb.exe" enable=yes >nul 2>&1
sc create GymWeb binPath= "\"%~dp0GymWeb.exe\"" start= auto DisplayName= "GymWeb Sistema Gimnasio" >nul
if errorlevel 1 (
    echo  ERROR: No se pudo crear el servicio GymWeb.
    pause
    exit /b 1
)
sc description GymWeb "Sistema de administracion de gimnasio" >nul
echo         Servicio GymWeb creado.

if exist "%~dp0BioService\BioService.exe" (
    netsh advfirewall firewall add rule name="BioService" dir=in action=allow program="%~dp0BioService\BioService.exe" enable=yes >nul 2>&1
    sc create BioService binPath= "\"%~dp0BioService\BioService.exe\"" start= auto DisplayName= "GymWeb BioService" >nul 2>&1
    sc description BioService "Microservicio biometrico universal para GymWeb" >nul 2>&1
    sc start BioService >nul 2>&1
    echo         Servicio BioService (Biometrico) creado e iniciado.
)
echo.

echo  [5/6] Iniciando el servicio GymWeb...
sc start GymWeb >nul
echo         Esperando que la app este lista...

:: Esperar hasta que la app responda (max 30 segundos)
set /a INTENTOS=0
:ESPERAR
set /a INTENTOS+=1
if !INTENTOS! GTR 15 goto :FIN_ESPERA
timeout /t 2 /nobreak >nul
powershell -NoProfile -Command "try { $r = Invoke-WebRequest -Uri 'http://localhost:!PUERTO!' -TimeoutSec 2 -UseBasicParsing -ErrorAction Stop; exit 0 } catch { exit 1 }" >nul 2>&1
if errorlevel 1 goto :ESPERAR

:FIN_ESPERA
sc query GymWeb | find "RUNNING" >nul
if errorlevel 1 (
    echo.
    echo  ADVERTENCIA: El servicio aun no reporta RUNNING.
    echo  Puedes verificarlo o iniciarlo con:  net start GymWeb
    echo.
) else (
    echo         Servicio corriendo en http://localhost:!PUERTO!
    echo.
)

echo  [6/6] Creando acceso directo en el Escritorio...
set SHORTCUT=%USERPROFILE%\Desktop\Gimnasio.lnk
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$ws = New-Object -ComObject WScript.Shell; $s = $ws.CreateShortcut('!SHORTCUT!'); $gymExe = Join-Path '%~dp0' 'Gym.exe'; if (Test-Path $gymExe) { $s.TargetPath = $gymExe; $s.Arguments = ''; } else { $s.TargetPath = 'wscript.exe'; $s.Arguments = '\"%~dp0AbrirGymWeb.vbs\"'; } $s.WorkingDirectory = '%~dp0'; $ico = Join-Path '%~dp0' 'app.ico'; if (-not (Test-Path $ico)) { $ico = Join-Path '%~dp0' 'wwwroot\favicon.ico'; } $s.IconLocation = $ico + ',0'; $s.Description = 'Sistema de Control y Gestion para Gimnasio'; $s.Save()"

if exist "%USERPROFILE%\Desktop\Gimnasio.lnk" (
    echo         Acceso directo "Gimnasio" creado con exito en el Escritorio.
) else (
    echo         No se pudo crear el acceso directo.
)
echo.

echo  ============================================================
echo   INSTALACION COMPLETADA
echo  ============================================================
echo.
echo    Acceso:      http://localhost:!PUERTO!
echo    El servicio inicia automaticamente con Windows.
echo    Icono en el Escritorio: Gimnasio
echo.
echo    El sistema es 100%% autonomo y esta listo para operar.
echo  ============================================================
echo.

timeout /t 2 /nobreak >nul
if exist "%~dp0Gym.exe" (
    start "" "%~dp0Gym.exe"
) else (
    start http://localhost:!PUERTO!
)

pause
endlocal
