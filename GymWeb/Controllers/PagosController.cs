using GymWeb.Helpers;
using GymWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GymWeb.Controllers;

public class PagosController : AuthController
{
    private readonly GymContext _db;
    private readonly IWebHostEnvironment _env;

    public PagosController(GymContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    public async Task<IActionResult> Index(string? search, string? metodo, string? desde, string? hasta)
    {
        ViewData["Title"] = "Pagos";

        var query = _db.Pagos
            .Include(p => p.IdSocioNavigation)
            .Include(p => p.IdMembresiaNavigation)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(p =>
                (p.IdSocioNavigation!.Nombre + " " + p.IdSocioNavigation.Paterno).ToLower().Contains(s));
        }
        if (!string.IsNullOrWhiteSpace(metodo))
            query = query.Where(p => p.MetodoPago == metodo);
        if (DateOnly.TryParse(desde, out var d))
            query = query.Where(p => p.FechaPago >= d);
        if (DateOnly.TryParse(hasta, out var h))
            query = query.Where(p => p.FechaPago <= h);

        var pagos = await query.OrderByDescending(p => p.FechaPago).ThenByDescending(p => p.IdPago).ToListAsync();

        // Stats
        var hoy = DateOnly.FromDateTime(DateTime.Today);
        var inicioMes = new DateOnly(hoy.Year, hoy.Month, 1);
        ViewBag.IngresosMes  = await _db.Pagos.Where(p => p.FechaPago >= inicioMes).SumAsync(p => (decimal?)p.Monto) ?? 0;
        ViewBag.TotalPagos   = await _db.Pagos.CountAsync();
        ViewBag.PagosMes     = await _db.Pagos.CountAsync(p => p.FechaPago >= inicioMes);

        ViewBag.Socios     = await _db.Socios.Where(s => s.IdEstado == 1).OrderBy(s => s.Paterno).ThenBy(s => s.Nombre).ToListAsync();
        ViewBag.Membresias = await _db.Membresia.Where(m => m.IdEstado == 1).OrderBy(m => m.Nombre).ToListAsync();

        var config = await _db.Configuracions.FirstOrDefaultAsync();
        ViewBag.GymNombre    = !string.IsNullOrWhiteSpace(config?.NombreGimnacio) ? config.NombreGimnacio : AppSettings.GymNombre;
        ViewBag.GymDomicilio = !string.IsNullOrWhiteSpace(config?.Domicilio) ? config.Domicilio : AppSettings.GymDomicilio;
        ViewBag.GymTelefono  = !string.IsNullOrWhiteSpace(config?.Telefono) ? config.Telefono : AppSettings.GymTelefono;
        ViewBag.GymPieTicket = !string.IsNullOrWhiteSpace(config?.Mensaje) ? config.Mensaje : AppSettings.GymPieTicket;

        return View(pagos);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int idSocio, int? idMembresia, decimal monto,
        string metodoPago, string fechaPago, string? notas)
    {
        if (monto <= 0)
        {
            TempData["Error"] = "El monto debe ser mayor a $0.";
            return RedirectToAction(nameof(Index));
        }
        if (!DateOnly.TryParse(fechaPago, out var fecha))
            fecha = DateOnly.FromDateTime(DateTime.Today);

        // Advertir si el socio no tiene membresía activa
        var hoyDt = DateTime.Today;
        var ultimaMem = await _db.Vwsociomembresias
            .Where(v => v.IdSocio == idSocio && v.IdEstado == 1)
            .OrderByDescending(v => v.FechaCreacion)
            .FirstOrDefaultAsync();
        bool sinMembresia = ultimaMem == null;
        bool memVencida   = ultimaMem != null && ultimaMem.Vencimiento.HasValue && ultimaMem.Vencimiento.Value < hoyDt;
        if (sinMembresia)
            TempData["Warning"] = "⚠️ El socio no tiene membresía asignada. El pago se registró, pero considera asignarle una.";
        else if (memVencida)
            TempData["Warning"] = "⚠️ La membresía del socio está vencida desde " + ultimaMem!.Vencimiento!.Value.ToString("dd/MM/yyyy") + ". El pago se registró.";

        var pago = new Pago
        {
            IdSocio     = idSocio,
            IdMembresia = idMembresia,
            Monto       = monto,
            MetodoPago  = metodoPago,
            FechaPago   = fecha,
            Notas       = notas,
            IdEstado    = 1,
            FechaCreacion  = DateTime.Now,
            IdUsuarioCreo  = UsuarioId
        };
        _db.Pagos.Add(pago);
        await _db.SaveChangesAsync();

        TempData["Success"] = "Pago registrado correctamente.";
        TempData["UltimoPagoId"] = pago.IdPago;
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> GetDetallePago(int idPago)
    {
        var pago = await _db.Pagos
            .Include(p => p.IdSocioNavigation)
            .Include(p => p.IdMembresiaNavigation)
            .Include(p => p.IdUsuarioCreoNavigation)
            .FirstOrDefaultAsync(p => p.IdPago == idPago);

        if (pago == null)
            return NotFound(new { ok = false, msg = "Pago no encontrado." });

        var socio = pago.IdSocioNavigation;
        string nombreSocio = socio != null ? $"{socio.Nombre} {socio.Paterno} {socio.Materno}".Trim() : "Socio no especificado";

        // Obtener última vigencia de membresía si existe
        var memActiva = await _db.Vwsociomembresias
            .Where(v => v.IdSocio == pago.IdSocio && v.IdEstado == 1)
            .OrderByDescending(v => v.FechaCreacion)
            .FirstOrDefaultAsync();

        string vigenciaTexto = memActiva?.Vencimiento != null 
            ? memActiva.Vencimiento.Value.ToString("dd/MM/yyyy") 
            : "";

        var config = await _db.Configuracions.FirstOrDefaultAsync();

        return Json(new
        {
            ok = true,
            idPago = pago.IdPago,
            folio = $"#PAG-{pago.IdPago:D6}",
            fecha = (pago.FechaCreacion ?? pago.FechaPago.ToDateTime(TimeOnly.MinValue)).ToString("dd/MM/yyyy HH:mm"),
            fechaPago = pago.FechaPago.ToString("dd/MM/yyyy"),
            socio = nombreSocio,
            socioId = pago.IdSocio,
            socioTelefono = socio?.Telefono ?? "",
            telefono = socio?.Telefono ?? "",
            plan = pago.IdMembresiaNavigation?.Nombre ?? (pago.Notas != null && pago.Notas.Contains("Abono") ? "Abono a Deuda / Liquidación" : "Cuota / Membresía"),
            vigencia = vigenciaTexto,
            metodoPago = pago.MetodoPago ?? "Efectivo",
            monto = pago.Monto,
            notas = pago.Notas ?? "",
            cajero = pago.IdUsuarioCreoNavigation?.Nombre ?? "Recepción",
            gymNombre = !string.IsNullOrWhiteSpace(config?.NombreGimnacio) ? config.NombreGimnacio : AppSettings.GymNombre,
            gymDomicilio = !string.IsNullOrWhiteSpace(config?.Domicilio) ? config.Domicilio : AppSettings.GymDomicilio,
            gymTelefono = !string.IsNullOrWhiteSpace(config?.Telefono) ? config.Telefono : AppSettings.GymTelefono,
            gymPieTicket = !string.IsNullOrWhiteSpace(config?.Mensaje) ? config.Mensaje : AppSettings.GymPieTicket,
            gymLogo = AppSettings.HasCustomLogo ? "/img/logo-custom.png" : "/img/gym.jpeg"
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var pago = await _db.Pagos.FindAsync(id);
        if (pago == null) return NotFound();
        _db.Pagos.Remove(pago);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Pago eliminado.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> ExportCsv(string? search, string? metodo, string? desde, string? hasta, string formato = "excel")
    {
        var query = _db.Pagos.Include(p => p.IdSocioNavigation).Include(p => p.IdMembresiaNavigation).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(p => (p.IdSocioNavigation!.Nombre + " " + p.IdSocioNavigation.Paterno).ToLower().Contains(s));
        }
        if (!string.IsNullOrWhiteSpace(metodo)) query = query.Where(p => p.MetodoPago == metodo);
        if (DateOnly.TryParse(desde, out var d)) query = query.Where(p => p.FechaPago >= d);
        if (DateOnly.TryParse(hasta, out var h)) query = query.Where(p => p.FechaPago <= h);

        var pagos = await query.OrderByDescending(p => p.FechaPago).ToListAsync();

        if (formato.Equals("csv", StringComparison.OrdinalIgnoreCase))
        {
            var sbCsv = new System.Text.StringBuilder();
            sbCsv.AppendLine("Fecha,Socio,Plan,Metodo,Monto,Notas");
            foreach (var p in pagos)
            {
                var nombre = $"{p.IdSocioNavigation?.Nombre} {p.IdSocioNavigation?.Paterno}".Trim();
                var plan = p.IdMembresiaNavigation?.Nombre ?? "—";
                sbCsv.AppendLine($"{p.FechaPago:dd/MM/yyyy},\"{nombre}\",\"{plan}\",{p.MetodoPago},{p.Monto:N2},\"{p.Notas ?? ""}\"");
            }

            var bytesCsv = System.Text.Encoding.UTF8.GetBytes(sbCsv.ToString());
            return File(bytesCsv, "text/csv", $"pagos_{DateTime.Today:yyyyMMdd}.csv");
        }

        // Formato Excel Nativo (.xlsx) con Logo incrustado directamente
        var cfg = await _db.Configuracions.FirstOrDefaultAsync();
        var gymNombre = !string.IsNullOrWhiteSpace(cfg?.NombreGimnacio) ? cfg.NombreGimnacio : AppSettings.GymNombre;
        var logoBytes = await ExcelReportHelper.GetLogoBytesAsync(_db, _env);

        var periodoTexto = "Todos los registros";
        if (!string.IsNullOrEmpty(desde) && !string.IsNullOrEmpty(hasta))
            periodoTexto = $"Del {desde} al {hasta}";
        else if (!string.IsNullOrEmpty(desde))
            periodoTexto = $"Desde el {desde}";
        else if (!string.IsNullOrEmpty(hasta))
            periodoTexto = $"Hasta el {hasta}";

        if (!string.IsNullOrEmpty(metodo))
            periodoTexto += $" | Método: {metodo}";

        var xlsxBytes = ExcelReportHelper.GenerarPagosXlsx(pagos, gymNombre, periodoTexto, logoBytes);
        return File(xlsxBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"pagos_{DateTime.Today:yyyyMMdd}.xlsx");
    }
}
