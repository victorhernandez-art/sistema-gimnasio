using GymWeb.Helpers;
using GymWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GymWeb.Controllers;

public class CheckoutItemDto
{
    public int IdProducto { get; set; }
    public int Cantidad { get; set; }
}

public class CheckoutRequestDto
{
    public List<CheckoutItemDto> Items { get; set; } = new();
    public string MetodoPago { get; set; } = "Efectivo"; // Efectivo | Transferencia | Tarjeta
    public decimal Descuento { get; set; } = 0;
    public decimal? MontoRecibido { get; set; }
    public decimal? Cambio { get; set; }
    public string? Referencia { get; set; }
    public string? Notas { get; set; }
}

public class ProductosController : AuthController
{
    private readonly GymContext _db;
    private readonly IWebHostEnvironment _env;
    public ProductosController(GymContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    // ── Index principal (Tienda POS + Historial) ─────────────────────────
    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Tienda";

        var productos = await _db.Productos
            .Where(p => p.IdEstado == 1)
            .OrderBy(p => p.Nombre)
            .ToListAsync();

        // ── Tarjetas de Resumen (Métricas de HOY) ────────────────────────
        var hoyInicio = DateTime.Today;
        var hoyFin = hoyInicio.AddDays(1);

        var salidasHoy = await _db.Salida
            .Include(s => s.Detallesalida)
            .Where(s => s.IdEstado == 1 && s.FechaCreacion >= hoyInicio && s.FechaCreacion < hoyFin)
            .ToListAsync();

        int ventasHoy = salidasHoy.Count;
        decimal ingresosHoy = salidasHoy.Sum(s => s.Total ?? s.Detallesalida.Sum(d => (decimal)(d.Cantidad * (d.PrecioUnitario ?? 0m))));
        int productosVendidosHoy = salidasHoy.SelectMany(s => s.Detallesalida).Sum(d => d.Cantidad);
        int stockBajo = productos.Count(p => p.Stock <= p.StockMinimo);

        ViewBag.VentasHoy            = ventasHoy;
        ViewBag.IngresosHoy          = ingresosHoy;
        ViewBag.ProductosVendidosHoy = productosVendidosHoy;
        ViewBag.StockBajo            = stockBajo;

        // Conteo de ventas en los últimos 30 días (para identificar "Más vendido" del último mes)
        var hace30Dias = DateTime.Today.AddDays(-30);
        var ventasUltimos30Dias = await _db.Detallesalida
            .Where(d => d.IdSalidaNavigation != null && d.IdSalidaNavigation.IdEstado == 1 && d.IdSalidaNavigation.FechaCreacion >= hace30Dias)
            .GroupBy(d => d.IdProducto)
            .Select(g => new { IdProducto = g.Key, Cantidad = g.Sum(x => x.Cantidad) })
            .ToListAsync();

        ViewBag.VentasCantidad = ventasUltimos30Dias.ToDictionary(v => v.IdProducto ?? 0, v => (int)v.Cantidad);

        // Identificar el ID del producto más vendido en los últimos 30 días (si tiene ventas)
        int idMasVendido = ventasUltimos30Dias
            .Where(v => v.Cantidad > 0)
            .OrderByDescending(v => v.Cantidad)
            .Select(v => v.IdProducto ?? 0)
            .FirstOrDefault();
        ViewBag.IdMasVendido = idMasVendido;

        ViewBag.Productos  = productos;
        ViewBag.Categorias = productos
            .Where(p => !string.IsNullOrWhiteSpace(p.Categoria))
            .Select(p => p.Categoria!.Trim())
            .Distinct()
            .OrderBy(c => c)
            .ToList();

        // ── Historial de Ventas ──────────────────────────────────────────
        var historialSalidas = await _db.Salida
            .Include(s => s.IdUsuarioCreoNavigation)
            .Include(s => s.Detallesalida)
                .ThenInclude(d => d.IdProductoNavigation)
            .Where(s => s.IdEstado == 1)
            .OrderByDescending(s => s.FechaCreacion)
            .Take(150)
            .ToListAsync();

        var config = await _db.Configuracions.FirstOrDefaultAsync();
        ViewBag.GymNombre    = !string.IsNullOrWhiteSpace(config?.NombreGimnacio) ? config.NombreGimnacio : AppSettings.GymNombre;
        ViewBag.GymDomicilio = !string.IsNullOrWhiteSpace(config?.Domicilio) ? config.Domicilio : AppSettings.GymDomicilio;
        ViewBag.GymTelefono  = !string.IsNullOrWhiteSpace(config?.Telefono) ? config.Telefono : AppSettings.GymTelefono;
        ViewBag.GymPieTicket = !string.IsNullOrWhiteSpace(config?.Mensaje) ? config.Mensaje : AppSettings.GymPieTicket;

        ViewBag.HistorialSalidas = historialSalidas;

        return View();
    }

