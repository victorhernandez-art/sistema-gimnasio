using GymWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace GymWeb.Controllers;

public class BiometricoController : AuthController
{
    private readonly GymContext _db;
    private readonly IHttpClientFactory _http;
    private readonly string _bridge;
    private readonly string _apiKey;

    public BiometricoController(GymContext db, IHttpClientFactory http, IConfiguration cfg)
    {
        _db     = db;
        _http   = http;
        _bridge = cfg["BioService:Url"] ?? "http://localhost:4500";
        _apiKey = cfg["BioService:ApiKey"] ?? "";
    }

    // Crea un HttpClient con el header de autenticación X-Api-Key
    private HttpClient CreateBioClient(TimeSpan? timeout = null)
    {
        var client = _http.CreateClient();
        client.Timeout = timeout ?? TimeSpan.FromSeconds(10);
        if (!string.IsNullOrEmpty(_apiKey))
            client.DefaultRequestHeaders.Add("X-Api-Key", _apiKey);
        return client;
    }

    // ── Estado del servicio biométrico ───────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Status()
    {
        try
        {
            var client = CreateBioClient(TimeSpan.FromSeconds(3));
            var resp = await client.GetAsync($"{_bridge}/status");
            var body = await resp.Content.ReadAsStringAsync();
            return Content(body, "application/json");
        }
        catch
        {
            return Json(new { ok = false, error = "bridge_offline", msg = "El servicio biométrico no está corriendo. Ejecuta INICIAR-BioService.bat" });
        }
    }

    // ── Diagnóstico detallado de hardware USB y controladores ───────
    [HttpGet]
    public async Task<IActionResult> Diagnostico()
    {
        string backendName = "PIN / Teclado (sin lector de huellas)";
        bool bridgeOnline = false;

        try
        {
            var client = CreateBioClient(TimeSpan.FromSeconds(2));
            var resp = await client.GetAsync($"{_bridge}/status");
            if (resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                var json = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(body);
                if (json.TryGetProperty("backend", out var bProp))
                    backendName = bProp.GetString() ?? backendName;
                bridgeOnline = true;
            }
        }
        catch { }

        // Diagnóstico nativo de hardware USB y WBF en Windows
        GymWeb.Helpers.HardwareDiagnosticResult diag;
        if (OperatingSystem.IsWindows())
        {
            diag = GymWeb.Helpers.HardwareDetector.RunDiagnostics(backendName);
        }
        else
        {
            diag = new GymWeb.Helpers.HardwareDiagnosticResult
            {
                ActiveBackend = backendName,
                SummaryMessage = "Diagnóstico biométrico solo disponible en Windows."
            };
        }

        return Json(new
        {
            ok = true,
            bridgeOnline,
            diag
        });
    }

    // ── Datos del socio + config biométrica actual ──────────────────
    [HttpGet]
    public async Task<IActionResult> Config(int idSocio)
    {
        var socio = await _db.Socios.FindAsync(idSocio);
        if (socio == null) return NotFound();

        var huella = await _db.SocioHuellas.FirstOrDefaultAsync(h => h.IdSocio == idSocio);

        return Json(new
        {
            idSocio      = socio.IdSocio,
            nombre       = $"{socio.Nombre} {socio.Paterno} {socio.Materno}".Trim(),
            configurado  = huella != null,
            tieneTemplate = huella?.Template != null && huella.Template.Length > 0,
            pin          = huella?.Pin ?? socio.IdSocio,   // sugerencia: usar ID del socio
            idDispositivo = huella?.IdDispositivo,
            fechaRegistro = huella?.FechaRegistro?.ToString("dd/MM/yyyy HH:mm")
        });
    }

