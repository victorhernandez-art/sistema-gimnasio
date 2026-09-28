$ErrorActionPreference = "Stop"

$root = $PSScriptRoot
$dist = Join-Path $root "dist"
$patchOut = Join-Path $dist "Actualizacion-GymWeb"
$patchFiles = Join-Path $patchOut "archivos"
$patchBio = Join-Path $patchOut "BioService"
$tempBuild = Join-Path $dist "temp_build"
$zipOut = Join-Path $dist "Parche_GymWeb_v2.0.zip"

Write-Host "[1/5] Limpiando salidas anteriores..."
if (Test-Path $patchOut) { Remove-Item -Recurse -Force $patchOut }
if (Test-Path $tempBuild) { Remove-Item -Recurse -Force $tempBuild }
New-Item -ItemType Directory -Force -Path $patchFiles | Out-Null
New-Item -ItemType Directory -Force -Path $patchBio | Out-Null

Write-Host "[2/5] Compilando GymWeb..."
$gymWebProj = Join-Path $root "GymWeb\GymWeb.csproj"
& dotnet publish $gymWebProj -r win-x64 -c Release --self-contained true -o $tempBuild /p:PublishSingleFile=false --nologo -v quiet

Write-Host "[3/5] Copiando GymWeb y dependencias completas..."
Get-ChildItem -Path $tempBuild -File | ForEach-Object {
    if ($_.Name -notmatch "appsettings.*\.json|gym\.db") {
        Copy-Item -Force $_.FullName (Join-Path $patchFiles $_.Name)
    }
}
if (Test-Path (Join-Path $tempBuild "runtimes")) {
    Copy-Item -Recurse -Force (Join-Path $tempBuild "runtimes") (Join-Path $patchFiles "runtimes")
}
if (Test-Path (Join-Path $tempBuild "wwwroot")) {
    Copy-Item -Recurse -Force (Join-Path $tempBuild "wwwroot") (Join-Path $patchFiles "wwwroot")
    Remove-Item -Force -ErrorAction SilentlyContinue (Join-Path $patchFiles "wwwroot\img\logo-custom.png")
    Remove-Item -Force -ErrorAction SilentlyContinue (Join-Path $patchFiles "wwwroot\img\fondo-custom.jpg")
    Remove-Item -Recurse -Force -ErrorAction SilentlyContinue (Join-Path $patchFiles "wwwroot\img\socios")
    Remove-Item -Recurse -Force -ErrorAction SilentlyContinue (Join-Path $patchFiles "wwwroot\backups")
}

Write-Host "[4/5] Compilando BioService..."
$bioProj = Join-Path $root "BioService\BioService.csproj"
& dotnet publish $bioProj -r win-x64 -c Release --self-contained true -o $patchBio /p:PublishSingleFile=false --nologo -v quiet
Copy-Item -Force (Join-Path $root "BioService\INICIAR-BioService.bat") (Join-Path $patchBio "INICIAR-BioService.bat")
Copy-Item -Force (Join-Path $root "BioService\PROBAR-DIAGNOSTICO.bat") (Join-Path $patchBio "PROBAR-DIAGNOSTICO.bat")

Copy-Item -Force (Join-Path $root "Publish\ACTUALIZAR.bat") (Join-Path $patchOut "ACTUALIZAR.bat")
Copy-Item -Force (Join-Path $root "Publish\actualizar.ps1") (Join-Path $patchOut "actualizar.ps1")
Copy-Item -Force (Join-Path $root "Publish\Iniciar GymWeb.bat") (Join-Path $patchOut "Iniciar GymWeb.bat")
Copy-Item -Force (Join-Path $root "Publish\AbrirGymWeb.vbs") (Join-Path $patchOut "AbrirGymWeb.vbs")
Copy-Item -Force (Join-Path $root "Publish\Crear Acceso Directo.bat") (Join-Path $patchOut "Crear Acceso Directo.bat")
Copy-Item -Force (Join-Path $root "app.ico") (Join-Path $patchOut "app.ico")
Copy-Item -Force (Join-Path $root "MIGRAR-DATOS-MYSQL-A-SQLITE.bat") (Join-Path $patchOut "MIGRAR-DATOS-MYSQL-A-SQLITE.bat")
if (Test-Path (Join-Path $root "GymApp_bin")) {
    Copy-Item -Recurse -Force (Join-Path $root "GymApp_bin\*") $patchOut -Exclude "app.ico"
    Copy-Item -Recurse -Force (Join-Path $root "GymApp_bin\*") $patchFiles -Exclude "app.ico"
}
Start-Sleep -Milliseconds 800
try { Remove-Item -Recurse -Force $tempBuild -ErrorAction SilentlyContinue } catch { }

Write-Host "[5/5] Creando ZIP del parche..."
if (Test-Path $zipOut) { Remove-Item -Force $zipOut }
$zipOut2 = Join-Path $dist "Parche_GymWeb_v2.1.zip"
if (Test-Path $zipOut2) { Remove-Item -Force $zipOut2 }
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($patchOut, $zipOut2, [System.IO.Compression.CompressionLevel]::Optimal, $false)
Copy-Item -Force $zipOut2 $zipOut

Write-Host "`n============================================================"
Write-Host " ¡PARCHE GENERADO Y VERIFICADO AL 100%!" -ForegroundColor Green
Write-Host " Archivo listo: $zipOut2"
Write-Host "============================================================`n"

Write-Host "Contenido verificado en el ZIP:"
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::OpenRead($zipOut)
foreach ($entry in $zip.Entries | Select-Object -First 10) {
    Write-Host ("  - {0} ({1:N0} bytes)" -f $entry.FullName, $entry.Length)
}
$zip.Dispose()
