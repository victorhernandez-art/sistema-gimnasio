using GymWeb.Helpers;
using GymWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace GymWeb.Controllers;

public class HomeController : AuthController
{
    private readonly GymContext _db;
    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _config;

    // Cache en memoria para no llamar a GitHub en cada recarga de página
    private static string? _cachedLatestVersion;
    private static string? _cachedDownloadUrl;
    private static string? _cachedReleaseNotes;
    private static string? _cachedReleaseTitle;
    private static DateTime _cacheExpiry = DateTime.MinValue;

    public HomeController(GymContext db, IHttpClientFactory http, IConfiguration config)
    {
        _db     = db;
        _http   = http;
        _config = config;
    }

    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Dashboard";

        ViewBag.TotalSocios     = await _db.Socios.CountAsync(s => s.IdEstado == 1);
        ViewBag.TotalMembresias = await _db.Membresia.CountAsync(m => m.IdEstado == 1);
        ViewBag.TotalProductos  = await _db.Productos.CountAsync(p => p.IdEstado == 1);
        var hoyDt = DateTime.Today;
        var mananaDt = hoyDt.AddDays(1);
        ViewBag.VisitasHoy      = await _db.Registros
            .CountAsync(r => r.FechaCreacion.HasValue &&
                             r.FechaCreacion.Value >= hoyDt &&
                             r.FechaCreacion.Value < mananaDt);

        ViewBag.UltimasVisitas = await _db.Registros
            .Include(r => r.IdSocioNavigation)
            .OrderByDescending(r => r.FechaCreacion)
            .Take(5)
            .ToListAsync();

        var next = DateTime.Today.AddDays(GymWeb.Helpers.AppSettings.DiasAviso);
        ViewBag.DiasAviso = GymWeb.Helpers.AppSettings.DiasAviso;
        ViewBag.MembresiasVencer = await _db.Vwultimamembresiadetallada
            .Where(v => v.Vencimiento.HasValue &&
                        v.Vencimiento.Value >= DateTime.Today &&
                        v.Vencimiento.Value <= next)
            .OrderBy(v => v.Vencimiento)
            .Take(6)
            .ToListAsync();

