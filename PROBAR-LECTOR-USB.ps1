Write-Host '===============================================================' -ForegroundColor Cyan
Write-Host '   ASISTENTE DE DIAGNOSTICO DE LECTOR DE HUELLA USB - GymWeb   ' -ForegroundColor Cyan
Write-Host '===============================================================' -ForegroundColor Cyan
Write-Host ''
Write-Host 'Buscando lectores de huella y dispositivos biometricos en tu PC...' -ForegroundColor Gray
Write-Host ''

$wbf = Get-PnpDevice -Class Biometric -ErrorAction SilentlyContinue
$pnp = Get-PnpDevice -ErrorAction SilentlyContinue | Where-Object { 
    $_.FriendlyName -match 'Fingerprint|Biometric|ZK9500|ZK4500|ZKTeco|DigitalPersona|U\.are\.U|SecuGen' -or 
    $_.InstanceId -match 'VID_1B55|VID_05BA|VID_1162|VID_0A5C' 
}

if ($wbf) {
    Write-Host '(OK - VERDE) Lector compatible con Windows Biometric Framework detectado:' -ForegroundColor Green
    foreach ($dev in $wbf) {
        Write-Host "  * $($dev.FriendlyName) [Estado: $($dev.Status)]" -ForegroundColor Green
    }
    Write-Host ''
    Write-Host '  -> Tu lector esta 100% listo para capturar huellas en GymWeb.' -ForegroundColor Green
} elseif ($pnp) {
    Write-Host '(ATENCION - AMARILLO) Dispositivo de huella detectado en puerto USB pero sin driver WBF:' -ForegroundColor Yellow
    foreach ($dev in $pnp) {
        Write-Host "  * $($dev.FriendlyName) [ID: $($dev.InstanceId)]" -ForegroundColor White
    }
    Write-Host ''
    Write-Host '  RECOMENDACION:' -ForegroundColor Yellow
    Write-Host '  -> Si es DigitalPersona: Instala el controlador oficial DigitalPersona WBF Driver.' -ForegroundColor Yellow
    Write-Host '  -> Si es ZKTeco: Instala los controladores zkusb.sys y el SDK ZKFinger.' -ForegroundColor Yellow
} else {
    Write-Host '(SIN LECTOR - GRIS) No se detecto ningun lector de huella conectado por USB.' -ForegroundColor Red
    Write-Host '  * Asegurate de que el cable USB este bien conectado a la computadora.' -ForegroundColor Gray
    Write-Host '  * Si no tienes lector, el sistema opera normalmente mediante Clave PIN o Tarjeta.' -ForegroundColor Gray
}

Write-Host ''
Write-Host '===============================================================' -ForegroundColor Gray
Write-Host 'Comprobando servicio local BioService (puerto 4500)...' -ForegroundColor Gray
Write-Host ''

try {
    $res = Invoke-RestMethod -Uri 'http://localhost:4500/status' -TimeoutSec 3
    Write-Host '(OK) BioService activo en puerto 4500.' -ForegroundColor Green
    Write-Host "  * Backend actual : $($res.backend)" -ForegroundColor Cyan
    Write-Host "  * Disponible     : $($res.disponible)" -ForegroundColor Cyan
    Write-Host "  * Version        : $($res.version)" -ForegroundColor Cyan
} catch {
    Write-Host '(AVISO) BioService no esta respondiendo en el puerto 4500.' -ForegroundColor Yellow
    Write-Host '  Inicia el sistema mediante Iniciar Gym.bat o INICIAR-BioService.bat.' -ForegroundColor Gray
}

Write-Host ''
Write-Host '===============================================================' -ForegroundColor Gray