    // ── Cobrar Venta (Carrito POS Multi-producto) ────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CobrarVenta([FromBody] CheckoutRequestDto dto)
    {
        if (dto == null || dto.Items == null || !dto.Items.Any())
            return Json(new { ok = false, msg = "El carrito de compra está vacío." });

        if (dto.Items.Any(i => i.Cantidad < 1))
            return Json(new { ok = false, msg = "Las cantidades deben ser al menos de 1 unidad." });

        string metodo = dto.MetodoPago?.Trim() ?? "Efectivo";
        if (metodo != "Efectivo" && metodo != "Transferencia" && metodo != "Tarjeta")
            metodo = "Efectivo";

        await using var tx = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        try
        {
            var itemIds = dto.Items.Select(i => i.IdProducto).Distinct().ToList();
            var prods = await _db.Productos.Where(p => itemIds.Contains(p.IdProducto) && p.IdEstado == 1).ToListAsync();

            decimal subtotal = 0;
            var detallesParaGuardar = new List<Detallesalidum>();
            var resumenTicket = new List<object>();

            foreach (var item in dto.Items)
            {
                var prod = prods.FirstOrDefault(p => p.IdProducto == item.IdProducto);
                if (prod == null)
                {
                    await tx.RollbackAsync();
                    return Json(new { ok = false, msg = $"El producto con ID {item.IdProducto} no existe o no está activo." });
                }

                if (prod.Stock < item.Cantidad)
                {
                    await tx.RollbackAsync();
                    return Json(new { ok = false, msg = $"Stock insuficiente para '{prod.Nombre}'. Disponible: {prod.Stock}, Solicitado: {item.Cantidad}." });
                }

                decimal precio = prod.Precio ?? 0m;
                decimal itemSubtotal = precio * item.Cantidad;
                subtotal += itemSubtotal;

                // Descontar inventario
                prod.Stock -= item.Cantidad;

                detallesParaGuardar.Add(new Detallesalidum
                {
                    IdProducto = prod.IdProducto,
                    Cantidad = item.Cantidad,
                    PrecioUnitario = precio
                });

                resumenTicket.Add(new
                {
                    idProducto = prod.IdProducto,
                    nombre = prod.Nombre,
                    categoria = prod.Categoria,
                    cantidad = item.Cantidad,
                    precioUnitario = precio,
                    subtotal = itemSubtotal,
                    nuevoStock = prod.Stock
                });
            }

            decimal descuento = Math.Max(0, dto.Descuento);
            decimal totalFinal = Math.Max(0, subtotal - descuento);

            if (metodo == "Efectivo" && dto.MontoRecibido.HasValue && dto.MontoRecibido.Value < totalFinal)
            {
                await tx.RollbackAsync();
                return Json(new { ok = false, msg = $"El efectivo recibido (${dto.MontoRecibido.Value:N2}) es menor que el total a pagar (${totalFinal:N2})." });
            }

            decimal cambio = 0;
            if (metodo == "Efectivo" && dto.MontoRecibido.HasValue)
            {
                cambio = Math.Max(0, dto.MontoRecibido.Value - totalFinal);
            }

            var salida = new Salidum
            {
                FechaCreacion = DateTime.Now,
                IdUsuarioCreo = UsuarioId,
                Total = totalFinal,
                MetodoPago = metodo,
                Descuento = descuento,
                MontoRecibido = dto.MontoRecibido,
                Cambio = cambio,
                Referencia = dto.Referencia?.Trim(),
                Notas = dto.Notas?.Trim(),
                IdEstado = 1
            };

            _db.Salida.Add(salida);
            await _db.SaveChangesAsync();

            // Formato de Folio estándar #000125
            salida.Folio = $"#{salida.IdSalida:D6}";

            foreach (var d in detallesParaGuardar)
            {
                d.IdSalida = salida.IdSalida;
                _db.Detallesalida.Add(d);
            }

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            var config = await _db.Configuracions.FirstOrDefaultAsync();
            return Json(new
            {
                ok = true,
                folio = salida.Folio,
                idSalida = salida.IdSalida,
                fecha = salida.FechaCreacion?.ToString("dd/MM/yyyy HH:mm"),
                usuario = User.Identity?.Name ?? "Recepción",
                metodo = salida.MetodoPago,
                subtotal = subtotal,
                descuento = descuento,
                total = totalFinal,
                montoRecibido = salida.MontoRecibido,
                cambio = salida.Cambio,
                referencia = salida.Referencia,
                gymNombre = !string.IsNullOrWhiteSpace(config?.NombreGimnacio) ? config.NombreGimnacio : AppSettings.GymNombre,
                gymDomicilio = !string.IsNullOrWhiteSpace(config?.Domicilio) ? config.Domicilio : AppSettings.GymDomicilio,
                gymTelefono = !string.IsNullOrWhiteSpace(config?.Telefono) ? config.Telefono : AppSettings.GymTelefono,
                gymPieTicket = !string.IsNullOrWhiteSpace(config?.Mensaje) ? config.Mensaje : AppSettings.GymPieTicket,
                items = resumenTicket
            });
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            return Json(new { ok = false, msg = "Error al procesar la venta: " + ex.Message });
        }
    }