        return View();
    }

    // ── Endpoint AJAX para verificar actualizaciones disponibles desde GitHub ──
    [HttpGet]
    public async Task<IActionResult> CheckUpdate()
    {
        try
        {
            // Usar cache de 24 horas para no martillar la API de GitHub
            if (DateTime.UtcNow < _cacheExpiry && _cachedLatestVersion != null)
            {
                bool cachedHasUpdate = !string.Equals(
                    _cachedLatestVersion, AppSettings.CurrentVersion,
                    StringComparison.OrdinalIgnoreCase);

                return Json(new
                {
                    hasUpdate    = cachedHasUpdate,
                    current      = AppSettings.CurrentVersion,
                    latest       = _cachedLatestVersion,
                    downloadUrl  = _cachedDownloadUrl,
                    releaseNotes = _cachedReleaseNotes ?? "",
                    releaseTitle = _cachedReleaseTitle ?? ""
                });
            }

            // Configuración del repositorio desde appsettings.json
            var owner = _config["GitHub:Owner"] ?? "victorhernandez-art";
            var repo  = _config["GitHub:Repo"]  ?? "sistema-gimnasio";
            var apiUrl = $"https://api.github.com/repos/{owner}/{repo}/releases?per_page=10";

            var client = _http.CreateClient();
            // GitHub requiere User-Agent para aceptar peticiones
            client.DefaultRequestHeaders.Add("User-Agent", "GymWeb-UpdateChecker/2.1");
            client.Timeout = TimeSpan.FromSeconds(8);

            var response = await client.GetAsync(apiUrl);
            if (!response.IsSuccessStatusCode)
            {
                return Json(new { hasUpdate = false, current = AppSettings.CurrentVersion });
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            
            System.Text.Json.JsonElement targetRelease = default;
            if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                // Buscar el release más reciente del sistema (tag que comience con 'v', ej: v2.3, v2.2)
                foreach (var r in doc.RootElement.EnumerateArray())
                {
                    if (r.TryGetProperty("tag_name", out var t) && (t.GetString() ?? "").StartsWith("v", StringComparison.OrdinalIgnoreCase))
                    {
                        targetRelease = r;
                        break;
                    }
                }
                if (targetRelease.ValueKind == System.Text.Json.JsonValueKind.Undefined)
                {
                    targetRelease = doc.RootElement.EnumerateArray().FirstOrDefault();
                }
            }
            else
            {
                targetRelease = doc.RootElement;
            }

            if (targetRelease.ValueKind == System.Text.Json.JsonValueKind.Undefined)
            {
                return Json(new { hasUpdate = false, current = AppSettings.CurrentVersion });
            }

            // Obtener datos del release del sistema
            var latestTag    = targetRelease.TryGetProperty("tag_name", out var tagEl) ? tagEl.GetString() ?? "" : "";
            var releaseTitle = targetRelease.TryGetProperty("name", out var nameEl)     ? nameEl.GetString() ?? "" : "";
            var releaseNotes = targetRelease.TryGetProperty("body", out var bodyEl)     ? bodyEl.GetString() ?? "" : "";

            // Construir URL de descarga del parche ligero
            string? downloadUrl = null;
            if (targetRelease.TryGetProperty("assets", out var assets))
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    var assetName = asset.GetProperty("name").GetString() ?? "";
                    if (assetName.Contains("Parche", StringComparison.OrdinalIgnoreCase) ||
                        assetName.Contains("Instalador", StringComparison.OrdinalIgnoreCase))
                    {
                        downloadUrl = asset.GetProperty("browser_download_url").GetString();
                        break;
                    }
                }
            }

            // Fallback: página HTML del release en GitHub
            downloadUrl ??= targetRelease.TryGetProperty("html_url", out var htmlUrl)
                ? htmlUrl.GetString()
                : $"https://github.com/{owner}/{repo}/releases/latest";

            // Guardar en cache por 24 horas
            _cachedLatestVersion = latestTag;
            _cachedDownloadUrl   = downloadUrl;
            _cachedReleaseNotes  = releaseNotes;
            _cachedReleaseTitle  = releaseTitle;
            _cacheExpiry         = DateTime.UtcNow.AddHours(24);

            bool hasUpdate = !string.Equals(latestTag, AppSettings.CurrentVersion,
                                            StringComparison.OrdinalIgnoreCase);

            return Json(new
            {
                hasUpdate    = hasUpdate,
                current      = AppSettings.CurrentVersion,
                latest       = latestTag,
                downloadUrl  = downloadUrl,
                releaseNotes = releaseNotes,
                releaseTitle = releaseTitle
            });
        }
        catch
        {
            // Si no hay internet o falla la API, simplemente no mostramos nada
            return Json(new { hasUpdate = false, current = AppSettings.CurrentVersion });
        }
    }

    [HttpGet]
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    public async Task<IActionResult> DescargarParche()
    {
        if (string.IsNullOrEmpty(_cachedDownloadUrl))
        {
            await CheckUpdate();
        }

        var url = _cachedDownloadUrl;
        if (string.IsNullOrEmpty(url))
        {
            return NotFound("No se encontró el archivo del parche.");
        }

        try
        {
            var client = _http.CreateClient();
            client.DefaultRequestHeaders.Add("User-Agent", "GymWeb-Downloader/2.4");
            client.Timeout = TimeSpan.FromMinutes(3);
            var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
            {
                return Redirect(url);
            }

            var stream = await response.Content.ReadAsStreamAsync();
            return File(stream, "application/zip", "Parche_Ligero_GymWeb.zip");
        }
        catch
        {
            return Redirect(url);
        }
    }

    public class GuardarTicketPdfPayload
    {
        public string Filename { get; set; } = "comprobante.pdf";
        public string Base64 { get; set; } = "";
    }

    [HttpPost]
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    public async Task<IActionResult> GuardarYCopiarPdf([FromBody] GuardarTicketPdfPayload payload)
    {
        if (string.IsNullOrEmpty(payload?.Base64))
            return BadRequest(new { ok = false, msg = "No se recibió archivo PDF." });

        try
        {
            string filename = string.IsNullOrWhiteSpace(payload.Filename) ? "comprobante.pdf" : payload.Filename;
            if (!filename.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                filename += ".pdf";

            foreach (char c in Path.GetInvalidFileNameChars())
                filename = filename.Replace(c, '_');

            string userFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string downloadsFolder = Path.Combine(userFolder, "Downloads");
            if (!Directory.Exists(downloadsFolder))
                downloadsFolder = Path.GetTempPath();

            string fullPath = Path.Combine(downloadsFolder, filename);

            string cleanBase64 = payload.Base64;
            int commaIdx = cleanBase64.IndexOf(',');
            if (commaIdx >= 0)
                cleanBase64 = cleanBase64.Substring(commaIdx + 1);

            byte[] pdfBytes = Convert.FromBase64String(cleanBase64);
            await System.IO.File.WriteAllBytesAsync(fullPath, pdfBytes);

            // Copiar al portapapeles de Windows y auto-pegar en la ventana de WhatsApp (mismo mecanismo de Taller-GitHub)
            if (OperatingSystem.IsWindows())
            {
                try
                {
                    string safePath = fullPath.Replace("'", "''");
                    string psCommand = $@"
                        Set-Clipboard -Path '{safePath}';
                        $wsh = New-Object -ComObject Wscript.Shell;
                        for ($i = 0; $i -lt 10; $i++) {{
                            if ($wsh.AppActivate('WhatsApp')) {{
                                Start-Sleep -Milliseconds 350;
                                $wsh.SendKeys('^v');
                                break;
                            }}
                            Start-Sleep -Milliseconds 800;
                        }}
                    ";
                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-NoProfile -WindowStyle Hidden -Command \"{psCommand}\"",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
                    };
                    System.Diagnostics.Process.Start(psi);
                }
                catch { }
            }

            return Json(new { ok = true, path = fullPath });
        }
        catch (Exception ex)
        {
            return Json(new { ok = false, error = ex.Message });
        }
    }
}

