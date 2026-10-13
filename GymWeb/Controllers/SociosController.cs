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

        // ── 1. Deudas de productos (Salidas a crédito fiadas con saldo pendiente) ──
        var deudasProductos = await _db.Salida
            .Where(s => s.IdEstado == 1 && s.EsCredito && s.SaldoPendiente > 0 && s.IdSocio != null)
            .GroupBy(s => s.IdSocio!.Value)
            .Select(g => new { IdSocio = g.Key, Total = g.Sum(x => x.SaldoPendiente) })
            .ToDictionaryAsync(x => x.IdSocio, x => x.Total);

        // ── 2. Membresías recientes y activas por socio (evalúa únicamente el ciclo actual/reciente) ──
        var hoyDt = DateTime.Today;
        var membresiasRecientes = await _db.Sociomembresia
            .Include(sm => sm.IdMembresiaNavigation)
            .Where(sm => sm.IdEstado == 1 && sm.IdSocio != null)
            .OrderByDescending(sm => sm.FechaInicioMembresia ?? sm.FechaCreacion)
            .ToListAsync();

        var ultimaMembresiaPorSocio = membresiasRecientes
            .GroupBy(sm => sm.IdSocio!.Value)
            .ToDictionary(g => g.Key, g => g.First());

        var todosPagos = await _db.Pagos
            .Where(p => p.IdEstado == 1 && (p.IdMembresia != null || p.Notas == null || !p.Notas.Contains("[TIENDA_CREDITO]")))
            .ToListAsync();

        var pagosPorSocio = todosPagos
            .GroupBy(p => p.IdSocio)
            .ToDictionary(g => g.Key, g => g.ToList());

        var deudasDict = new Dictionary<int, SocioDeudorItemDto>();
        foreach (var s in socios)
        {
            decimal dProd = deudasProductos.TryGetValue(s.IdSocio, out var dp) ? dp : 0m;
            decimal dMem = 0m;

            if (ultimaMembresiaPorSocio.TryGetValue(s.IdSocio, out var memReciente))
            {
                decimal precioReciente = memReciente.Precio ?? 0m;
                var pagosSocio = pagosPorSocio.TryGetValue(s.IdSocio, out var lp) ? lp : new List<Pago>();

                // Fecha de inicio y fecha de registro de la membresía
                var fInicio = (memReciente.FechaInicioMembresia ?? memReciente.FechaCreacion ?? DateTime.Today).Date;
                var fRegistro = (memReciente.FechaCreacion ?? fInicio).Date;
                var fMinima = DateOnly.FromDateTime(fInicio < fRegistro ? fInicio.AddDays(-7) : fRegistro.AddDays(-7));

                // 1. Pagos explícitamente dirigidos a esta membresía o realizados en el ciclo
                decimal totalPagadoCiclo = pagosSocio
                    .Where(p => (p.IdMembresia.HasValue && p.IdMembresia == memReciente.IdMembresia) || p.FechaPago >= fMinima)
                    .Sum(p => p.Monto);

                // 2. Pagos generales / abonos registrados desde la fecha de registro de esta membresía
                if (totalPagadoCiclo < precioReciente)
                {
                    var fCreacionOnly = DateOnly.FromDateTime(fRegistro.AddDays(-1));
                    decimal pagosDesdeCreacion = pagosSocio
                        .Where(p => p.FechaPago >= fCreacionOnly)
                        .Sum(p => p.Monto);
                    totalPagadoCiclo = Math.Max(totalPagadoCiclo, pagosDesdeCreacion);
                }

                // 3. Respaldo histórico para evitar falsos adeudos en clientes migrados
                if (totalPagadoCiclo < precioReciente)
                {
                    decimal totalPagadoHistorico = pagosSocio.Sum(p => p.Monto);
                    if (totalPagadoHistorico >= precioReciente)
                    {
                        totalPagadoCiclo = precioReciente;
                    }
                }

                if (totalPagadoCiclo >= precioReciente)
                {
                    dMem = 0m;
                }
                else
                {
                    // Solo marcar como adeudo si la membresía está vigente o venció hace menos de 15 días
                    int duracionDias = memReciente.IdMembresiaNavigation?.Meses ?? 30;
                    DateTime fVence = fInicio.AddDays(duracionDias);
                    if (fVence >= hoyDt.AddDays(-15))
                    {
                        dMem = Math.Max(0m, precioReciente - totalPagadoCiclo);
                    }
                }
            }

            deudasDict[s.IdSocio] = new SocioDeudorItemDto
            {
                IdSocio = s.IdSocio,
                NombreCompleto = s.NombreCompleto,
                Telefono = s.Telefono ?? "",
                DeudaProductos = dProd,
                DeudaMembresias = dMem
            };
        }

        ViewBag.Deudas = deudasDict;
        ViewBag.TotalDeudoresCount = deudasDict.Values.Count(d => d.DeudaTotal > 0);
        ViewBag.TotalMontoPorCobrar = deudasDict.Values.Sum(d => d.DeudaTotal);
        ViewBag.UltimasMembresias = ultimaMembresiaPorSocio;

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
    public async Task<IActionResult> Create(Socio socio, string? FotoBase64, int? IdMembresia, DateTime? FechaInicioMembresia,
        bool registrarPago = false, string metodoPago = "Efectivo", decimal? montoPago = null)
    {
        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Nuevo Socio";
            ViewBag.Membresias = await _db.Membresia.Where(m => m.IdEstado == 1).OrderBy(m => m.Nombre).ToListAsync();
            return View(socio);
        }
        socio.Nombre = socio.Nombre?.Trim();
        socio.Paterno = "";
        socio.Materno = socio.Materno?.Trim();
        socio.TipoDocumento = string.IsNullOrWhiteSpace(socio.TipoDocumento) ? "S/N" : socio.TipoDocumento.Trim();

        if (socio.FechaCreacion == null)
        {
            socio.FechaCreacion = DateTime.Now;
        }
        else if (socio.FechaCreacion.Value.Date == DateTime.Today)
        {
            socio.FechaCreacion = DateTime.Now;
        }
        else
        {
            socio.FechaCreacion = socio.FechaCreacion.Value.Date.Add(DateTime.Now.TimeOfDay);
        }

        socio.IdUsuarioCreo = UsuarioId;
        socio.IdEstado = 1;
        if (!string.IsNullOrEmpty(FotoBase64))
        {
            var b64 = FotoBase64.Contains(',') ? FotoBase64.Split(',')[1] : FotoBase64;
            socio.Foto = Convert.FromBase64String(b64);
        }

        // Asignar primera clave consecutiva entera >= 1 disponible (0001, 0002, ...)
        // Condición: Una clave se considera OCUPADA si existe un socio registrado (ya sea ACTIVO o INACTIVO).
        // Si la clave 1 (0001) no está ocupada, se asigna 1. Si está ocupada (inactivo o activo), se evalúa la siguiente (0002, etc.).
        var idsOcupados = await _db.Socios.Select(s => s.IdSocio).ToListAsync();
        var setOcupados = new HashSet<int>(idsOcupados);
        int nuevaClave = 1;
        while (setOcupados.Contains(nuevaClave))
        {
            nuevaClave++;
        }
        socio.IdSocio = nuevaClave;

        _db.Socios.Add(socio);
        await _db.SaveChangesAsync();

        // Mantener sincronizado sqlite_sequence en SQLite
        try
        {
            if (_db.Database.IsSqlite())
            {
                var maxId = await _db.Socios.MaxAsync(s => (int?)s.IdSocio) ?? 0;
                await _db.Database.ExecuteSqlRawAsync("DELETE FROM sqlite_sequence WHERE name = 'socio';");
                if (maxId > 0)
                {
                    await _db.Database.ExecuteSqlRawAsync(
                        "INSERT INTO sqlite_sequence (name, seq) VALUES ('socio', {0});", maxId);
                }
            }
        }
        catch { }

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
                FechaCreacion = socio.FechaCreacion ?? DateTime.Now,
                IdUsuarioCreo = UsuarioId
            });
            await _db.SaveChangesAsync();

            // Verificar si el usuario marcó registrar el cobro al crear el socio
            bool cobrar = false;
            if (Request.HasFormContentType && Request.Form.TryGetValue("registrarPago", out var regVal))
            {
                cobrar = regVal.Any(v => string.Equals(v, "true", StringComparison.OrdinalIgnoreCase) || 
                                         string.Equals(v, "on", StringComparison.OrdinalIgnoreCase) || 
                                         string.Equals(v, "1", StringComparison.OrdinalIgnoreCase));
            }

            decimal montoFinal = 0m;
            if (montoPago.HasValue && montoPago.Value > 0)
            {
                montoFinal = montoPago.Value;
            }
            else if (Request.Form.TryGetValue("montoPago", out var rawMonto) && !string.IsNullOrWhiteSpace(rawMonto))
            {
                var clean = rawMonto.ToString().Trim().Replace("$", "");
                if (decimal.TryParse(clean, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsedVal) ||
                    decimal.TryParse(clean, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.CurrentCulture, out parsedVal))
                {
                    montoFinal = parsedVal;
                }
            }

            if (montoFinal <= 0 && mem?.Precio != null)
            {
                montoFinal = mem.Precio.Value;
            }

            if (cobrar && montoFinal > 0)
            {
                var pago = new Pago
                {
                    IdSocio = socio.IdSocio,
                    IdMembresia = IdMembresia.Value,
                    Monto = montoFinal,
                    MetodoPago = string.IsNullOrWhiteSpace(metodoPago) ? "Efectivo" : metodoPago,
                    FechaPago = DateOnly.FromDateTime(DateTime.Today),
                    Notas = $"Cobro membresía inicial: {mem?.Nombre ?? "Membresía"}",
                    IdEstado = 1,
                    FechaCreacion = DateTime.Now,
                    IdUsuarioCreo = UsuarioId
                };
                _db.Pagos.Add(pago);
                await _db.SaveChangesAsync();
                TempData["UltimoPagoId"] = pago.IdPago;
                TempData["Success"] = $"Socio registrado y cobro de ${montoFinal:N2} ingresado a caja correctamente.";
            }
            else
            {
                TempData["Success"] = "Socio registrado con membresía a crédito (pendiente de cobro en caja).";
            }
        }
        else
        {
            TempData["Success"] = "Socio registrado correctamente.";
        }
        LiveSyncService.NotifySociosChanged();
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        ViewData["Title"] = "Editar Socio";
        var socio = await _db.Socios.FindAsync(id);
        if (socio == null) return NotFound();

        var memActiva = await _db.Sociomembresia
            .Include(sm => sm.IdMembresiaNavigation)
            .Where(sm => sm.IdSocio == id && sm.IdEstado == 1)
            .OrderByDescending(sm => sm.FechaInicioMembresia ?? sm.FechaCreacion)
            .FirstOrDefaultAsync();
        ViewBag.MembresiaActiva = memActiva;

        return View(socio);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Socio socio, string? FotoBase64, int? IdSocioMembresia, DateTime? FechaInicioMembresia)
    {
        ModelState.Remove(nameof(socio.IdEstadoNavigation));
        ModelState.Remove(nameof(socio.IdUsuarioCreoNavigation));
        ModelState.Remove(nameof(socio.Registros));
        ModelState.Remove(nameof(socio.Sociomembresia));
        ModelState.Remove(nameof(socio.Salidas));
        ModelState.Remove(nameof(socio.SocioHuella));

        if (string.IsNullOrWhiteSpace(socio.Nombre))
        {
            ModelState.AddModelError("Nombre", "El nombre del socio es obligatorio.");
        }

        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Editar Socio";
            if (IdSocioMembresia.HasValue)
            {
                ViewBag.MembresiaActiva = await _db.Sociomembresia
                    .Include(sm => sm.IdMembresiaNavigation)
                    .FirstOrDefaultAsync(sm => sm.IdSocioMembresia == IdSocioMembresia.Value);
            }
            return View(socio);
        }

        var existing = await _db.Socios.FindAsync(socio.IdSocio);
        if (existing == null) return NotFound();

        existing.Nombre = socio.Nombre?.Trim();
        existing.Paterno = "";
        existing.Materno = socio.Materno?.Trim();
        existing.TipoDocumento = string.IsNullOrWhiteSpace(socio.TipoDocumento) ? "S/N" : socio.TipoDocumento.Trim();
        existing.Telefono = socio.Telefono?.Trim();
        existing.Observaciones = socio.Observaciones?.Trim();
        if (socio.FechaCreacion.HasValue)
        {
            var horaActual = existing.FechaCreacion.HasValue ? existing.FechaCreacion.Value.TimeOfDay : DateTime.Now.TimeOfDay;
            existing.FechaCreacion = socio.FechaCreacion.Value.Date.Add(horaActual);
        }
        if (!string.IsNullOrEmpty(FotoBase64))
        {
            var b64 = FotoBase64.Contains(',') ? FotoBase64.Split(',')[1] : FotoBase64;
            existing.Foto = Convert.FromBase64String(b64);
        }

        // Si se editó la fecha de inicio de su membresía
        if (IdSocioMembresia.HasValue && FechaInicioMembresia.HasValue)
        {
            var sm = await _db.Sociomembresia.FindAsync(IdSocioMembresia.Value);
            if (sm != null && sm.IdSocio == socio.IdSocio)
            {
                sm.FechaInicioMembresia = FechaInicioMembresia.Value.Date;
            }
        }

        await _db.SaveChangesAsync();
        LiveSyncService.NotifySociosChanged();
        TempData["Success"] = "Socio y vigencia de membresía actualizados correctamente.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ActualizarFechaInicioMembresia(int idSocioMembresia, int idSocio, DateTime nuevaFechaInicio)
    {
        var sm = await _db.Sociomembresia.FindAsync(idSocioMembresia);
        if (sm == null || sm.IdSocio != idSocio)
        {
            TempData["Warning"] = "No se encontró el registro de membresía a actualizar.";
            return RedirectToAction(nameof(Membresia), new { id = idSocio });
        }

        sm.FechaInicioMembresia = nuevaFechaInicio.Date;
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Fecha de inicio de membresía actualizada a {nuevaFechaInicio:dd/MM/yyyy} correctamente.";
        return RedirectToAction(nameof(Membresia), new { id = idSocio });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var socio = await _db.Socios.FindAsync(id);
        if (socio != null) { socio.IdEstado = 2; await _db.SaveChangesAsync(); }
        LiveSyncService.NotifySociosChanged();
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

            // Sincronizar sqlite_sequence tras eliminar
            try
            {
                if (_db.Database.IsSqlite())
                {
                    var maxId = await _db.Socios.MaxAsync(s => (int?)s.IdSocio) ?? 0;
                    await _db.Database.ExecuteSqlRawAsync("DELETE FROM sqlite_sequence WHERE name = 'socio';");
                    if (maxId > 0)
                    {
                        await _db.Database.ExecuteSqlRawAsync(
                            "INSERT INTO sqlite_sequence (name, seq) VALUES ('socio', {0});", maxId);
                    }
                }
            }
            catch { }

            LiveSyncService.NotifySociosChanged();
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
            LiveSyncService.NotifySociosChanged();
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
        LiveSyncService.NotifySociosChanged();
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
    public async Task<IActionResult> AsignarMembresia(int idSocio, int idMembresia, DateTime fechaInicio,
        bool registrarPago = false, string metodoPago = "Efectivo", decimal? montoPago = null)
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
            TempData["Warning"] = $"⚠️ El socio ya tiene la membresía ‘{activa.NombreMembresia}’ activa hasta {activa.Vencimiento.Value:dd/MM/yyyy}. Se agregó la nueva de todas formas.";

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

        bool cobrar = false;
        if (Request.HasFormContentType && Request.Form.TryGetValue("registrarPago", out var regVal))
        {
            cobrar = regVal.Any(v => string.Equals(v, "true", StringComparison.OrdinalIgnoreCase) || 
                                     string.Equals(v, "on", StringComparison.OrdinalIgnoreCase) || 
                                     string.Equals(v, "1", StringComparison.OrdinalIgnoreCase));
        }

        decimal montoFinal = 0m;
        if (montoPago.HasValue && montoPago.Value > 0)
        {
            montoFinal = montoPago.Value;
        }
        else if (Request.Form.TryGetValue("montoPago", out var rawMonto) && !string.IsNullOrWhiteSpace(rawMonto))
        {
            var clean = rawMonto.ToString().Trim().Replace("$", "");
            if (decimal.TryParse(clean, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsedVal) ||
                decimal.TryParse(clean, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.CurrentCulture, out parsedVal))
            {
                montoFinal = parsedVal;
            }
        }

        if (montoFinal <= 0 && mem.Precio != null)
        {
            montoFinal = mem.Precio.Value;
        }

        if (cobrar && montoFinal > 0)
        {
            var pago = new Pago
            {
                IdSocio = idSocio,
                IdMembresia = idMembresia,
                Monto = montoFinal,
                MetodoPago = string.IsNullOrWhiteSpace(metodoPago) ? "Efectivo" : metodoPago,
                FechaPago = DateOnly.FromDateTime(DateTime.Today),
                Notas = $"Cobro membresía: {mem.Nombre}",
                IdEstado = 1,
                FechaCreacion = DateTime.Now,
                IdUsuarioCreo = UsuarioId
            };
            _db.Pagos.Add(pago);
            await _db.SaveChangesAsync();
            TempData["UltimoPagoId"] = pago.IdPago;
            TempData["Success"] = $"Membresía asignada y cobro de ${montoFinal:N2} registrado en caja correctamente.";
        }
        else
        {
            TempData["Success"] = "Membresía asignada a crédito (pendiente de cobro en caja).";
        }

        LiveSyncService.NotifySociosChanged();
        return RedirectToAction(nameof(Membresia), new { id = idSocio });
    }

    public async Task<IActionResult> PagosSocio(int id)
    {
        var socio = await _db.Socios.FindAsync(id);
        if (socio == null) return NotFound();
        ViewData["Title"] = $"Pagos de {socio.NombreCompleto}";
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
            .Include(s => s.Sociomembresia.Where(sm => sm.IdEstado == 1))
                .ThenInclude(sm => sm.IdMembresiaNavigation)
            .OrderBy(s => s.Paterno).ThenBy(s => s.Nombre)
            .ToListAsync();

        if (formato.Equals("csv", StringComparison.OrdinalIgnoreCase))
        {
            var sbCsv = new System.Text.StringBuilder();
            sbCsv.AppendLine("Clave,Nombre,TipoDocumento,NumeroDocumento,Telefono,Estado,InicioMembresia");
            foreach (var s in socios)
            {
                var estado = s.IdEstado == 1 ? "Activo" : "Inactivo";
                var fIni = s.Sociomembresia
                    .Where(sm => sm.IdEstado == 1)
                    .OrderByDescending(sm => sm.FechaInicioMembresia ?? sm.FechaCreacion)
                    .FirstOrDefault()?.FechaInicioMembresia?.ToString("dd/MM/yyyy") ?? "Sin membresía";
                var nombreCompleto = s.NombreCompleto;
                var tipoDoc = !string.IsNullOrWhiteSpace(s.TipoDocumento) ? s.TipoDocumento : "S/N";
                var numDoc = s.NumeroDocumento;
                sbCsv.AppendLine($"{s.IdSocio:D4},\"{nombreCompleto}\",\"{tipoDoc}\",\"{numDoc}\",\"{s.Telefono}\",{estado},{fIni}");
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

    // ── CONSULTA DE ASISTENCIAS DE UN SOCIO (Semana / Mes / Custom) ──
    [HttpGet]
    public async Task<IActionResult> GetAsistencias(int id, string periodo = "semana", string? desde = null, string? hasta = null)
    {
        var socio = await _db.Socios.FindAsync(id);
        if (socio == null) return NotFound(new { ok = false, msg = "Socio no encontrado." });

        DateTime fDesde, fHasta;
        DateTime hoy = DateTime.Today;

        string[] formats = { "yyyy-MM-dd", "dd/MM/yyyy", "yyyy/MM/dd", "dd-MM-yyyy", "MM/dd/yyyy" };
        if (periodo == "mes")
        {
            fDesde = new DateTime(hoy.Year, hoy.Month, 1);
            fHasta = fDesde.AddMonths(1).AddDays(-1).Add(new TimeSpan(23, 59, 59));
        }
        else if (periodo == "custom" &&
                 (DateTime.TryParseExact(desde, formats, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var d) || DateTime.TryParse(desde, out d)) &&
                 (DateTime.TryParseExact(hasta, formats, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var h) || DateTime.TryParse(hasta, out h)))
        {
            fDesde = d.Date;
            fHasta = h.Date.AddDays(1).AddTicks(-1);
        }
        else // "semana" por defecto
        {
            int diff = (7 + (hoy.DayOfWeek - DayOfWeek.Monday)) % 7;
            fDesde = hoy.AddDays(-diff).Date;
            fHasta = hoy.AddDays(1).Date.AddSeconds(-1);
        }

        var registros = await _db.Registros
            .Where(r => r.IdSocio == id && r.FechaCreacion.HasValue &&
                        r.FechaCreacion.Value >= fDesde &&
                        r.FechaCreacion.Value <= fHasta)
            .OrderByDescending(r => r.FechaCreacion)
            .ToListAsync();

        var esCultura = new System.Globalization.CultureInfo("es-MX");
        var items = registros.Select(r => new {
            id = r.Idregistro,
            fecha = r.FechaCreacion!.Value.ToString("dd/MM/yyyy"),
            dia = esCultura.DateTimeFormat.GetDayName(r.FechaCreacion!.Value.DayOfWeek),
            diaSemana = esCultura.DateTimeFormat.GetDayName(r.FechaCreacion!.Value.DayOfWeek).ToUpper(),
            hora = r.FechaCreacion!.Value.ToString("hh:mm tt"),
            fechaCompleta = r.FechaCreacion!.Value.ToString("dddd, dd 'de' MMMM 'de' yyyy", esCultura)
        }).ToList();

        int diasUnicos = registros.Select(r => r.FechaCreacion!.Value.Date).Distinct().Count();

        string pTexto = periodo switch {
            "mes" => fDesde.ToString("MMMM yyyy", esCultura),
            "semana" => $"{fDesde:dd/MM/yyyy} al {fHasta:dd/MM/yyyy}",
            _ => $"{fDesde:dd/MM/yyyy} al {fHasta:dd/MM/yyyy}"
        };

        return Json(new {
            ok = true,
            socio = new {
                id = socio.IdSocio,
                nombre = socio.NombreCompleto,
                dni = !string.IsNullOrWhiteSpace(socio.NumeroDocumento) ? socio.NumeroDocumento : "—",
                telefono = socio.Telefono ?? "—"
            },
            periodo,
            desde = fDesde.ToString("yyyy-MM-dd"),
            hasta = fHasta.ToString("yyyy-MM-dd"),
            periodoTexto = pTexto,
            periodoDisplay = pTexto,
            total = diasUnicos,
            totalAsistencias = registros.Count,
            diasUnicos = diasUnicos,
            asistencias = items,
            items,
            ultimaVisita = registros.FirstOrDefault()?.FechaCreacion?.ToString("dd/MM/yyyy hh:mm tt") ?? "Sin asistencias en este período"
        });
    }

    // ── ESTADO DE CUENTA / GESTIÓN DE FIADOS Y DEUDAS ──
    [HttpGet]
    public async Task<IActionResult> GetEstadoCuenta(int id)
    {
        try
        {
            var socio = await _db.Socios.FindAsync(id);
            if (socio == null) return NotFound(new { ok = false, msg = "Socio no encontrado." });

            // 1. Ventas de productos fiadas
            var salidasCredito = await _db.Salida
                .Include(s => s.Detallesalida)
                    .ThenInclude(d => d.IdProductoNavigation)
                .Where(s => s.IdSocio == id && s.EsCredito && s.IdEstado == 1)
                .OrderByDescending(s => s.FechaCreacion)
                .ToListAsync();

            var productosFiados = salidasCredito.Select(s => {
                string itemsStr = "Productos varios";
                if (s.Detallesalida != null && s.Detallesalida.Any())
                {
                    itemsStr = string.Join(", ", s.Detallesalida.Select(d =>
                        $"{d.IdProductoNavigation?.Nombre ?? "Producto"} (x{d.Cantidad})"));
                }

                decimal totNota = s.Total ?? 0m;
                return new {
                    idSalida = s.IdSalida,
                    folio = s.Folio ?? $"#{s.IdSalida:D6}",
                    fecha = s.FechaCreacion?.ToString("dd/MM/yyyy HH:mm") ?? "—",
                    total = totNota,
                    totalNota = totNota,
                    saldoPendiente = s.SaldoPendiente,
                    liquidado = s.SaldoPendiente <= 0,
                    notas = s.Notas ?? "",
                    items = itemsStr
                };
            }).ToList();

            decimal deudaProductos = salidasCredito.Where(s => s.SaldoPendiente > 0).Sum(s => s.SaldoPendiente);

            // 2. Membresías adeudadas (evalúa el ciclo actual/reciente)
            var membresiasAsignadas = await _db.Sociomembresia
                .Include(sm => sm.IdMembresiaNavigation)
                .Where(sm => sm.IdSocio == id && sm.IdEstado == 1)
                .OrderByDescending(sm => sm.FechaInicioMembresia ?? sm.FechaCreacion)
                .ToListAsync();

            var pagosSocio = await _db.Pagos
                .Where(p => p.IdSocio == id && p.IdEstado == 1)
                .OrderByDescending(p => p.FechaPago)
                .ToListAsync();

            var membresiasAdeudadas = new List<object>();
            decimal deudaMembresias = 0m;

            if (membresiasAsignadas.Any())
            {
                var m = membresiasAsignadas.First();
                decimal precio = m.Precio ?? 0m;
                var fInicio = (m.FechaInicioMembresia ?? m.FechaCreacion ?? DateTime.Today).Date;
                var fRegistro = (m.FechaCreacion ?? fInicio).Date;
                var fMinima = DateOnly.FromDateTime(fInicio < fRegistro ? fInicio.AddDays(-7) : fRegistro.AddDays(-7));

                decimal pagado = pagosSocio
                    .Where(p => (p.IdMembresia.HasValue && p.IdMembresia == m.IdMembresia) || p.FechaPago >= fMinima)
                    .Sum(p => p.Monto);

                if (pagado < precio)
                {
                    var fCreacionOnly = DateOnly.FromDateTime(fRegistro.AddDays(-1));
                    decimal pagosDesdeCreacion = pagosSocio
                        .Where(p => p.FechaPago >= fCreacionOnly)
                        .Sum(p => p.Monto);
                    pagado = Math.Max(pagado, pagosDesdeCreacion);
                }

                if (pagado < precio && pagosSocio.Sum(p => p.Monto) >= precio)
                {
                    pagado = precio;
                }

                decimal pendiente = Math.Max(0m, precio - pagado);
                int duracionDias = m.IdMembresiaNavigation?.Meses ?? 30;
                DateTime fVence = fInicio.AddDays(duracionDias);

                if (pendiente > 0 && fVence >= DateTime.Today.AddDays(-15))
                {
                    deudaMembresias = pendiente;
                    string fIni = m.FechaInicioMembresia?.ToString("dd/MM/yyyy") ?? m.FechaCreacion?.ToString("dd/MM/yyyy") ?? "—";
                    string vigencia = $"{fIni} al {fVence:dd/MM/yyyy}";

                    membresiasAdeudadas.Add(new {
                        id = m.IdSocioMembresia,
                        nombre = m.IdMembresiaNavigation?.Nombre ?? "Membresía",
                        vigencia = vigencia,
                        precio = precio,
                        pagado = pagado,
                        pendiente = pendiente
                    });
                }
            }

            decimal deudaTotal = deudaProductos + deudaMembresias;

            return Json(new {
                ok = true,
                socio = new {
                    id = socio.IdSocio,
                    nombre = socio.NombreCompleto,
                    dni = !string.IsNullOrWhiteSpace(socio.NumeroDocumento) ? socio.NumeroDocumento : "—",
                    telefono = socio.Telefono ?? "—"
                },
                deudaTotal,
                totalDeuda = deudaTotal,
                deudaProductos,
                deudaMembresias,
                productosFiados,
                membresiasAdeudadas
            });
        }
        catch (Exception ex)
        {
            return Json(new { ok = false, msg = "Error al consultar estado de cuenta: " + ex.Message });
        }
    }

    public class RegistrarAbonoRequest
    {
        public int IdSocio { get; set; }
        public decimal Monto { get; set; }
        public string MetodoPago { get; set; } = "Efectivo";
        public string TipoCobro { get; set; } = "General"; // "Productos" | "Membresia" | "General"
        public string? Notas { get; set; }
    }

    // ── REGISTRAR COBRO / ABONO DE DEUDA (ENTRA A CAJA EN LA FECHA ACTUAL) ──
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegistrarAbono([FromBody] RegistrarAbonoRequest dto)
    {
        if (dto == null || dto.Monto <= 0)
            return Json(new { ok = false, msg = "El monto a abonar debe ser mayor a $0." });

        var socio = await _db.Socios.FindAsync(dto.IdSocio);
        if (socio == null)
            return Json(new { ok = false, msg = "Socio no encontrado." });

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            decimal restante = dto.Monto;
            string tipoCobro = string.IsNullOrWhiteSpace(dto.TipoCobro) ? "General" : dto.TipoCobro;

            // Descontar saldo pendiente de las notas fiadas de productos más antiguas
            if (tipoCobro == "Productos" || tipoCobro == "General")
            {
                var salidasPendientes = await _db.Salida
                    .Where(s => s.IdSocio == dto.IdSocio && s.EsCredito && s.SaldoPendiente > 0 && s.IdEstado == 1)
                    .OrderBy(s => s.FechaCreacion)
                    .ToListAsync();

                foreach (var salida in salidasPendientes)
                {
                    if (restante <= 0) break;

                    if (salida.SaldoPendiente <= restante)
                    {
                        restante -= salida.SaldoPendiente;
                        salida.SaldoPendiente = 0;
                        salida.FechaLiquidacion = DateTime.Now;
                    }
                    else
                    {
                        salida.SaldoPendiente -= restante;
                        restante = 0;
                    }
                }
            }

            int? idMemRef = null;
            if (dto.TipoCobro == "Membresia")
            {
                idMemRef = await _db.Sociomembresia
                    .Where(sm => sm.IdSocio == dto.IdSocio && sm.IdEstado == 1)
                    .OrderByDescending(sm => sm.FechaInicioMembresia ?? sm.FechaCreacion)
                    .Select(sm => sm.IdMembresia)
                    .FirstOrDefaultAsync();
            }
            else if (tipoCobro == "General" && restante > 0)
            {
                idMemRef = await _db.Sociomembresia
                    .Where(sm => sm.IdSocio == dto.IdSocio && sm.IdEstado == 1)
                    .OrderByDescending(sm => sm.FechaInicioMembresia ?? sm.FechaCreacion)
                    .Select(sm => sm.IdMembresia)
                    .FirstOrDefaultAsync();
            }

            // Registrar en la tabla pago para que ENTRA FORMALMENTE A LA CAJA HOY EN EL CORTE
            var pago = new Pago
            {
                IdSocio = dto.IdSocio,
                IdMembresia = idMemRef,
                Monto = dto.Monto,
                MetodoPago = string.IsNullOrWhiteSpace(dto.MetodoPago) ? "Efectivo" : dto.MetodoPago,
                FechaPago = DateOnly.FromDateTime(DateTime.Today),
                Notas = $"Abono a deuda ({dto.TipoCobro}): {dto.Notas}".Trim(),
                IdEstado = 1,
                FechaCreacion = DateTime.Now,
                IdUsuarioCreo = UsuarioId
            };

            _db.Pagos.Add(pago);
            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            LiveSyncService.NotifySociosChanged();
            LiveSyncService.NotifyVentasChanged();

            return Json(new {
                ok = true,
                msg = $"Abono de ${dto.Monto:N2} cobrado exitosamente e ingresado al corte de caja de hoy.",
                pagoId = pago.IdPago
            });
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            return Json(new { ok = false, msg = "Error al registrar el abono: " + ex.Message });
        }
    }

    // ── EXPORTAR REPORTE DE DEUDORES EN EXCEL NATIVO CON LOGO ──
    public async Task<IActionResult> ExportarDeudores(string formato = "excel")
    {
        var socios = await _db.Socios
            .Include(s => s.IdEstadoNavigation)
            .OrderBy(s => s.Paterno).ThenBy(s => s.Nombre)
            .ToListAsync();

        var deudasProductos = await _db.Salida
            .Where(s => s.IdEstado == 1 && s.EsCredito && s.SaldoPendiente > 0 && s.IdSocio != null)
            .GroupBy(s => s.IdSocio!.Value)
            .Select(g => new { IdSocio = g.Key, Total = g.Sum(x => x.SaldoPendiente) })
            .ToDictionaryAsync(x => x.IdSocio, x => x.Total);

        var hoyDt = DateTime.Today;
        var membresiasRecientes = await _db.Sociomembresia
            .Include(sm => sm.IdMembresiaNavigation)
            .Where(sm => sm.IdEstado == 1 && sm.IdSocio != null)
            .OrderByDescending(sm => sm.FechaInicioMembresia ?? sm.FechaCreacion)
            .ToListAsync();

        var ultimaMembresiaPorSocio = membresiasRecientes
            .GroupBy(sm => sm.IdSocio!.Value)
            .ToDictionary(g => g.Key, g => g.First());

        var todosPagos = await _db.Pagos
            .Where(p => p.IdEstado == 1 && (p.IdMembresia != null || p.Notas == null || !p.Notas.Contains("[TIENDA_CREDITO]")))
            .ToListAsync();

        var pagosPorSocio = todosPagos
            .GroupBy(p => p.IdSocio)
            .ToDictionary(g => g.Key, g => g.ToList());

        var deudores = new List<SocioDeudorItemDto>();
        foreach (var s in socios)
        {
            decimal dProd = deudasProductos.TryGetValue(s.IdSocio, out var dp) ? dp : 0m;
            decimal dMem = 0m;

            if (ultimaMembresiaPorSocio.TryGetValue(s.IdSocio, out var memReciente))
            {
                decimal precioReciente = memReciente.Precio ?? 0m;
                var pagosSocio = pagosPorSocio.TryGetValue(s.IdSocio, out var lp) ? lp : new List<Pago>();

                var fInicio = (memReciente.FechaInicioMembresia ?? memReciente.FechaCreacion ?? DateTime.Today).Date;
                var fRegistro = (memReciente.FechaCreacion ?? fInicio).Date;
                var fMinima = DateOnly.FromDateTime(fInicio < fRegistro ? fInicio.AddDays(-7) : fRegistro.AddDays(-7));

                decimal totalPagadoCiclo = pagosSocio
                    .Where(p => (p.IdMembresia.HasValue && p.IdMembresia == memReciente.IdMembresia) || p.FechaPago >= fMinima)
                    .Sum(p => p.Monto);

                if (totalPagadoCiclo < precioReciente)
                {
                    var fCreacionOnly = DateOnly.FromDateTime(fRegistro.AddDays(-1));
                    decimal pagosDesdeCreacion = pagosSocio
                        .Where(p => p.FechaPago >= fCreacionOnly)
                        .Sum(p => p.Monto);
                    totalPagadoCiclo = Math.Max(totalPagadoCiclo, pagosDesdeCreacion);
                }

                if (totalPagadoCiclo < precioReciente && pagosSocio.Sum(p => p.Monto) >= precioReciente)
                {
                    totalPagadoCiclo = precioReciente;
                }

                if (totalPagadoCiclo >= precioReciente)
                {
                    dMem = 0m;
                }
                else
                {
                    int duracionDias = memReciente.IdMembresiaNavigation?.Meses ?? 30;
                    DateTime fVence = fInicio.AddDays(duracionDias);
                    if (fVence >= hoyDt.AddDays(-15))
                    {
                        dMem = Math.Max(0m, precioReciente - totalPagadoCiclo);
                    }
                }
            }

            if (dProd + dMem > 0)
            {
                deudores.Add(new SocioDeudorItemDto
                {
                    IdSocio = s.IdSocio,
                    NombreCompleto = s.NombreCompleto,
                    Telefono = s.Telefono ?? "",
                    DeudaProductos = dProd,
                    DeudaMembresias = dMem
                });
            }
        }

        var cfg = await _db.Configuracions.FirstOrDefaultAsync();
        var gymNombre = !string.IsNullOrWhiteSpace(cfg?.NombreGimnacio) ? cfg.NombreGimnacio : AppSettings.GymNombre;
        var logoBytes = await ExcelReportHelper.GetLogoBytesAsync(_db, _env);

        var xlsxBytes = ExcelReportHelper.GenerarSociosDeudoresXlsx(deudores, gymNombre, logoBytes);
        return File(xlsxBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"socios_deudores_{DateTime.Today:yyyyMMdd}.xlsx");
    }

    // ── EXPORTAR REPORTE DE ASISTENCIAS DE UN SOCIO EN EXCEL CON LOGO ──
    public async Task<IActionResult> ExportarAsistenciasSocio(int id, string periodo = "semana", string? desde = null, string? hasta = null)
    {
        var socio = await _db.Socios.FindAsync(id);
        if (socio == null) return NotFound();

        DateTime fDesde, fHasta;
        DateTime hoy = DateTime.Today;

        string[] formats = { "yyyy-MM-dd", "dd/MM/yyyy", "yyyy/MM/dd", "dd-MM-yyyy", "MM/dd/yyyy" };
        if (periodo == "mes")
        {
            fDesde = new DateTime(hoy.Year, hoy.Month, 1);
            fHasta = fDesde.AddMonths(1).AddDays(-1).Add(new TimeSpan(23, 59, 59));
        }
        else if (periodo == "custom" &&
                 (DateTime.TryParseExact(desde, formats, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var d) || DateTime.TryParse(desde, out d)) &&
                 (DateTime.TryParseExact(hasta, formats, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var h) || DateTime.TryParse(hasta, out h)))
        {
            fDesde = d.Date;
            fHasta = h.Date.AddDays(1).AddTicks(-1);
        }
        else
        {
            int diff = (7 + (hoy.DayOfWeek - DayOfWeek.Monday)) % 7;
            fDesde = hoy.AddDays(-diff).Date;
            fHasta = hoy.AddDays(1).Date.AddSeconds(-1);
        }

        var registros = await _db.Registros
            .Where(r => r.IdSocio == id && r.FechaCreacion.HasValue &&
                        r.FechaCreacion.Value >= fDesde &&
                        r.FechaCreacion.Value <= fHasta)
            .OrderByDescending(r => r.FechaCreacion)
            .ToListAsync();

        var esCultura = new System.Globalization.CultureInfo("es-MX");
        string periodoTexto = periodo == "mes" ? fDesde.ToString("MMMM yyyy", esCultura) : $"{fDesde:dd/MM/yyyy} al {fHasta:dd/MM/yyyy}";

        var cfg = await _db.Configuracions.FirstOrDefaultAsync();
        var gymNombre = !string.IsNullOrWhiteSpace(cfg?.NombreGimnacio) ? cfg.NombreGimnacio : AppSettings.GymNombre;
        var logoBytes = await ExcelReportHelper.GetLogoBytesAsync(_db, _env);

        var xlsxBytes = ExcelReportHelper.GenerarAsistenciasSocioXlsx(socio, registros, periodoTexto, gymNombre, logoBytes);
        return File(xlsxBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"asistencias_socio_{id}_{DateTime.Today:yyyyMMdd}.xlsx");
    }

    // ── OBTENER SOCIOS CON TELÉFONO PARA DIFUSIÓN MASIVA POR WHATSAPP ──
    [HttpGet]
    public async Task<IActionResult> GetSociosParaDifusion(string filtro = "todos")
    {
        var hoyDt = DateTime.Today;

        // 1. Obtener todos los socios que tengan teléfono registrado
        var socios = await _db.Socios
            .Include(s => s.IdEstadoNavigation)
            .Where(s => !string.IsNullOrWhiteSpace(s.Telefono))
            .OrderBy(s => s.Nombre)
            .ToListAsync();

        // 2. Obtener deudas de productos fiados (crédito pendiente en tienda)
        var deudasProductos = await _db.Salida
            .Where(s => s.IdEstado == 1 && s.EsCredito && s.SaldoPendiente > 0 && s.IdSocio != null)
            .GroupBy(s => s.IdSocio!.Value)
            .Select(g => new { IdSocio = g.Key, Total = g.Sum(x => x.SaldoPendiente) })
            .ToDictionaryAsync(x => x.IdSocio, x => x.Total);

        // 3. Obtener última membresía de cada socio
        var membresiasRecientes = await _db.Sociomembresia
            .Include(sm => sm.IdMembresiaNavigation)
            .Where(sm => sm.IdEstado == 1 && sm.IdSocio != null)
            .OrderByDescending(sm => sm.FechaInicioMembresia ?? sm.FechaCreacion)
            .ToListAsync();

        var ultimaMembresiaPorSocio = membresiasRecientes
            .GroupBy(sm => sm.IdSocio!.Value)
            .ToDictionary(g => g.Key, g => g.First());

        // 4. Pagos para calcular deudas de membresías
        var todosPagos = await _db.Pagos
            .Where(p => p.IdEstado == 1 && (p.IdMembresia != null || p.Notas == null || !p.Notas.Contains("[TIENDA_CREDITO]")))
            .ToListAsync();

        var pagosPorSocio = todosPagos
            .GroupBy(p => p.IdSocio)
            .ToDictionary(g => g.Key, g => g.ToList());

        var config = await _db.Configuracions.FirstOrDefaultAsync();
        string gymNombre = !string.IsNullOrWhiteSpace(config?.NombreGimnacio) ? config.NombreGimnacio : AppSettings.GymNombre;

        var lista = new List<dynamic>();

        foreach (var s in socios)
        {
            decimal dProd = deudasProductos.TryGetValue(s.IdSocio, out var dp) ? dp : 0m;
            decimal dMem = 0m;
            bool tieneMembresia = ultimaMembresiaPorSocio.TryGetValue(s.IdSocio, out var memReciente);
            DateTime? fechaVencimiento = null;
            string nombreMembresia = "Sin membresía";
            bool activo = false;
            bool vencido = true;

            if (tieneMembresia && memReciente != null)
            {
                nombreMembresia = memReciente.IdMembresiaNavigation?.Nombre ?? "Membresía";
                var fInicio = (memReciente.FechaInicioMembresia ?? memReciente.FechaCreacion ?? DateTime.Today).Date;
                var fRegistro = (memReciente.FechaCreacion ?? fInicio).Date;
                int duracionDias = memReciente.IdMembresiaNavigation?.Meses ?? 30;
                fechaVencimiento = fInicio.AddDays(duracionDias);

                // Activo si no ha vencido y el socio está activo
                if (fechaVencimiento.Value >= hoyDt && s.IdEstado == 1)
                {
                    activo = true;
                    vencido = false;
                }
                else
                {
                    activo = false;
                    vencido = true;
                }

                // Cálculo de saldo pendiente de membresía
                decimal precioReciente = memReciente.Precio ?? 0m;
                var pagosSocio = pagosPorSocio.TryGetValue(s.IdSocio, out var lp) ? lp : new List<Pago>();
                var fMinima = DateOnly.FromDateTime(fInicio < fRegistro ? fInicio.AddDays(-7) : fRegistro.AddDays(-7));

                decimal totalPagadoCiclo = pagosSocio
                    .Where(p => (p.IdMembresia.HasValue && p.IdMembresia == memReciente.IdMembresia) || p.FechaPago >= fMinima)
                    .Sum(p => p.Monto);

                if (totalPagadoCiclo < precioReciente)
                {
                    var fCreacionOnly = DateOnly.FromDateTime(fRegistro.AddDays(-1));
                    decimal pagosDesdeCreacion = pagosSocio
                        .Where(p => p.FechaPago >= fCreacionOnly)
                        .Sum(p => p.Monto);
                    totalPagadoCiclo = Math.Max(totalPagadoCiclo, pagosDesdeCreacion);
                }

                if (totalPagadoCiclo < precioReciente)
                {
                    decimal totalPagadoHistorico = pagosSocio.Sum(p => p.Monto);
                    if (totalPagadoHistorico >= precioReciente)
                    {
                        totalPagadoCiclo = precioReciente;
                    }
                }

                if (totalPagadoCiclo < precioReciente && fechaVencimiento.Value >= hoyDt.AddDays(-15))
                {
                    dMem = Math.Max(0m, precioReciente - totalPagadoCiclo);
                }
            }
            else
            {
                activo = false;
                vencido = true;
            }

            decimal deudaTotal = dProd + dMem;

            lista.Add(new
            {
                idSocio = s.IdSocio,
                nombre = s.NombreCompleto,
                nombreCompleto = s.NombreCompleto,
                dni = s.Materno ?? "",
                telefono = s.Telefono!.Trim().Replace(" ", "").Replace("-", ""),
                membresia = nombreMembresia,
                vencimiento = fechaVencimiento.HasValue ? fechaVencimiento.Value.ToString("dd/MM/yyyy") : "Sin fecha",
                vencido = vencido,
                activo = activo,
                deuda = deudaTotal,
                tieneDeuda = deudaTotal > 0
            });
        }

        // 5. Aplicar filtro seleccionado por el usuario
        var fClean = (filtro ?? "todos").Trim().ToLowerInvariant();
        if (fClean == "activos")
        {
            lista = lista.Where(s => (bool)s.activo == true).ToList();
        }
        else if (fClean == "inactivos" || fClean == "vencidos")
        {
            lista = lista.Where(s => (bool)s.activo == false).ToList();
        }
        else if (fClean == "deudores" || fClean == "deuda" || fClean == "con_deuda")
        {
            lista = lista.Where(s => (bool)s.tieneDeuda == true).ToList();
        }

        return Json(new
        {
            ok = true,
            gymNombre = gymNombre,
            filtro = fClean,
            total = lista.Count,
            socios = lista
        });
    }
}
