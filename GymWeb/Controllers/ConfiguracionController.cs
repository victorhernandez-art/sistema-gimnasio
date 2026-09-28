using GymWeb.Helpers;
using GymWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GymWeb.Controllers;

public class ConfiguracionController : SuperAdminController
{
    private readonly GymContext _db;
    private readonly IWebHostEnvironment _env;
    public ConfiguracionController(GymContext db, IWebHostEnvironment env) { _db = db; _env = env; }

    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Configuración";
        var config = await _db.Configuracions.FirstOrDefaultAsync();
        if (config == null)
        {
            config = new Configuracion { IdConfiguracion = 1 };
            _db.Configuracions.Add(config);
            await _db.SaveChangesAsync();
        }

        ViewBag.PcId = LicenseService.GetPcId();
        ViewBag.LicenseStatus = LicenseService.GetStatus();

        return View(config);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult IniciarPruebaGratuita()
    {
        var status = LicenseService.GetStatus();
        if (status.Valid)
        {
            TempData["Success"] = "Tu prueba o licencia ya se encuentra activa.";
            return RedirectToAction("Index", "Home");
        }

        if (status.Expired)
        {
            TempData["Error"] = "Tu período de prueba de 7 días ya ha finalizado. Por favor ingresa tu clave vitalicia oficial.";
            TempData["ActiveTab"] = "licencia";
            return RedirectToAction(nameof(Index), new { tab = "licencia" });
        }

        string pcId = LicenseService.GetPcId();
        string trialKey = LicenseService.GenerateTrialKey(pcId, 7);
        var (success, message, info) = LicenseService.Activate(trialKey);
        if (success)
        {
            TempData["Success"] = "🎉 ¡Felicidades! Tu prueba gratuita de 7 días ha sido activada con éxito. Disfruta de todos los módulos sin limitaciones.";
            return RedirectToAction("Index", "Home");
        }
        else
        {
            TempData["Error"] = message;
            TempData["ActiveTab"] = "licencia";
            return RedirectToAction(nameof(Index), new { tab = "licencia" });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ActivarLicencia(string ClaveActivacion)
    {
        if (string.IsNullOrWhiteSpace(ClaveActivacion))
        {
            TempData["Error"] = "Por favor ingresa una clave de activación válida.";
            TempData["ActiveTab"] = "licencia";
            return RedirectToAction(nameof(Index), new { tab = "licencia" });
        }

        var (success, message, info) = LicenseService.Activate(ClaveActivacion);
        if (success)
        {
            TempData["Success"] = message;
        }
        else
        {
            TempData["Error"] = message;
        }

        TempData["ActiveTab"] = "licencia";
        return RedirectToAction(nameof(Index), new { tab = "licencia" });
    }

    [HttpGet]
    public IActionResult GetLicenseStatus()
    {
        var status = LicenseService.GetStatus();
        return Json(new
        {
            pcId = LicenseService.GetPcId(),
            valid = status.Valid,
            type = status.Type,
            expired = status.Expired,
            expireDateFormatted = status.ExpireDateFormatted,
            daysLeft = status.DaysLeft
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(
        string NombreGimnacio, 
        string? Domicilio,
        string? Telefono,
        string? Mensaje,
        int? MensajeVencimiento, 
        IFormFile? FotoFondo, 
        IFormFile? FotoLogo, 
        bool EliminarLogo = false)
    {
        var existing = await _db.Configuracions.FirstOrDefaultAsync();
        if (existing == null)
        {
            existing = new Configuracion { IdConfiguracion = 1 };
            _db.Configuracions.Add(existing);
        }
        existing.NombreGimnacio     = NombreGimnacio?.Trim();
        existing.Domicilio          = Domicilio?.Trim();
        existing.Telefono           = Telefono?.Trim();
        existing.Mensaje            = Mensaje?.Trim();
        existing.MensajeVencimiento = MensajeVencimiento ?? 5;
        existing.FechaModificacion  = DateTime.Now;
        existing.IdUsuarioModifico  = UsuarioId;

        AppSettings.GymNombre    = string.IsNullOrWhiteSpace(NombreGimnacio) ? "GymPro" : NombreGimnacio.Trim();
        AppSettings.GymDomicilio = Domicilio?.Trim() ?? "";
        AppSettings.GymTelefono  = Telefono?.Trim() ?? "";
        AppSettings.GymPieTicket = string.IsNullOrWhiteSpace(Mensaje) ? "¡Gracias por su preferencia!" : Mensaje.Trim();
        AppSettings.DiasAviso    = MensajeVencimiento ?? 5;

        // Manejo de eliminación del logo
        if (EliminarLogo)
        {
            var logoDest = Path.Combine(_env.WebRootPath, "img", "logo-custom.png");
            if (System.IO.File.Exists(logoDest))
            {
                try { System.IO.File.Delete(logoDest); } catch { }
            }
            existing.Logo = null;
            AppSettings.HasCustomLogo = false;
        }
        else if (FotoLogo != null && FotoLogo.Length > 0)
        {
            var allowedLogo = new[] { "image/jpeg", "image/png", "image/webp", "image/gif", "image/svg+xml" };
            if (!allowedLogo.Contains(FotoLogo.ContentType.ToLower()))
            {
                TempData["Error"] = "Formato de logo no permitido. Usa PNG, JPG, WEBP o SVG.";
                return RedirectToAction(nameof(Index));
            }

            if (FotoLogo.ContentType.ToLower() != "image/svg+xml" && !await IsValidImageAsync(FotoLogo))
            {
                TempData["Error"] = "El archivo de logo no es una imagen válida.";
                return RedirectToAction(nameof(Index));
            }

            using var ms = new MemoryStream();
            await FotoLogo.CopyToAsync(ms);
            var logoBytes = ms.ToArray();
            existing.Logo = logoBytes;

            var logoDest = Path.Combine(_env.WebRootPath, "img", "logo-custom.png");
            await System.IO.File.WriteAllBytesAsync(logoDest, logoBytes);
            AppSettings.HasCustomLogo = true;
        }

        if (FotoFondo != null && FotoFondo.Length > 0)
        {
            var allowed = new[] { "image/jpeg", "image/png", "image/webp", "image/gif" };
            if (!allowed.Contains(FotoFondo.ContentType.ToLower()))
            {
                TempData["Error"] = "Solo se permiten imágenes (JPG, PNG, WEBP).";
                return RedirectToAction(nameof(Index));
            }

            // Verificar firma mágica del archivo — el ContentType del cliente es falsificable
            if (!await IsValidImageAsync(FotoFondo))
            {
                TempData["Error"] = "El archivo no es una imagen válida. Solo se aceptan JPG, PNG, WEBP y GIF reales.";
                return RedirectToAction(nameof(Index));
            }

            var dest = Path.Combine(_env.WebRootPath, "img", "fondo-custom.jpg");
            using var stream = new FileStream(dest, FileMode.Create);
            await FotoFondo.CopyToAsync(stream);
            AppSettings.HasCustomBg = true;
        }

        await _db.SaveChangesAsync();

        TempData["Success"] = "Configuración guardada correctamente.";
        return RedirectToAction(nameof(Index));
    }

    // Verifica que el archivo realmente sea una imagen leyendo su firma mágica
    private static readonly Dictionary<string, byte[]> MagicBytes = new()
    {
        { "image/jpeg", new byte[] { 0xFF, 0xD8, 0xFF } },
        { "image/png",  new byte[] { 0x89, 0x50, 0x4E, 0x47 } },
        { "image/webp", new byte[] { 0x52, 0x49, 0x46, 0x46 } },  // RIFF (primeros 4 bytes de WebP)
        { "image/gif",  new byte[] { 0x47, 0x49, 0x46 } },        // GIF
    };

    private static async Task<bool> IsValidImageAsync(IFormFile file)
    {
        if (file.Length < 8) return false;
        if (!MagicBytes.TryGetValue(file.ContentType.ToLower(), out var sig)) return false;
        using var s = file.OpenReadStream();
        var buf = new byte[sig.Length];
        var bytesRead = await s.ReadAtLeastAsync(buf, sig.Length, throwOnEndOfStream: false);
        return bytesRead == sig.Length && buf.SequenceEqual(sig);
    }
}