    // ── Guardar / actualizar asignación PIN + template ───────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Guardar(int idSocio, int pin, string? idDispositivo, string? templateBase64)
    {
        if (pin < 1 || pin > 999999)
            return Json(new { ok = false, msg = "PIN debe ser un número entre 1 y 999999." });

        // Verificar que el PIN no lo tenga otro socio
        var conflicto = await _db.SocioHuellas
            .AnyAsync(h => h.Pin == pin && h.IdSocio != idSocio);
        if (conflicto)
            return Json(new { ok = false, msg = "Ese PIN ya está asignado a otro socio." });

        var huella = await _db.SocioHuellas.FirstOrDefaultAsync(h => h.IdSocio == idSocio);
        if (huella == null)
        {
            huella = new SocioHuella
            {
                IdSocio       = idSocio,
                Pin           = pin,
                IdDispositivo = idDispositivo,
                FechaRegistro = DateTime.Now
            };
            _db.SocioHuellas.Add(huella);
        }
        else
        {
            huella.Pin           = pin;
            huella.IdDispositivo = idDispositivo;
        }

        // Guardar template biométrico si se proporcionó
        if (!string.IsNullOrEmpty(templateBase64))
        {
            try
            {
                huella.Template = Convert.FromBase64String(templateBase64);
                huella.FechaRegistro = DateTime.Now; // actualizar fecha al recapturar huella
            }
            catch
            {
                return Json(new { ok = false, msg = "Error al procesar el template biométrico." });
            }
        }

        await _db.SaveChangesAsync();
        return Json(new { ok = true });
    }

    // ── Verificar huella contra todos los templates ─────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Verificar()
    {
        try
        {
            // 1. Capturar huella desde el servicio bridge
            var client = CreateBioClient(TimeSpan.FromSeconds(45));
            var captureResp = await client.PostAsync($"{_bridge}/capture", null);
            var captureBody = await captureResp.Content.ReadAsStringAsync();
            var captureData = JsonSerializer.Deserialize<JsonElement>(captureBody);

            if (!captureData.GetProperty("ok").GetBoolean())
            {
                var msg = captureData.TryGetProperty("msg", out var msgProp) ? msgProp.GetString() : "Error en captura.";
                return Json(new { ok = false, msg });
            }

            var probeTemplate = captureData.GetProperty("template").GetString();

            // 2. Obtener todos los templates almacenados
            var huellas = await _db.SocioHuellas
                .Where(h => h.Template != null && h.IdEstado == 1)
                .Select(h => new { h.Pin, h.Template })
                .ToListAsync();

            if (huellas.Count == 0)
                return Json(new { ok = false, msg = "No hay huellas registradas en el sistema." });

            // 3. Enviar al bridge para matching
            var templates = new Dictionary<string, string>();
            foreach (var h in huellas)
            {
                if (h.Template != null)
                    templates[h.Pin.ToString()] = Convert.ToBase64String(h.Template);
            }

            var verifyPayload = JsonSerializer.Serialize(new
            {
                template = probeTemplate,
                templates
            });

            var verifyResp = await client.PostAsync($"{_bridge}/verify",
                new StringContent(verifyPayload, System.Text.Encoding.UTF8, "application/json"));
            var verifyBody = await verifyResp.Content.ReadAsStringAsync();

            return Content(verifyBody, "application/json");
        }
        catch
        {
            return Json(new { ok = false, error = "bridge_offline", msg = "Servicio biométrico no disponible." });
        }
    }

    // ── Proxy → Bridge: listar dispositivos conectados ──────────────
    [HttpGet]
    public async Task<IActionResult> Dispositivos()
    {
        try
        {
            var client = CreateBioClient(TimeSpan.FromSeconds(3));
            var resp   = await client.GetAsync($"{_bridge}/devices");
            var body   = await resp.Content.ReadAsStringAsync();
            return Content(body, "application/json");
        }
        catch
        {
            return Json(new { ok = false, error = "bridge_offline" });
        }
    }

    // ── Proxy → Bridge: iniciar captura de huella ───────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> IniciarCaptura()
    {
        try
        {
            var client = CreateBioClient(TimeSpan.FromSeconds(45));
            var resp   = await client.PostAsync($"{_bridge}/capture", null);
            var body   = await resp.Content.ReadAsStringAsync();
            return Content(body, "application/json");
        }
        catch
        {
            return Json(new { ok = false, error = "bridge_offline" });
        }
    }

    // ── Proxy → Bridge: sincronizar usuario al dispositivo ──────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Sincronizar([FromBody] SyncRequest req)
    {
        try
        {
            var client  = CreateBioClient(TimeSpan.FromSeconds(10));
            var payload = new StringContent(
                JsonSerializer.Serialize(req),
                System.Text.Encoding.UTF8,
                "application/json");
            var resp = await client.PostAsync($"{_bridge}/sync", payload);
            var body = await resp.Content.ReadAsStringAsync();
            return Content(body, "application/json");
        }
        catch
        {
            return Json(new { ok = false, error = "bridge_offline" });
        }
    }

    public record SyncRequest(int Pin, string Nombre, string? Dispositivo);
}
