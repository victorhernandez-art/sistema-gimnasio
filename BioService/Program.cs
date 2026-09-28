/**
 * ══════════════════════════════════════════════════════════════════
 *  BioService — Servicio Biométrico Universal para GymWeb
 *
 *  Reemplaza al antiguo zk9500-service (que nunca funcionó con
 *  hardware real). Este servicio usa Windows Biometric Framework
 *  para soportar CUALQUIER lector de huellas con driver Windows.
 *
 *  Puerto: 4500 (mismo que antes — compatible con GymWeb)
 *
 *  Iniciar:
 *    dotnet run
 *    — o doble clic en INICIAR-BioService.bat
 *
 *  Backends soportados (auto-detectados):
 *    🟢 WinBio   → Lectores Windows Hello (DigitalPersona, Eikon, etc.)
 *    🔵 PIN      → Teclado numérico / barcode (fallback)
 * ══════════════════════════════════════════════════════════════════
 */

using BioService.Backends;
using BioService.Tests;
using System.Text.Json;
using System.Collections.Concurrent;

if (args.Contains("--test"))
{
    DiagnosticTest.RunAll();
    return;
}

// ── Auto-detectar el mejor backend con soporte dinámico Hot-Plug ──
var backend = new DynamicBackendSelector();

// ── Estado interno ──────────────────────────────────────────────
var sseClients = new ConcurrentBag<HttpResponse>();
// Templates almacenados en memoria (cargados bajo demanda desde GymWeb)
var templateStore = new ConcurrentDictionary<int, byte[]>();

// ── Configurar API ──────────────────────────────────────────────
var builder = WebApplication.CreateSlimBuilder(args);
builder.Host.UseWindowsService();
builder.WebHost.UseUrls("http://127.0.0.1:4500");

var app = builder.Build();

// ── ApiKey compartida con GymWeb (se configura en appsettings.json) ─
var apiKey = app.Configuration["ApiKey"] ?? "";

// ── Middleware de autenticación X-Api-Key ───────────────────────
// Protege todos los endpoints excepto /status, /diagnostics y /finger (SSE)
app.Use(async (ctx, next) =>
{
    var path = ctx.Request.Path.Value ?? "";
    bool esPublico = path.StartsWith("/status", StringComparison.OrdinalIgnoreCase)
                  || path.StartsWith("/diagnostics", StringComparison.OrdinalIgnoreCase)
                  || path.StartsWith("/finger", StringComparison.OrdinalIgnoreCase);

    if (!esPublico && !string.IsNullOrEmpty(apiKey))
    {
        if (!ctx.Request.Headers.TryGetValue("X-Api-Key", out var keyHeader) || keyHeader != apiKey)
        {
            ctx.Response.StatusCode = 401;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsync("{\"ok\":false,\"error\":\"unauthorized\",\"msg\":\"Acceso denegado al servicio biométrico.\"}");
            return;
        }
    }
    await next();
});

// ── CORS (GymWeb se conecta desde otra URL) ─────────────────────
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("Access-Control-Allow-Origin", "*");
    context.Response.Headers.Append("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
    context.Response.Headers.Append("Access-Control-Allow-Headers", "Content-Type, X-Api-Key");

    if (context.Request.Method == "OPTIONS")
    {
        context.Response.StatusCode = 200;
        return;
    }
    await next();
});

// ════════════════════════════════════════════════════════════════
//  GET /status  — estado del servicio
// ════════════════════════════════════════════════════════════════
app.MapGet("/status", () =>
{
    return Results.Json(new
    {
        ok = true,
        backend = backend.Name,
        disponible = backend.IsAvailable,
        version = "2.1.0"
    });
});

// ════════════════════════════════════════════════════════════════
//  GET /diagnostics  — diagnóstico detallado de hardware USB
// ════════════════════════════════════════════════════════════════
app.MapGet("/diagnostics", () =>
{
    if (OperatingSystem.IsWindows())
    {
        var diag = HardwareDetector.RunDiagnostics(backend.Name);
        return Results.Json(new { ok = true, diag });
    }
    return Results.Json(new { ok = false, msg = "Solo disponible en Windows" });
});

// ════════════════════════════════════════════════════════════════
//  GET /devices  — dispositivos biométricos conectados
// ════════════════════════════════════════════════════════════════
app.MapGet("/devices", async () =>
{
    var devices = await backend.GetDevicesAsync();
    return Results.Json(new { ok = true, devices });
});

// ════════════════════════════════════════════════════════════════
//  POST /capture  — captura una huella (devuelve template)
// ════════════════════════════════════════════════════════════════
app.MapPost("/capture", async (CancellationToken ct) =>
{
    var result = await backend.CaptureAsync(ct);
    if (result.Ok && result.Template != null)
    {
        return Results.Json(new
        {
            ok = true,
            template = Convert.ToBase64String(result.Template)
        });
    }
    return Results.Json(new { ok = false, msg = result.Msg ?? "Error en captura." });
});

