using GymWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GymWeb.Controllers;

public class RegistroController : AuthController
{
    private readonly GymContext _db;
    public RegistroController(GymContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Registro de Visitas";
        var visitas = await _db.Vwultimamembresiadetallada
            .OrderBy(v => v.Paterno)
            .ToListAsync();
        // Precio de visita de día = membresía con duración de 1 día
        var precioVisita = await _db.Membresia
            .Where(m => m.Meses == 1 && m.IdEstado == 1)
            .Select(m => m.Precio)
            .FirstOrDefaultAsync() ?? 0m;
        ViewBag.PrecioVisita = precioVisita;
        return View(visitas);
    }

    [HttpGet]
    public async Task<IActionResult> BuscarSocio(int id)
    {
        if (id < 1001) return Json(new { found = false });
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
            nombre      = $"{s.NombreSocio} {s.Paterno} {s.Materno}".Trim(),
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
        if (idSocio < 1001) { TempData["Error"] = "Clave inv\u00e1lida para socio."; return RedirectToAction(nameof(Index)); }
        _db.Registros.Add(new Registro
        {
            IdSocio = idSocio,
            FechaCreacion = DateTime.Now
        });
        await _db.SaveChangesAsync();
        TempData["Success"] = "Visita registrada.";
        return RedirectToAction(nameof(Index));
    }

    // AJAX — registers entry and returns JSON for the biometric modal
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegistrarAjax(int idSocio)
    {
        if (idSocio < 1001) return Json(new { ok = false, msg = idSocio == 1000 ? "La clave 1000 es para visitas de d\u00eda, use el formulario correspondiente." : "Clave inv\u00e1lida." });
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

        var vencido     = s.Vencimiento.HasValue && s.Vencimiento.Value < DateTime.Now;
        var sinMem      = !s.Vencimiento.HasValue;
        int? diasRest   = s.Vencimiento.HasValue && !vencido
            ? (int)Math.Ceiling((s.Vencimiento.Value - DateTime.Now).TotalDays) : null;
        var porVencer   = diasRest.HasValue && diasRest.Value <= GymWeb.Helpers.AppSettings.DiasAviso;
        return Json(new {
            ok          = true,
            idSocio     = s.IdSocio,
            nombre      = $"{s.NombreSocio} {s.Paterno} {s.Materno}".Trim(),
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
        if (idSocio < 1001) return Json(new { ok = false, msg = "Clave inválida." });
        var s = await _db.Vwultimamembresiadetallada
            .FirstOrDefaultAsync(v => v.IdSocio == idSocio);
        if (s == null) return Json(new { ok = false, msg = "Socio no encontrado." });

        // Registrar sin verificar duplicado — el recepcionista ya confirmó
        _db.Registros.Add(new Registro { IdSocio = idSocio, FechaCreacion = DateTime.Now });
        await _db.SaveChangesAsync();

        var vencido     = s.Vencimiento.HasValue && s.Vencimiento.Value < DateTime.Now;
        var sinMem      = !s.Vencimiento.HasValue;
        int? diasRest   = s.Vencimiento.HasValue && !vencido
            ? (int)Math.Ceiling((s.Vencimiento.Value - DateTime.Now).TotalDays) : null;
        var porVencer   = diasRest.HasValue && diasRest.Value <= GymWeb.Helpers.AppSettings.DiasAviso;
        return Json(new {
            ok          = true,
            idSocio     = s.IdSocio,
            nombre      = $"{s.NombreSocio} {s.Paterno} {s.Materno}".Trim(),
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
        if (string.IsNullOrWhiteSpace(nombreVisita))
        {
            TempData["Error"] = "Ingrese el nombre del visitante.";
            return RedirectToAction(nameof(Index));
        }
        // Solo se permite el número 1000 o un nombre (texto no numérico)
        if (long.TryParse(nombreVisita.Trim(), out long numVisita) && numVisita != 1000)
        {
            TempData["Error"] = "Solo se permite el número 1000 o el nombre de la persona.";
            return RedirectToAction(nameof(Index));
        }
        // Precio de visita de día = membresía con duración de 1 día
        var precio = await _db.Membresia
            .Where(m => m.Meses == 1 && m.IdEstado == 1)
            .Select(m => m.Precio)
            .FirstOrDefaultAsync() ?? 0m;
        _db.Registros.Add(new Registro
        {
            IdSocio = null,
            NombreVisita = nombreVisita.Trim(),
            PrecioVisita = precio,
            FechaCreacion = DateTime.Now
        });
        await _db.SaveChangesAsync();
        var precioStr = precio > 0 ? $" — ${precio:N2}" : "";
        TempData["Success"] = $"Visita de día registrada: {nombreVisita.Trim()}{precioStr}";
        return RedirectToAction(nameof(Index));
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
