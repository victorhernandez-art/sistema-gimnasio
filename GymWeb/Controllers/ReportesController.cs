using GymWeb.Helpers;
using GymWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GymWeb.Controllers;

public class ReportesController : AuthController
{
    private readonly GymContext _db;
    private readonly IWebHostEnvironment _env;

    public ReportesController(GymContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    public IActionResult Index()
    {
        ViewData["Title"] = "Reportes";
        return View();
    }

    public async Task<IActionResult> Visitas(DateTime? desde, DateTime? hasta)
    {
        ViewData["Title"] = "Reporte de Visitas";
        DateTime fDesde = (desde ?? DateTime.Today.AddMonths(-1)).Date;
        DateTime fHasta = (hasta ?? DateTime.Today).Date.AddDays(1);
        ViewBag.Desde = (desde ?? DateTime.Today.AddMonths(-1)).ToString("yyyy-MM-dd");
        ViewBag.Hasta = (hasta ?? DateTime.Today).ToString("yyyy-MM-dd");
        var list = await _db.Registros
            .Include(r => r.IdSocioNavigation)
            .Where(r => r.FechaCreacion.HasValue &&
                        r.FechaCreacion.Value >= fDesde &&
                        r.FechaCreacion.Value < fHasta)
            .OrderByDescending(r => r.FechaCreacion)
            .ToListAsync();
        return View(list);
    }

    public async Task<IActionResult> CorteDia(DateTime? fecha, string? mes, string modo = "dia")
    {
        ViewData["Title"] = "Corte del Período";
        string mesVal = mes ?? DateTime.Today.ToString("yyyy-MM");
        DateTime desde, hasta;
        if (modo == "mes")
        {
            if (DateTime.TryParseExact(mesVal + "-01", "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var primerDia))
            { desde = primerDia; hasta = primerDia.AddMonths(1).AddDays(-1); }
            else { desde = hasta = DateTime.Today; }
        }
        else { modo = "dia"; desde = hasta = fecha ?? DateTime.Today; }

        var esCultura = new System.Globalization.CultureInfo("es-MX");
        ViewBag.Modo         = modo;
        ViewBag.Mes          = mesVal;
        ViewBag.Fecha        = desde.ToString("yyyy-MM-dd");
        ViewBag.FechaDisplay = modo == "mes" ? desde.ToString("MMMM yyyy", esCultura) : desde.ToString("dd/MM/yyyy");

        var config = await _db.Configuracions.FirstOrDefaultAsync();
        var precioVisita = await _db.Membresia
            .Where(m => m.Meses == 1 && m.IdEstado == 1)
            .Select(m => m.Precio)
            .FirstOrDefaultAsync() ?? config?.PrecioVisita ?? 0m;
        ViewBag.PrecioConfig = modo == "dia" ? precioVisita : 0m;

        DateTime fDesde = desde.Date;
        DateTime fHasta = hasta.Date.AddDays(1);

        var ventas = await _db.Detallesalida
            .Include(d => d.IdProductoNavigation)
            .Include(d => d.IdSalidaNavigation)
            .Where(d => d.IdSalidaNavigation!.FechaCreacion.HasValue &&
                        d.IdSalidaNavigation.FechaCreacion.Value >= fDesde &&
                        d.IdSalidaNavigation.FechaCreacion.Value < fHasta)
            .OrderBy(d => d.IdSalidaNavigation!.FechaCreacion)
            .ToListAsync();

        var visitas = await _db.Registros
            .Where(r => r.IdSocio == null &&
                        r.FechaCreacion.HasValue &&
                        r.FechaCreacion.Value >= fDesde &&
                        r.FechaCreacion.Value < fHasta)
            .OrderBy(r => r.FechaCreacion)
            .ToListAsync();

        var pagos = await _db.Pagos
            .Include(p => p.IdSocioNavigation)
            .Include(p => p.IdMembresiaNavigation)
            .Where(p => p.FechaCreacion.HasValue &&
                        p.FechaCreacion.Value >= fDesde &&
                        p.FechaCreacion.Value < fHasta)
            .OrderBy(p => p.FechaCreacion)
            .ToListAsync();

        ViewBag.Ventas  = ventas;
        ViewBag.Visitas = visitas;
        ViewBag.Pagos   = pagos;
        return View();
    }

    public async Task<IActionResult> ExportarCorte(DateTime? fecha, string? mes, string modo = "dia", string formato = "excel")
    {
        string mesVal = mes ?? DateTime.Today.ToString("yyyy-MM");
        DateTime desde, hasta;
        if (modo == "mes")
        {
            if (DateTime.TryParseExact(mesVal + "-01", "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var primerDia))
            { desde = primerDia; hasta = primerDia.AddMonths(1).AddDays(-1); }
            else { desde = hasta = DateTime.Today; }
        }
        else { desde = hasta = fecha ?? DateTime.Today; }

        DateTime fDesde = desde.Date;
        DateTime fHasta = hasta.Date.AddDays(1);

        var ventas = await _db.Detallesalida
            .Include(d => d.IdProductoNavigation)
            .Include(d => d.IdSalidaNavigation)
            .Where(d => d.IdSalidaNavigation!.FechaCreacion.HasValue &&
                        d.IdSalidaNavigation.FechaCreacion.Value >= fDesde &&
                        d.IdSalidaNavigation.FechaCreacion.Value < fHasta)
            .OrderBy(d => d.IdSalidaNavigation!.FechaCreacion).ToListAsync();

        var visitas = await _db.Registros
            .Where(r => r.IdSocio == null &&
                        r.FechaCreacion.HasValue &&
                        r.FechaCreacion.Value >= fDesde &&
                        r.FechaCreacion.Value < fHasta)
            .OrderBy(r => r.FechaCreacion).ToListAsync();

        var pagos = await _db.Pagos
            .Include(p => p.IdSocioNavigation)
            .Include(p => p.IdMembresiaNavigation)
            .Where(p => p.FechaCreacion.HasValue &&
                        p.FechaCreacion.Value >= fDesde &&
                        p.FechaCreacion.Value < fHasta)
            .OrderBy(p => p.FechaCreacion).ToListAsync();

        var esCultura = new System.Globalization.CultureInfo("es-MX");
        string periodo = modo == "mes"
            ? desde.ToString("MMMM_yyyy", esCultura)
            : desde.ToString("yyyyMMdd");
        string periodoLabel = modo == "mes"
            ? desde.ToString("MMMM yyyy", esCultura)
            : desde.ToString("dd/MM/yyyy");
        string tipoCorte = modo == "mes" ? "Mes" : "Día";
        decimal totVisitas = visitas.Sum(v => v.PrecioVisita);
        decimal totVentasContado = ventas.Where(d => d.IdSalidaNavigation?.EsCredito != true).Sum(d => (d.PrecioUnitario ?? 0) * d.Cantidad);
        decimal totVentasCredito = ventas.Where(d => d.IdSalidaNavigation?.EsCredito == true).Sum(d => (d.PrecioUnitario ?? 0) * d.Cantidad);
        decimal totPagos   = pagos.Sum(p => p.Monto);
        decimal totGeneral = totVisitas + totVentasContado + totPagos;

        // Si solicitan formato CSV plano tradicional
        if (string.Equals(formato, "csv", StringComparison.OrdinalIgnoreCase))
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Corte del " + tipoCorte + ": " + periodoLabel);
            sb.AppendLine();
            sb.AppendLine("VISITAS DE DÍA");
            sb.AppendLine("Fecha,Hora,Visitante,Monto");
            foreach (var v in visitas)
            {
                string vf = v.FechaCreacion?.ToString("dd/MM/yyyy") ?? "";
                string vh = v.FechaCreacion?.ToString("HH:mm") ?? "";
                sb.AppendLine(vf + "," + vh + "," + CsvEsc(v.NombreVisita) + "," + v.PrecioVisita.ToString("N2"));
            }
            sb.AppendLine("SUBTOTAL," + totVisitas.ToString("N2"));
            sb.AppendLine();
            sb.AppendLine("VENTAS DE PRODUCTOS");
            sb.AppendLine("Fecha,Hora,Producto,Método,Cantidad,Total,Ganancia");
            foreach (var d in ventas)
            {
                string vf   = d.IdSalidaNavigation?.FechaCreacion?.ToString("dd/MM/yyyy") ?? "";
                string vh   = d.IdSalidaNavigation?.FechaCreacion?.ToString("HH:mm") ?? "";
                bool esCredito = d.IdSalidaNavigation?.EsCredito == true;
                string metodo = esCredito ? "A Crédito" : "Contado";
                decimal tot = (d.PrecioUnitario ?? 0) * d.Cantidad;
                decimal gan = ((d.PrecioUnitario ?? 0) - (d.IdProductoNavigation?.Costo ?? 0)) * d.Cantidad;
                sb.AppendLine(vf + "," + vh + "," + CsvEsc(d.IdProductoNavigation?.Nombre) + "," + metodo + "," + d.Cantidad + "," + tot.ToString("N2") + "," + gan.ToString("N2"));
            }
            sb.AppendLine("SUBTOTAL EN CAJA (CONTADO)," + totVentasContado.ToString("N2"));
            if (totVentasCredito > 0)
            {
                sb.AppendLine("SUBTOTAL A CRÉDITO (NO SUMA A CAJA)," + totVentasCredito.ToString("N2"));
            }
            sb.AppendLine();
            sb.AppendLine("PAGOS DE MEMBRESÍAS");
            sb.AppendLine("Fecha,Hora,Socio,Plan,Método,Monto");
            foreach (var p in pagos)
            {
                string pf     = p.FechaCreacion?.ToString("dd/MM/yyyy") ?? "";
                string ph     = p.FechaCreacion?.ToString("HH:mm") ?? "";
                string nombre = p.IdSocioNavigation != null
                    ? p.IdSocioNavigation.Nombre + " " + p.IdSocioNavigation.Paterno : "";
                string plan   = p.IdMembresiaNavigation?.Nombre ?? (!string.IsNullOrWhiteSpace(p.Notas) ? p.Notas : "Abono / Cobro");
                sb.AppendLine(pf + "," + ph + "," + CsvEsc(nombre) + "," + CsvEsc(plan) + "," + CsvEsc(p.MetodoPago) + "," + p.Monto.ToString("N2"));
            }
            sb.AppendLine("SUBTOTAL," + totPagos.ToString("N2"));
            sb.AppendLine();
            sb.AppendLine("TOTAL GENERAL EN CAJA," + totGeneral.ToString("N2"));

            var bytes = new System.Text.UTF8Encoding(true).GetBytes(sb.ToString());
            return File(bytes, "text/csv", "corte_" + periodo + ".csv");
        }

        // ========================================================
        // FORMATO PROFESIONAL EXCEL (.xlsx) CON LOGO NATIVO
        // ========================================================
        var cfg = await _db.Configuracions.FirstOrDefaultAsync();
        string gymNombre = !string.IsNullOrWhiteSpace(cfg?.NombreGimnacio) ? cfg.NombreGimnacio : AppSettings.GymNombre;
        byte[]? logoBytes = await ExcelReportHelper.GetLogoBytesAsync(_db, _env);

        var xlsxBytes = ExcelReportHelper.GenerarCorteXlsx(visitas, ventas, pagos, gymNombre, tipoCorte, periodoLabel, logoBytes);
        return File(xlsxBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"corte_{periodo}.xlsx");
    }

    private static string CsvEsc(string? s) => s == null ? "" : "\"" + s.Replace("\"", "\"\"") + "\"";

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AplicarPrecio(DateTime fecha, decimal precio)
    {
        if (precio <= 0)
        {
            TempData["Error"] = "El precio en Configuración es $0.00. Primero configure el precio de visita de día.";
            return RedirectToAction(nameof(CorteDia), new { fecha = fecha.ToString("yyyy-MM-dd") });
        }
        DateTime fDia = fecha.Date;
        DateTime fDiaSig = fecha.Date.AddDays(1);
        var visitas = await _db.Registros
            .Where(r => r.IdSocio == null &&
                        r.FechaCreacion.HasValue &&
                        r.FechaCreacion.Value >= fDia &&
                        r.FechaCreacion.Value < fDiaSig &&
                        r.PrecioVisita == 0)
            .ToListAsync();
        if (!visitas.Any())
        {
            TempData["Info"] = "Todas las visitas del día ya tienen precio asignado.";
            return RedirectToAction(nameof(CorteDia), new { fecha = fecha.ToString("yyyy-MM-dd") });
        }
        foreach (var v in visitas) v.PrecioVisita = precio;
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Se actualizaron {visitas.Count} visita(s) a ${precio:N2} c/u — Total: ${visitas.Count * precio:N2}";
        return RedirectToAction(nameof(CorteDia), new { fecha = fecha.ToString("yyyy-MM-dd") });
    }

    public async Task<IActionResult> Asistencia(int? idSocio, DateTime? desde, DateTime? hasta)
    {
        ViewData["Title"] = "Asistencia por Socio";
        DateTime dtDesde = (desde ?? DateTime.Today.AddMonths(-1)).Date;
        DateTime dtHasta = (hasta ?? DateTime.Today).Date.AddDays(1);

        ViewBag.Desde = (desde ?? DateTime.Today.AddMonths(-1)).ToString("yyyy-MM-dd");
        ViewBag.Hasta = (hasta ?? DateTime.Today).ToString("yyyy-MM-dd");

        var socios = await _db.Socios.Where(s => s.IdEstado == 1)
            .OrderBy(s => s.Paterno).ThenBy(s => s.Nombre).ToListAsync();
        ViewBag.Socios = socios;
        ViewBag.IdSocio = idSocio;

        List<GymWeb.Models.Registro> registros = new();
        GymWeb.Models.Socio? socioSel = null;
        if (idSocio.HasValue)
        {
            socioSel = await _db.Socios.FindAsync(idSocio.Value);
            registros = await _db.Registros
                .Where(r => r.IdSocio == idSocio.Value &&
                            r.FechaCreacion.HasValue &&
                            r.FechaCreacion.Value >= dtDesde &&
                            r.FechaCreacion.Value < dtHasta)
                .OrderByDescending(r => r.FechaCreacion)
                .ToListAsync();
        }
        ViewBag.SocioSel = socioSel;
        return View(registros);
    }
}
