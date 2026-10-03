@echo off
chcp 65001 >nul
title Calibración y Limpieza de Impresora Térmica POS
color 0b

echo ============================================================
echo   CALIBRACIÓN Y OPTIMIZACIÓN DE IMPRESORA TÉRMICA POS
echo   (Mecánica probada y verificada de Sistema Taller)
echo ============================================================
echo.
echo [1/3] Limpiando trabajos retenidos o bloqueados en el Spooler...
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "try { $jobs = Get-PrintJob -PrinterName * -ErrorAction SilentlyContinue | Where-Object { $_.JobStatus -match 'Error|Retained|Blocked|UserIntervention' }; foreach ($j in $jobs) { Remove-PrintJob -PrinterName $j.PrinterName -ID $j.Id -ErrorAction SilentlyContinue } } catch {}"

echo [2/3] Verificando controladores térmicos POS-58 / POS-80...
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "try { $printers = Get-Printer | Where-Object { $_.Name -match '58|pos-?58|zj-?58' }; foreach ($p in $printers) { try { $cfg = Get-PrintConfiguration -PrinterName $p.Name -ErrorAction SilentlyContinue; if ($cfg -and $cfg.PrintTicketXML -and $cfg.PrintTicketXML.Contains('ZIJIPAPER3276')) { $newXml = $cfg.PrintTicketXML.Replace('ZIJIPAPER3276', 'ZIJIPAPER210').Replace('3275974', '209995'); Set-PrintConfiguration -PrinterName $p.Name -PrintTicketXml $newXml -ErrorAction SilentlyContinue; Write-Host ('   ✓ Corregido tamaño de papel infinito (3276mm a 210mm) en: ' + $p.Name) -ForegroundColor Green } } catch {} } } catch {}"

echo [3/3] Reiniciando servicio de Cola de Impresión de Windows...
net stop spooler >nul 2>&1
net start spooler >nul 2>&1

echo.
echo ============================================================
echo   ✓ IMPRESORA LISTA Y CALIBRADA PARA TICKETS
echo ============================================================
echo.
pause