// ════════════════════════════════════════════════════════════════
//  POST /verify  — NUEVO: verifica huella contra templates guardados
//
//  Body: { "template": "<base64>", "templates": { "1001": "<base64>", ... } }
//  Responde: { "ok": true, "pin": 1001, "score": 85.2 }
// ════════════════════════════════════════════════════════════════
app.MapPost("/verify", async (HttpRequest req) =>
{
    try
    {
        var body = await JsonSerializer.DeserializeAsync<JsonElement>(req.Body);
        var probeB64 = body.GetProperty("template").GetString();
        if (string.IsNullOrEmpty(probeB64))
            return Results.Json(new { ok = false, msg = "Template vacío." });

        var probeBytes = Convert.FromBase64String(probeB64);

        // Cargar templates del body
        var stored = new Dictionary<int, byte[]>();
        if (body.TryGetProperty("templates", out var templatesObj))
        {
            foreach (var prop in templatesObj.EnumerateObject())
            {
                if (int.TryParse(prop.Name, out int pin))
                {
                    var tplB64 = prop.Value.GetString();
                    if (!string.IsNullOrEmpty(tplB64))
                        stored[pin] = Convert.FromBase64String(tplB64);
                }
            }
        }

        if (stored.Count == 0)
            return Results.Json(new { ok = false, msg = "No hay templates para comparar." });

        var result = await backend.IdentifyAsync(probeBytes, stored);
        if (result.Ok)
            return Results.Json(new { ok = true, pin = result.Pin, score = result.Score });

        return Results.Json(new { ok = false, msg = result.Msg, score = result.Score });
    }
    catch (Exception ex)
    {
        return Results.Json(new { ok = false, msg = $"Error: {ex.Message}" });
    }
});

// ════════════════════════════════════════════════════════════════
//  POST /sync  — mantener compatibilidad con el servicio anterior
// ════════════════════════════════════════════════════════════════
app.MapPost("/sync", async (HttpRequest req) =>
{
    try
    {
        var body = await JsonSerializer.DeserializeAsync<JsonElement>(req.Body);
        int pin = body.GetProperty("pin").GetInt32();
        string nombre = body.GetProperty("nombre").GetString() ?? "";

        Console.WriteLine($"[BioService] Sincronizado: PIN {pin} → {nombre}");
        return Results.Json(new { ok = true, msg = $"PIN {pin} registrado para {nombre}." });
    }
    catch (Exception ex)
    {
        return Results.Json(new { ok = false, msg = ex.Message });
    }
});

// ════════════════════════════════════════════════════════════════
//  GET /finger  — SSE: eventos en tiempo real (compatibilidad)
// ════════════════════════════════════════════════════════════════
app.MapGet("/finger", async (HttpContext ctx) =>
{
    ctx.Response.ContentType = "text/event-stream";
    ctx.Response.Headers.Append("Cache-Control", "no-cache");
    ctx.Response.Headers.Append("Connection", "keep-alive");

    sseClients.Add(ctx.Response);

    // Heartbeat para mantener la conexión
    try
    {
        while (!ctx.RequestAborted.IsCancellationRequested)
        {
            await ctx.Response.WriteAsync(":heartbeat\n\n", ctx.RequestAborted);
            await ctx.Response.Body.FlushAsync(ctx.RequestAborted);
            await Task.Delay(25000, ctx.RequestAborted);
        }
    }
    catch (OperationCanceledException) { }
});

// ════════════════════════════════════════════════════════════════
//  POST /test  — emitir evento de prueba (para testing sin lector)
// ════════════════════════════════════════════════════════════════
app.MapPost("/test", async (HttpRequest req) =>
{
    int pin = 1001;
    try
    {
        var body = await JsonSerializer.DeserializeAsync<JsonElement>(req.Body);
        if (body.TryGetProperty("pin", out var pinProp))
            pin = pinProp.GetInt32();
    }
    catch { }

    await EmitFingerEvent(pin);
    return Results.Json(new { ok = true, msg = $"Evento de prueba emitido: PIN {pin}" });
});

// ── Función para emitir evento SSE a todos los clientes ─────────
async Task EmitFingerEvent(int pin)
{
    var payload = $"data: {{\"id\": {pin}}}\n\n";
    // ConcurrentBag doesn't support removal, so we collect alive clients
    var alive = new ConcurrentBag<HttpResponse>();
    foreach (var client in sseClients)
    {
        try
        {
            await client.WriteAsync(payload);
            await client.Body.FlushAsync();
            alive.Add(client);
        }
        catch { /* client disconnected */ }
    }
    // Note: ConcurrentBag doesn't support clear; we just let old refs be GC'd
}

// ── Imprimir banner al iniciar ──────────────────────────────────
Console.WriteLine(@"
╔═══════════════════════════════════════════════════════════╗
║   BioService v2.0 — Servicio Biométrico Universal        ║
║   Puerto: 4500                                            ║
║                                                           ║
║   Backend: " + backend.Name.PadRight(44) + @"║
║                                                           ║
║   Endpoints:                                              ║
║     GET  /status   — estado del servicio                  ║
║     GET  /devices  — dispositivos conectados              ║
║     POST /capture  — capturar huella                      ║
║     POST /verify   — verificar huella vs templates        ║
║     POST /sync     — sincronizar PIN                      ║
║     GET  /finger   — SSE (eventos en tiempo real)         ║
║     POST /test     — evento de prueba                     ║
╚═══════════════════════════════════════════════════════════╝
");

// ── Iniciar listener en segundo plano ───────────────────────────
var cts = new CancellationTokenSource();
_ = Task.Run(() => backend.StartListeningAsync(EmitFingerEvent, cts.Token));

app.Run();
cts.Cancel();
