using System.Diagnostics;
using System.IO.Compression;

namespace GymWeb.Helpers;

public class UpdateProgressInfo
{
    public string Status { get; set; } = "idle"; // idle, downloading, extracting, installing, ready, error, cancelled
    public int Percent { get; set; } = 0;
    public long DownloadedBytes { get; set; } = 0;
    public long TotalBytes { get; set; } = 0;
    public string DownloadedFormatted { get; set; } = "0 KB";
    public string TotalFormatted { get; set; } = "0 KB";
    public string SpeedText { get; set; } = "";
    public string Message { get; set; } = "";
    public string? Error { get; set; }
}

public static class AutoUpdateService
{
    private static readonly object _lock = new();
    private static CancellationTokenSource? _cts;
    private static UpdateProgressInfo _progress = new();
    private static DateTime _lastDownloadTime = DateTime.MinValue;
    private static long _lastDownloadedBytes = 0;

    public static UpdateProgressInfo GetProgress()
    {
        lock (_lock)
        {
            return new UpdateProgressInfo
            {
                Status = _progress.Status,
                Percent = _progress.Percent,
                DownloadedBytes = _progress.DownloadedBytes,
                TotalBytes = _progress.TotalBytes,
                DownloadedFormatted = FormatBytes(_progress.DownloadedBytes),
                TotalFormatted = FormatBytes(_progress.TotalBytes),
                SpeedText = _progress.SpeedText,
                Message = _progress.Message,
                Error = _progress.Error
            };
        }
    }

    public static void Cancel()
    {
        lock (_lock)
        {
            if (_cts != null)
            {
                try { _cts.Cancel(); } catch { }
                _cts = null;
            }
            _progress = new UpdateProgressInfo { Status = "cancelled", Message = "Descarga cancelada por el usuario." };
        }
    }

    public static string DetectTargetDirectory()
    {
        var baseDir = AppContext.BaseDirectory.TrimEnd('\\', '/');

        // Si estamos ejecutando desde compilación de desarrollo (bin/Debug o bin/Release)
        if (baseDir.Contains(@"\bin\Debug\") || baseDir.Contains(@"\bin\Release\"))
        {
            var pubDir = Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\..\Publish"));
            if (Directory.Exists(pubDir)) return pubDir;

            var gymRoot = Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\.."));
            if (Directory.Exists(gymRoot)) return gymRoot;
        }

        if (File.Exists(Path.Combine(baseDir, "GymWeb.dll")) || File.Exists(Path.Combine(baseDir, "GymWeb.exe")))
        {
            return baseDir;
        }

        var curDir = Directory.GetCurrentDirectory().TrimEnd('\\', '/');
        if (File.Exists(Path.Combine(curDir, "GymWeb.dll")) || File.Exists(Path.Combine(curDir, "GymWeb.exe")))
        {
            return curDir;
        }

        string[] standardPaths = {
            @"C:\GymWeb",
            @"C:\Gym",
            @"c:\xampp\htdocs\Gym-Paises\Gym\Publish",
            @"c:\xampp\htdocs\Gym-Paises\Gym",
            @"C:\Program Files\GymWeb"
        };
        foreach (var path in standardPaths)
        {
            if (Directory.Exists(path) && (File.Exists(Path.Combine(path, "GymWeb.dll")) || File.Exists(Path.Combine(path, "GymWeb.exe"))))
            {
                return path;
            }
        }

        return baseDir;
    }

    public static string FormatBytes(long bytes)
    {
        if (bytes <= 0) return "0 KB";
        if (bytes >= 1048576 * 1024L) return $"{(bytes / (1048576.0 * 1024.0)):0.00} GB";
        if (bytes >= 1048576) return $"{(bytes / 1048576.0):0.0} MB";
        if (bytes >= 1024) return $"{(bytes / 1024.0):0.0} KB";
        return $"{bytes} B";
    }

    public static bool StartUpdate(string downloadUrl, string targetDir, IHttpClientFactory httpFactory)
    {
        lock (_lock)
        {
            if (_progress.Status == "downloading" || _progress.Status == "extracting" || _progress.Status == "installing")
            {
                return false; // Ya hay una operación en curso
            }

            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            _progress = new UpdateProgressInfo
            {
                Status = "downloading",
                Percent = 0,
                Message = "Iniciando descarga del paquete de actualización..."
            };
        }

        var token = _cts.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                string tempDir = Path.Combine(Path.GetTempPath(), "GymWeb_Update");
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, true); } catch { }
                }
                Directory.CreateDirectory(tempDir);

                string zipPath = Path.Combine(tempDir, "Parche_Update.zip");
                string extractDir = Path.Combine(tempDir, "extracted");

                // 1. Descarga con reporte de progreso
                var client = httpFactory.CreateClient();
                client.DefaultRequestHeaders.Add("User-Agent", "GymWeb-AutoUpdater/2.5");
                client.Timeout = TimeSpan.FromMinutes(10);