    // ── Detalle de Venta para Modal de Historial ────────────────────────
    [HttpGet]
    public async Task<IActionResult> GetDetalleVenta(int idSalida)
    {
        var salida = await _db.Salida
            .Include(s => s.IdUsuarioCreoNavigation)
            .Include(s => s.Detallesalida)
                .ThenInclude(d => d.IdProductoNavigation)
            .FirstOrDefaultAsync(s => s.IdSalida == idSalida);

        if (salida == null)
            return NotFound(new { ok = false, msg = "Venta no encontrada." });

        var detalles = salida.Detallesalida.Select(d => new
        {
            idProducto = d.IdProducto,
            nombre = d.IdProductoNavigation?.Nombre ?? "Producto sin nombre",
            categoria = d.IdProductoNavigation?.Categoria ?? "Otro",
            cantidad = d.Cantidad,
            precioUnitario = d.PrecioUnitario ?? 0m,
            subtotal = (d.PrecioUnitario ?? 0m) * d.Cantidad
        }).ToList();

        decimal totalCalculado = salida.Total ?? detalles.Sum(x => x.subtotal);
        var config = await _db.Configuracions.FirstOrDefaultAsync();

        return Json(new
        {
            ok = true,
            idSalida = salida.IdSalida,
            folio = salida.Folio ?? $"#{salida.IdSalida:D6}",
            fecha = salida.FechaCreacion?.ToString("dd/MM/yyyy HH:mm"),
            metodoPago = salida.MetodoPago ?? "Efectivo",
            usuario = salida.IdUsuarioCreoNavigation?.Nombre ?? "Recepción",
            subtotal = detalles.Sum(x => x.subtotal),
            descuento = salida.Descuento ?? 0m,
            total = totalCalculado,
            montoRecibido = salida.MontoRecibido,
            cambio = salida.Cambio,
            referencia = salida.Referencia,
            notas = salida.Notas,
            gymNombre = !string.IsNullOrWhiteSpace(config?.NombreGimnacio) ? config.NombreGimnacio : AppSettings.GymNombre,
            gymDomicilio = !string.IsNullOrWhiteSpace(config?.Domicilio) ? config.Domicilio : AppSettings.GymDomicilio,
            gymTelefono = !string.IsNullOrWhiteSpace(config?.Telefono) ? config.Telefono : AppSettings.GymTelefono,
            gymPieTicket = !string.IsNullOrWhiteSpace(config?.Mensaje) ? config.Mensaje : AppSettings.GymPieTicket,
            items = detalles
        });
    }

