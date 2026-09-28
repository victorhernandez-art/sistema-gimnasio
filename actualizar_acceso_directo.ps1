$desktop = [Environment]::GetFolderPath("Desktop")
$shortcutPath = Join-Path $desktop "Gimnasio.lnk"
$baseDir = "c:\xampp\htdocs\Gym (2) - copia\Gym"
$gymExe = Join-Path $baseDir "Gym.exe"

$wsh = New-Object -ComObject WScript.Shell
$sc = $wsh.CreateShortcut($shortcutPath)

if (Test-Path $gymExe) {
    $sc.TargetPath = $gymExe
    $sc.Arguments = ""
} else {
    $sc.TargetPath = "wscript.exe"
    $sc.Arguments = "`"$baseDir\AbrirGym.vbs`""
}

$sc.WorkingDirectory = $baseDir
$sc.IconLocation = "$baseDir\app.ico,0"
$sc.Description = "Sistema de Control y Gestion de Gimnasio"
$sc.Save()

Write-Host "Acceso directo Gimnasio.lnk actualizado con exito hacia Gym.exe con la pesa cian."
