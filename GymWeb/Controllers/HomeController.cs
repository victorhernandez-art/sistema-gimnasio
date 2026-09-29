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
        ViewBag.VisitasHoy      = await _db.Registros
            .CountAsync(r => r.FechaCreacion.HasValue &&
                             r.FechaCreacion.Value.Date == DateTime.Today);

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
            var apiUrl = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";

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
            var root = doc.RootElement;

            // Obtener el tag del último release (ej: "v2.1", "v2.2")
            var latestTag    = root.GetProperty("tag_name").GetString() ?? "";
            var releaseTitle = root.TryGetProperty("name", out var nameEl)  ? nameEl.GetString() ?? "" : "";
            var releaseNotes = root.TryGetProperty("body", out var bodyEl)  ? bodyEl.GetString() ?? "" : "";

            // Construir URL de descarga del parche ligero
            // Convención: el asset del parche se llama Parche_Ligero_GymWeb.zip
            // Si no existe ese asset específico, usamos la página del release en GitHub
            string? downloadUrl = null;
            if (root.TryGetProperty("assets", out var assets))
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
            downloadUrl ??= root.TryGetProperty("html_url", out var htmlUrl)
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
}