    // ── Crear producto (AJAX) ───────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateAjax(string nombre, string? categoria, decimal precio,
        decimal? costo, int stock, int stockMinimo, string? descripcion, IFormFile? foto)
    {
        if (string.IsNullOrWhiteSpace(nombre) || precio <= 0)
            return Json(new { ok = false, msg = "Nombre y precio son requeridos." });

        string? imgPath = null;
        if (foto != null && foto.Length > 0)
        {
            var ext = Path.GetExtension(foto.FileName).ToLowerInvariant();
            var allowed = new[] { ".png", ".jpg", ".jpeg", ".webp", ".svg" };
            if (allowed.Contains(ext))
            {
                var folder = Path.Combine(_env.WebRootPath, "img", "productos");
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                var fileName = $"prod_{DateTime.Now:yyyyMMddHHmmssfff}_{Guid.NewGuid().ToString("N")[..6]}{ext}";
                var fullPath = Path.Combine(folder, fileName);
                using (var fs = new FileStream(fullPath, FileMode.Create))
                {
                    await foto.CopyToAsync(fs);
                }
                imgPath = $"/img/productos/{fileName}";
            }
        }

        var p = new Producto
        {
            Nombre        = nombre.Trim(),
            Categoria     = string.IsNullOrWhiteSpace(categoria) ? "Otro" : categoria.Trim(),
            Precio        = precio,
            Costo         = costo,
            Stock         = Math.Max(0, stock),
            StockMinimo   = Math.Max(0, stockMinimo),
            Descripcion   = descripcion?.Trim(),
            ImagenUrl     = imgPath,
            IdEstado      = 1,
            FechaCreacion = DateTime.Now,
            IdUsuarioCreo = UsuarioId
        };
        _db.Productos.Add(p);
        await _db.SaveChangesAsync();
        return Json(new { ok = true, id = p.IdProducto, imagenUrl = p.ImagenUrl });
    }

    // ── Editar producto (AJAX) ──────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditAjax(int id, string nombre, string? categoria, decimal precio,
        decimal? costo, int stock, int stockMinimo, string? descripcion, IFormFile? foto, bool eliminarFoto = false)
    {
        var p = await _db.Productos.FindAsync(id);
        if (p == null) return Json(new { ok = false, msg = "Producto no encontrado." });

        if (costo.HasValue && costo.Value > precio)
            return Json(new { ok = false, msg = $"El costo (${costo:N2}) no puede ser mayor que el precio de venta (${precio:N2})." });

        p.Nombre      = nombre.Trim();
        p.Categoria   = string.IsNullOrWhiteSpace(categoria) ? "Otro" : categoria.Trim();
        p.Precio      = precio;
        p.Costo       = costo;
        p.Stock       = Math.Max(0, stock);
        p.StockMinimo = Math.Max(0, stockMinimo);
        p.Descripcion = descripcion?.Trim();

        if (eliminarFoto)
        {
            if (!string.IsNullOrEmpty(p.ImagenUrl) && p.ImagenUrl.StartsWith("/img/productos/prod_"))
            {
                var oldPath = Path.Combine(_env.WebRootPath, p.ImagenUrl.TrimStart('/'));
                if (System.IO.File.Exists(oldPath))
                {
                    try { System.IO.File.Delete(oldPath); } catch { }
                }
            }
            p.ImagenUrl = null;
        }
        else if (foto != null && foto.Length > 0)
        {
            var ext = Path.GetExtension(foto.FileName).ToLowerInvariant();
            var allowed = new[] { ".png", ".jpg", ".jpeg", ".webp", ".svg" };
            if (allowed.Contains(ext))
            {
                var folder = Path.Combine(_env.WebRootPath, "img", "productos");
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                // Eliminar foto física anterior si existía
                if (!string.IsNullOrEmpty(p.ImagenUrl) && p.ImagenUrl.StartsWith("/img/productos/prod_"))
                {
                    var oldPath = Path.Combine(_env.WebRootPath, p.ImagenUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                    if (System.IO.File.Exists(oldPath))
                    {
                        try { System.IO.File.Delete(oldPath); } catch { }
                    }
                }

                var fileName = $"prod_{id}_{DateTime.Now:yyyyMMddHHmmssfff}{ext}";
                var fullPath = Path.Combine(folder, fileName);
                using (var fs = new FileStream(fullPath, FileMode.Create))
                {
                    await foto.CopyToAsync(fs);
                }
                p.ImagenUrl = $"/img/productos/{fileName}";
            }
        }

        await _db.SaveChangesAsync();
        return Json(new { ok = true, imagenUrl = p.ImagenUrl });
    }

    // ── Eliminar (inactivar) ────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var p = await _db.Productos.FindAsync(id);
        if (p != null) { p.IdEstado = 2; await _db.SaveChangesAsync(); }
        TempData["Success"] = "Producto eliminado.";
        return RedirectToAction(nameof(Index));
    }

    // ── Agregar stock (reabastecimiento) ───────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AgregarStock(int idProducto, int cantidad)
    {
        if (cantidad < 1) return Json(new { ok = false, msg = "Cantidad inválida." });
        var p = await _db.Productos.FindAsync(idProducto);
        if (p == null) return Json(new { ok = false, msg = "Producto no encontrado." });
        p.Stock += cantidad;
        await _db.SaveChangesAsync();
        return Json(new { ok = true, nuevoStock = p.Stock });
    }

    // ── Venta rápida desde inventario (compatibilidad) ──────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VentaRapida(int idProducto, int cantidad)
    {
        return await CobrarVenta(new CheckoutRequestDto
        {
            Items = new List<CheckoutItemDto> { new CheckoutItemDto { IdProducto = idProducto, Cantidad = cantidad } },
            MetodoPago = "Efectivo"
        });
    }

    // ── Get datos de un producto para editar (AJAX) ─────────────────────
    [HttpGet]
    public async Task<IActionResult> GetProducto(int id)
    {
        var p = await _db.Productos.FindAsync(id);
        if (p == null) return NotFound();
        return Json(new
        {
            p.IdProducto, p.Nombre, p.Categoria, p.Precio, p.Costo,
            p.Stock, p.StockMinimo, p.Descripcion, p.ImagenUrl
        });
    }
}
