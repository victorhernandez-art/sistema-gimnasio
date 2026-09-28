using GymWeb.Helpers;
using GymWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GymWeb.Controllers;

public class SociosController : AuthController
{
    private readonly GymContext _db;
    private readonly IWebHostEnvironment _env;

    public SociosController(GymContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Socios";
        var socios = await _db.Socios
            .Include(s => s.IdEstadoNavigation)
            .Include(s => s.SocioHuella)
            .OrderByDescending(s => s.FechaCreacion)
            .ToListAsync();
        return View(socios);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        ViewData["Title"] = "Nuevo Socio";
        ViewBag.Membresias = await _db.Membresia.Where(m => m.IdEstado == 1).OrderBy(m => m.Nombre).ToListAsync();
        return View(new Socio { FechaCreacion = DateTime.Now, IdEstado = 1 });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Socio socio, string? FotoBase64, int? IdMembresia, DateTime? FechaInicioMembresia)
    {
        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Nuevo Socio";
            ViewBag.Membresias = await _db.Membresia.Where(m => m.IdEstado == 1).OrderBy(m => m.Nombre).ToListAsync();
            return View(socio);
        }
        socio.FechaCreacion = DateTime.Now;
        socio.IdUsuarioCreo = UsuarioId;
        socio.IdEstado = 1;
        if (!string.IsNullOrEmpty(FotoBase64))
        {
            var b64 = FotoBase64.Contains(',') ? FotoBase64.Split(',')[1] : FotoBase64;
            socio.Foto = Convert.FromBase64String(b64);
        }
        _db.Socios.Add(socio);
        await _db.SaveChangesAsync();
        if (IdMembresia.HasValue && FechaInicioMembresia.HasValue)
        {
            var mem = await _db.Membresia.FindAsync(IdMembresia.Value);
            _db.Sociomembresia.Add(new Sociomembresium
            {
                IdSocio = socio.IdSocio,
                IdMembresia = IdMembresia.Value,
                FechaInicioMembresia = FechaInicioMembresia.Value,
                Precio = mem?.Precio,
                IdEstado = 1,
                FechaCreacion = DateTime.Now,
                IdUsuarioCreo = UsuarioId
            });
            await _db.SaveChangesAsync();
        }
        TempData["Success"] = "Socio registrado correctamente.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        ViewData["Title"] = "Editar Socio";
        var socio = await _db.Socios.FindAsync(id);
        if (socio == null) return NotFound();
        return View(socio);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Socio socio, string? FotoBase64)
    {
        if (!ModelState.IsValid) { ViewData["Title"] = "Editar Socio"; return View(socio); }
        var existing = await _db.Socios.FindAsync(socio.IdSocio);
        if (existing == null) return NotFound();
        existing.Nombre = socio.Nombre;
        existing.Paterno = socio.Paterno;
        existing.Materno = socio.Materno;
        existing.Telefono = socio.Telefono;
        existing.Observaciones = socio.Observaciones;
        if (!string.IsNullOrEmpty(FotoBase64))
        {
            var b64 = FotoBase64.Contains(',') ? FotoBase64.Split(',')[1] : FotoBase64;
            existing.Foto = Convert.FromBase64String(b64);
        }
        await _db.SaveChangesAsync();
        TempData["Success"] = "Socio actualizado.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var socio = await _db.Socios.FindAsync(id);
        if (socio != null) { socio.IdEstado = 2; await _db.SaveChangesAsync(); }
        TempData["Success"] = "Socio inactivado.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PermanentDelete(int id)
    {
        var socio = await _db.Socios.FindAsync(id);
        if (socio == null) return RedirectToAction(nameof(Index));

        try
        {
            // Eliminar registros relacionados antes del socio (FK)
            _db.Registros.RemoveRange(_db.Registros.Where(r => r.IdSocio == id));
            _db.Sociomembresia.RemoveRange(_db.Sociomembresia.Where(m => m.IdSocio == id));
            _db.Pagos.RemoveRange(_db.Pagos.Where(p => p.IdSocio == id));
            _db.SocioHuellas.RemoveRange(_db.SocioHuellas.Where(h => h.IdSocio == id));
            // Tabla visita no tiene modelo EF — borrar con SQL raw
            await _db.Database.ExecuteSqlRawAsync("DELETE FROM visita WHERE idSocio = {0}", id);

            _db.Socios.Remove(socio);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Socio eliminado permanentemente.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = "No se pudo eliminar: " + ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> GetFoto(int id)
    {
        var socio = await _db.Socios.FindAsync(id);
        if (socio?.Foto == null || socio.Foto.Length == 0) return NotFound();
        return File(socio.Foto, "image/jpeg");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EliminarFoto(int id)
    {
        var socio = await _db.Socios.FindAsync(id);
        if (socio != null)
        {
            socio.Foto = null;
            await _db.SaveChangesAsync();
            TempData["Success"] = "Foto eliminada correctamente.";
        }
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activate(int id)
    {
        var socio = await _db.Socios.FindAsync(id);
        if (socio != null) { socio.IdEstado = 1; await _db.SaveChangesAsync(); }
        TempData["Success"] = "Socio activado correctamente.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Membresia(int id)
    {
        ViewData["Title"] = "Membresía del Socio";
        var socio = await _db.Socios.FindAsync(id);
        if (socio == null) return NotFound();
        ViewBag.Socio = socio;
        ViewBag.Membresias = await _db.Membresia.Where(m => m.IdEstado == 1).ToListAsync();
        var historial = await _db.Vwsociomembresias
            .Where(v => v.IdSocio == id)
            .OrderByDescending(v => v.FechaCreacion)
            .ToListAsync();
        return View(historial);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AsignarMembresia(int idSocio, int idMembresia, DateTime fechaInicio)
    {
        var mem = await _db.Membresia.FindAsync(idMembresia);
        if (mem == null) return NotFound();

        // Advertir si ya tiene membresía activa y vigente
        var hoyDt = DateTime.Today;
        var activa = await _db.Vwsociomembresias
            .Where(v => v.IdSocio == idSocio && v.IdEstado == 1)
            .OrderByDescending(v => v.FechaCreacion)
            .FirstOrDefaultAsync();
        if (activa != null && activa.Vencimiento.HasValue && activa.Vencimiento.Value >= hoyDt)
            TempData["Warning"] = $"⚠️ El socio ya tiene la membresía \u2018{activa.NombreMembresia}\u2019 activa hasta {activa.Vencimiento.Value:dd/MM/yyyy}. Se agregó la nueva de todas formas.";

        _db.Sociomembresia.Add(new Sociomembresium
        {
            IdSocio = idSocio,
            IdMembresia = idMembresia,
            Precio = mem.Precio,
            FechaInicioMembresia = fechaInicio,
            FechaCreacion = DateTime.Now,
            IdUsuarioCreo = UsuarioId,
            IdEstado = 1
        });
        await _db.SaveChangesAsync();
        TempData["Success"] = "Membresía asignada.";
        return RedirectToAction(nameof(Membresia), new { id = idSocio });
    }

    public async Task<IActionResult> PagosSocio(int id)
    {
        var socio = await _db.Socios.FindAsync(id);
        if (socio == null) return NotFound();
        ViewData["Title"] = $"Pagos de {socio.Nombre} {socio.Paterno}";
        ViewBag.Socio = socio;
        var pagos = await _db.Pagos
            .Include(p => p.IdMembresiaNavigation)
            .Where(p => p.IdSocio == id)
            .OrderByDescending(p => p.FechaPago)
            .ToListAsync();
        return View(pagos);
    }

    public async Task<IActionResult> ExportCsv(string formato = "excel")
    {
        var socios = await _db.Socios
            .Include(s => s.IdEstadoNavigation)
            .OrderBy(s => s.Paterno).ThenBy(s => s.Nombre)
            .ToListAsync();

        if (formato.Equals("csv", StringComparison.OrdinalIgnoreCase))
        {
            var sbCsv = new System.Text.StringBuilder();
            sbCsv.AppendLine("Clave,Nombre,Paterno,Materno,Telefono,Estado,FechaRegistro");
            foreach (var s in socios)
            {
                var estado = s.IdEstado == 1 ? "Activo" : "Inactivo";
                sbCsv.AppendLine($"{s.IdSocio},\"{s.Nombre}\",\"{s.Paterno}\",\"{s.Materno}\",\"{s.Telefono}\",{estado},{s.FechaCreacion:dd/MM/yyyy}");
            }
            var bytesCsv = System.Text.Encoding.UTF8.GetBytes(sbCsv.ToString());
            return File(bytesCsv, "text/csv", $"socios_{DateTime.Today:yyyyMMdd}.csv");
        }

        // Formato Excel Nativo (.xlsx) con Logo incrustado directamente
        var cfg = await _db.Configuracions.FirstOrDefaultAsync();
        var gymNombre = !string.IsNullOrWhiteSpace(cfg?.NombreGimnacio) ? cfg.NombreGimnacio : AppSettings.GymNombre;
        var logoBytes = await ExcelReportHelper.GetLogoBytesAsync(_db, _env);

        var xlsxBytes = ExcelReportHelper.GenerarSociosXlsx(socios, gymNombre, logoBytes);
        return File(xlsxBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"socios_{DateTime.Today:yyyyMMdd}.xlsx");
    }
}
