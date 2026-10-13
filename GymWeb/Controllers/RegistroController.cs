using GymWeb.Helpers;
using GymWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GymWeb.Controllers;

public class RegistroController : AuthController
{
    private readonly GymContext _db;
    public RegistroController(GymContext db) => _db = db;

    private async Task<decimal> ObtenerPrecioVisitaDiaAsync()
    {
        // 1. Buscar membresía activa cuyo nombre contenga 'visita' o 'día' o 'dia' (ordenada por la más reciente o con id más alto)
        var todasMembresias = await _db.Membresia
            .Where(m => m.IdEstado == 1)
            .OrderByDescending(m => m.FechaCreacion)
            .ThenByDescending(m => m.IdMembresia)
            .ToListAsync();

        // Buscar coincidencia por nombre ("visita", "dia", "día")
        var memVisita = todasMembresias.FirstOrDefault(m => 
            !string.IsNullOrWhiteSpace(m.Nombre) && 
            (m.Nombre.Contains("visita", StringComparison.OrdinalIgnoreCase) || 
             m.Nombre.Contains("día", StringComparison.OrdinalIgnoreCase) || 
             m.Nombre.Contains("dia", StringComparison.OrdinalIgnoreCase)));

        if (memVisita != null && memVisita.Precio.HasValue && memVisita.Precio.Value > 0)
            return memVisita.Precio.Value;

        // 2. Si no, buscar la de 1 día (Meses == 1) más reciente
        var mem1Dia = todasMembresias.FirstOrDefault(m => m.Meses == 1 && m.Precio.HasValue && m.Precio.Value > 0);
        if (mem1Dia != null)
            return mem1Dia.Precio!.Value;

        // 3. Fallback a configuración o default
        var config = await _db.Configuracions.FirstOrDefaultAsync();
        return config?.PrecioVisita ?? 0m;
    }

    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Registro de Visitas";
        var visitas = await _db.Vwultimamembresiadetallada
            .OrderBy(v => v.Paterno)
            .ToListAsync();

        ViewBag.SociosDict = await _db.Socios
            .AsNoTracking()
            .ToDictionaryAsync(s => s.IdSocio);