                using (var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, token))
                {
                    response.EnsureSuccessStatusCode();

                    long totalBytes = response.Content.Headers.ContentLength ?? 0;
                    lock (_lock)
                    {
                        _progress.TotalBytes = totalBytes;
                        _lastDownloadTime = DateTime.UtcNow;
                        _lastDownloadedBytes = 0;
                    }

                    using var stream = await response.Content.ReadAsStreamAsync(token);
                    using var fileStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

                    byte[] buffer = new byte[81920];
                    long totalRead = 0;
                    int bytesRead;

                    while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
                    {
                        token.ThrowIfCancellationRequested();
                        await fileStream.WriteAsync(buffer, 0, bytesRead, token);
                        totalRead += bytesRead;

                        var now = DateTime.UtcNow;
                        var elapsed = (now - _lastDownloadTime).TotalSeconds;

                        lock (_lock)
                        {
                            _progress.DownloadedBytes = totalRead;
                            if (totalBytes > 0)
                            {
                                _progress.Percent = (int)Math.Min(85, (totalRead * 85) / totalBytes);
                            }

                            if (elapsed >= 0.5)
                            {
                                double bytesPerSec = (totalRead - _lastDownloadedBytes) / elapsed;
                                _progress.SpeedText = bytesPerSec switch
                                {
                                    >= 1048576 => $"{(bytesPerSec / 1048576.0):0.0} MB/s",
                                    >= 1024 => $"{(bytesPerSec / 1024.0):0.0} KB/s",
                                    _ => "Calculando..."
                                };
                                _lastDownloadTime = now;
                                _lastDownloadedBytes = totalRead;
                            }
                        }
                    }
                }

                token.ThrowIfCancellationRequested();

                // 2. Extracción del archivo comprimido
                lock (_lock)
                {
                    _progress.Status = "extracting";
                    _progress.Percent = 90;
                    _progress.Message = "Extrayendo archivos de la actualización...";
                }

                if (Directory.Exists(extractDir))
                {
                    try { Directory.Delete(extractDir, true); } catch { }
                }
                Directory.CreateDirectory(extractDir);

                ZipFile.ExtractToDirectory(zipPath, extractDir, overwriteFiles: true);

                // 3. Preparar aplicación en caliente
                lock (_lock)
                {
                    _progress.Status = "installing";
                    _progress.Percent = 96;
                    _progress.Message = "Aplicando actualización en caliente...";
                }

                string sourceFilesDir = Directory.Exists(Path.Combine(extractDir, "archivos"))
                    ? Path.Combine(extractDir, "archivos")
                    : extractDir;

                string batchScript = Path.Combine(tempDir, "hot_patch_apply.bat");
                string safeTarget = targetDir.TrimEnd('\\', '/');

                string batchContent = $@"@echo off
setlocal
timeout /t 2 /nobreak >nul

:: Detener servicio Windows si existe
sc query GymWeb >nul 2>&1
if %errorlevel% equ 0 (
    net stop GymWeb >nul 2>&1
    timeout /t 1 /nobreak >nul
)

:: Detener procesos en ejecucion de GymWeb
taskkill /F /IM GymWeb.exe >nul 2>&1
taskkill /F /IM Gym.exe >nul 2>&1
timeout /t 1 /nobreak >nul

:: Copiar archivos actualizados preservando base de datos, licencias y configuracion
robocopy ""{sourceFilesDir}"" ""{safeTarget}"" /E /XF gym.db license*.json appsettings*.json /NP /R:2 /W:1 >nul 2>&1
if exist ""{extractDir}\ACTUALIZAR.bat"" (
    robocopy ""{extractDir}"" ""{safeTarget}"" /LEV:1 /XF gym.db license*.json appsettings*.json /NP /R:2 /W:1 >nul 2>&1
)

:: Reiniciar la aplicacion
sc query GymWeb >nul 2>&1
if %errorlevel% equ 0 (
    net start GymWeb >nul 2>&1
) else if exist ""{safeTarget}\Gym.exe"" (
    start """" ""{safeTarget}\Gym.exe""
) else if exist ""{safeTarget}\Iniciar GymWeb.bat"" (
    start """" /B ""{safeTarget}\Iniciar GymWeb.bat""
) else if exist ""{safeTarget}\GymWeb.exe"" (
    start """" /B ""{safeTarget}\GymWeb.exe""
)
exit
";
                await File.WriteAllTextAsync(batchScript, batchContent);

                // Marcar como listo
                lock (_lock)
                {
                    _progress.Status = "ready";
                    _progress.Percent = 100;
                    _progress.Message = "¡Actualización completada! Reiniciando sistema...";
                }

                // Esperar 1.8 segundos para que la respuesta HTTP llegue al navegador
                await Task.Delay(1800, CancellationToken.None);

                // Ejecutar script hot_patch en segundo plano sin ventana
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c \"{batchScript}\"",
                    CreateNoWindow = true,
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                Process.Start(psi);

                // Terminar proceso actual de GymWeb limpiamente
                _ = Task.Run(async () =>
                {
                    await Task.Delay(2000);
                    Environment.Exit(0);
                });
            }
            catch (OperationCanceledException)
            {
                lock (_lock)
                {
                    _progress.Status = "cancelled";
                    _progress.Message = "Descarga cancelada por el usuario.";
                }
            }
            catch (Exception ex)
            {
                lock (_lock)
                {
                    _progress.Status = "error";
                    _progress.Error = ex.Message;
                    _progress.Message = $"Error al actualizar: {ex.Message}";
                }
            }
        }, token);

        return true;
    }
}