        ViewBag.PrecioVisita = await ObtenerPrecioVisitaDiaAsync();
        return View(visitas);
    }

    [HttpGet]
    public async Task<IActionResult> BuscarSocio(int id)
    {
        if (id == 1000 && !await _db.Socios.AnyAsync(s => s.IdSocio == 1000))
        {
            var precio = await ObtenerPrecioVisitaDiaAsync();
            return Json(new {
                found = true,
                esVisitaDia = true,
                idSocio = 1000,
                nombre = "Visita de Día (General)",
                membresia = $"Visita de Día — {GymWeb.Helpers.AppSettings.MonedaSimbolo}{precio:N2}",
                precioVisita = precio,
                vencimiento = DateTime.Today.ToString("dd/MM/yyyy"),
                vencido = false,
                sinMembresia = false,
                porVencer = false,
                diasRestantes = 0
            });
        }
        if (id < 1) return Json(new { found = false });
        var s = await _db.Vwultimamembresiadetallada
            .FirstOrDefaultAsync(v => v.IdSocio == id);
        if (s == null) return Json(new { found = false });
        var vencido     = s.Vencimiento.HasValue && s.Vencimiento.Value < DateTime.Now;
        var sinMembresia= !s.Vencimiento.HasValue;
        int? diasRest   = s.Vencimiento.HasValue && !vencido
            ? (int)Math.Ceiling((s.Vencimiento.Value - DateTime.Now).TotalDays) : null;
        var porVencer   = diasRest.HasValue && diasRest.Value <= GymWeb.Helpers.AppSettings.DiasAviso;
        return Json(new {
            found       = true,
            idSocio     = s.IdSocio,
            nombre      = s.NombreCompleto,
            dni         = s.NumeroDocumento,
            membresia   = s.NombreMembresia ?? "Sin membresía",
            vencimiento = s.Vencimiento?.ToString("dd/MM/yyyy"),
            vencido,
            sinMembresia,
            porVencer,
            diasRestantes = diasRest
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Registrar(int idSocio)
    {
        if (idSocio == 1000 && !await _db.Socios.AnyAsync(s => s.IdSocio == 1000))
        {
            return await RegistrarVisita("1000");
        }
        if (idSocio < 1) { TempData["Error"] = "Clave inválida para socio."; return RedirectToAction(nameof(Index)); }
        _db.Registros.Add(new Registro
        {
            IdSocio = idSocio,
            FechaCreacion = DateTime.Now
        });
        await _db.SaveChangesAsync();
        LiveSyncService.NotifyVisitasChanged();
        TempData["Success"] = "Visita registrada.";
        return RedirectToAction(nameof(Index));
    }

    // AJAX — registers entry and returns JSON for the biometric modal
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegistrarAjax(int idSocio, string? nombreVisita = null)
    {
        if (idSocio == 1000 && !await _db.Socios.AnyAsync(s => s.IdSocio == 1000))
        {
            var precio = await ObtenerPrecioVisitaDiaAsync();
            string nombreFinal = string.IsNullOrWhiteSpace(nombreVisita) || nombreVisita.Trim() == "1000"
                ? "Visita de Día #1000"
                : nombreVisita.Trim();

            var reg = new Registro
            {
                IdSocio = null,
                NombreVisita = nombreFinal,
                PrecioVisita = precio,
                FechaCreacion = DateTime.Now
            };
            _db.Registros.Add(reg);
            await _db.SaveChangesAsync();
            LiveSyncService.NotifyVisitasChanged();

            return Json(new {
                ok = true,
                esVisitaDia = true,
                idSocio = 1000,
                nombre = reg.NombreVisita,
                membresia = $"Visita de Día — {GymWeb.Helpers.AppSettings.MonedaSimbolo}{precio:N2}",
                precio = precio,
                vencimiento = DateTime.Today.ToString("dd/MM/yyyy"),
                vencido = false,
                sinMem = false,
                porVencer = false,
                diasRestantes = 0,
                yaRegistrado = false
            });
        }

        if (idSocio < 1) return Json(new { ok = false, msg = "Clave inválida." });
        var s = await _db.Vwultimamembresiadetallada
            .FirstOrDefaultAsync(v => v.IdSocio == idSocio);
        if (s == null) return Json(new { ok = false, msg = "Socio no encontrado." });

        // Prevenir doble check-in — solo registrar si no entró hoy
        var hoy = DateTime.Today;
        var manana = hoy.AddDays(1);
        var yaRegistrado = await _db.Registros
            .AnyAsync(r => r.IdSocio == idSocio && r.FechaCreacion.HasValue && r.FechaCreacion.Value >= hoy && r.FechaCreacion.Value < manana);

        if (yaRegistrado)
            return Json(new { ok = false, yaRegistrado = true,
                msg = $"{s.NombreSocio} ya fue registrado hoy. ¿Confirmas una segunda entrada?" });

        _db.Registros.Add(new Registro { IdSocio = idSocio, FechaCreacion = DateTime.Now });
        await _db.SaveChangesAsync();
        LiveSyncService.NotifyVisitasChanged();

        var vencido     = s.Vencimiento.HasValue && s.Vencimiento.Value < DateTime.Now;
        var sinMem      = !s.Vencimiento.HasValue;
        int? diasRest   = s.Vencimiento.HasValue && !vencido
            ? (int)Math.Ceiling((s.Vencimiento.Value - DateTime.Now).TotalDays) : null;
        var porVencer   = diasRest.HasValue && diasRest.Value <= GymWeb.Helpers.AppSettings.DiasAviso;
        return Json(new {
            ok          = true,
            idSocio     = s.IdSocio,
            nombre      = s.NombreCompleto,
            dni         = s.NumeroDocumento,
            membresia   = s.NombreMembresia ?? "Sin membresía",
            vencimiento = s.Vencimiento?.ToString("dd/MM/yyyy"),
            vencido,
            sinMem,
            porVencer,
            diasRestantes = diasRest,
            yaRegistrado  = false
        });
    }

    // Segunda entrada — el recepcionista confirmó explícitamente desde el modal de advertencia
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegistrarAjaxForzado(int idSocio)
    {
        if (idSocio < 1) return Json(new { ok = false, msg = "Clave inválida." });
        var s = await _db.Vwultimamembresiadetallada
            .FirstOrDefaultAsync(v => v.IdSocio == idSocio);
        if (s == null) return Json(new { ok = false, msg = "Socio no encontrado." });

        // Registrar sin verificar duplicado — el recepcionista ya confirmó
        _db.Registros.Add(new Registro { IdSocio = idSocio, FechaCreacion = DateTime.Now });
        await _db.SaveChangesAsync();
        LiveSyncService.NotifyVisitasChanged();

        var vencido     = s.Vencimiento.HasValue && s.Vencimiento.Value < DateTime.Now;
        var sinMem      = !s.Vencimiento.HasValue;
        int? diasRest   = s.Vencimiento.HasValue && !vencido
            ? (int)Math.Ceiling((s.Vencimiento.Value - DateTime.Now).TotalDays) : null;
        var porVencer   = diasRest.HasValue && diasRest.Value <= GymWeb.Helpers.AppSettings.DiasAviso;
        return Json(new {
            ok          = true,
            idSocio     = s.IdSocio,
            nombre      = s.NombreCompleto,
            dni         = s.NumeroDocumento,
            membresia   = s.NombreMembresia ?? "Sin membresía",
            vencimiento = s.Vencimiento?.ToString("dd/MM/yyyy"),
            vencido,
            sinMem,
            porVencer,
            diasRestantes = diasRest,
            yaRegistrado  = true   // indicar al modal que es segunda entrada
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegistrarVisita(string nombreVisita)
    {
        string nombreLimpio = (nombreVisita ?? "").Trim();
        if (string.IsNullOrWhiteSpace(nombreLimpio))
        {
            TempData["Error"] = "Ingrese el código 1000 o el nombre de la persona.";
            return RedirectToAction(nameof(Index));
        }

        var precio = await ObtenerPrecioVisitaDiaAsync();
        string nombreFinal = (nombreLimpio == "1000") ? "Visita de Día #1000" : nombreLimpio;

        _db.Registros.Add(new Registro
        {
            IdSocio = null,
            NombreVisita = nombreFinal,
            PrecioVisita = precio,
            FechaCreacion = DateTime.Now
        });
        await _db.SaveChangesAsync();
        LiveSyncService.NotifyVisitasChanged();

        var precioStr = precio > 0 ? $" — ${precio:N2}" : "";
        TempData["Success"] = $"Visita de día registrada: {nombreFinal}{precioStr}";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegistrarVisitaAjax(string nombreVisita)
    {
        string nombreLimpio = (nombreVisita ?? "").Trim();
        if (string.IsNullOrWhiteSpace(nombreLimpio))
        {
            return Json(new { ok = false, msg = "Ingrese el código 1000 o el nombre de la persona." });
        }

        var precio = await ObtenerPrecioVisitaDiaAsync();
        string nombreFinal = (nombreLimpio == "1000") ? "Visita de Día #1000" : nombreLimpio;

        var reg = new Registro
        {
            IdSocio = null,
            NombreVisita = nombreFinal,
            PrecioVisita = precio,
            FechaCreacion = DateTime.Now
        };
        _db.Registros.Add(reg);
        await _db.SaveChangesAsync();
        LiveSyncService.NotifyVisitasChanged();

        return Json(new {
            ok = true,
            esVisitaDia = true,
            idRegistro = reg.Idregistro,
            nombre = reg.NombreVisita,
            precio = precio,
            fecha = reg.FechaCreacion?.ToString("dd/MM/yyyy HH:mm"),
            msg = $"Visita de día registrada: {reg.NombreVisita} — ${precio:N2}"
        });
    }

    public async Task<IActionResult> Historial(DateTime? desde, DateTime? hasta)
    {
        ViewData["Title"] = "Historial de Visitas";
        DateTime dtDesde = (desde ?? DateTime.Today.AddDays(-30)).Date;
        DateTime dtHasta = (hasta ?? DateTime.Today).Date.AddDays(1);
        ViewBag.Desde = (desde ?? DateTime.Today.AddDays(-30)).ToString("yyyy-MM-dd");
        ViewBag.Hasta = (hasta ?? DateTime.Today).ToString("yyyy-MM-dd");

        var registros = await _db.Registros
            .Include(r => r.IdSocioNavigation)
            .Where(r => r.FechaCreacion.HasValue &&
                        r.FechaCreacion.Value >= dtDesde &&
                        r.FechaCreacion.Value < dtHasta)
            .OrderByDescending(r => r.FechaCreacion)
            .ToListAsync();
        return View(registros);
    }
}
