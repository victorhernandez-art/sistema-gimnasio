using ClosedXML.Excel;
using GymWeb.Models;
using Microsoft.EntityFrameworkCore;

namespace GymWeb.Helpers;

public static class ExcelReportHelper
{
    /// <summary>
    /// Obtiene los bytes de la imagen del logotipo del gimnasio.
    /// Busca primero en wwwroot/img/logo-custom.png y como respaldo en la BD (Configuracion.Logo).
    /// </summary>
    public static async Task<byte[]?> GetLogoBytesAsync(GymContext db, IWebHostEnvironment? env)
    {
        try
        {
            if (env != null)
            {
                var path = Path.Combine(env.WebRootPath, "img", "logo-custom.png");
                if (File.Exists(path))
                {
                    var bytes = await File.ReadAllBytesAsync(path);
                    if (bytes != null && bytes.Length > 0)
                        return bytes;
                }
            }

            var cfg = await db.Configuracions.FirstOrDefaultAsync();
            if (cfg?.Logo != null && cfg.Logo.Length > 0)
            {
                return cfg.Logo;
            }
        }
        catch { }

        return null;
    }

    /// <summary>
    /// Configura la hoja para impresión y vista de diseño de página en TAMAÑO CARTA (Letter) estándar,
    /// garantizando que no se corte y no tome por error medidas de impresoras térmicas de tickets (58mm/80mm).
    /// </summary>
    private static void ConfigurarPaginaCarta(IXLWorksheet ws, XLPageOrientation orientacion = XLPageOrientation.Portrait)
    {
        ws.PageSetup.PaperSize = XLPaperSize.LetterPaper; // Tamaño Carta (8.5 x 11 pulgadas / 215.9 x 279.4 mm)
        ws.PageSetup.PageOrientation = orientacion;
        ws.PageSetup.AdjustTo(100); // 100% de escala estándar en hoja Carta completa sin colapsar altura
        ws.PageSetup.Margins.Left = 0.5;
        ws.PageSetup.Margins.Right = 0.5;
        ws.PageSetup.Margins.Top = 0.6;
        ws.PageSetup.Margins.Bottom = 0.6;
        ws.PageSetup.Margins.Header = 0.3;
        ws.PageSetup.Margins.Footer = 0.3;
        ws.PageSetup.CenterHorizontally = true;
        ws.PageSetup.ShowGridlines = true;
    }

    /// <summary>
    /// Agrega el encabezado institucional y el logotipo en la hoja de cálculo de ClosedXML.
    /// </summary>
    private static int AgregarEncabezado(IXLWorksheet ws, string gymNombre, string tituloReporte, string periodo, byte[]? logoBytes, int totalCols = 5)
    {
        // Altura de las primeras 4 filas para dar espacio visual elegante
        ws.Row(1).Height = 22;
        ws.Row(2).Height = 20;
        ws.Row(3).Height = 18;
        ws.Row(4).Height = 10; // Espaciador

        int startDataRow = 5;

        // Si hay logo, se incrusta como imagen OpenXml dentro del XLSX
        if (logoBytes != null && logoBytes.Length > 0)
        {
            try
            {
                using var ms = new MemoryStream(logoBytes);
                var pic = ws.AddPicture(ms, "LogoGym");
                pic.MoveTo(ws.Cell(1, 1), 6, 6);
                pic.WithSize(130, 58);
            }
            catch { }

            // Texto a partir de la columna C combinado hasta totalCols
            int endCol = Math.Max(totalCols, 4);
            var r1 = ws.Range(1, 3, 1, endCol);
            r1.Merge();
            r1.Value = gymNombre.ToUpper();
            r1.Style.Font.Bold = true;
            r1.Style.Font.FontSize = 15;
            r1.Style.Font.FontColor = XLColor.FromHtml("#0F172A");

            var r2 = ws.Range(2, 3, 2, endCol);
            r2.Merge();
            r2.Value = tituloReporte;
            r2.Style.Font.Bold = true;
            r2.Style.Font.FontSize = 11;
            r2.Style.Font.FontColor = XLColor.FromHtml("#2563EB");

            var r3 = ws.Range(3, 3, 3, endCol);
            r3.Merge();
            r3.Value = $"Período: {periodo}  |  Fecha de emisión: {DateTime.Now:dd/MM/yyyy HH:mm}";
            r3.Style.Font.FontSize = 9;
            r3.Style.Font.FontColor = XLColor.FromHtml("#64748B");

            ws.Column(1).Width = 14;
            ws.Column(2).Width = 14;
        }
        else
        {
            // Sin logo: encabezado desde la columna A
            int endCol = Math.Max(totalCols, 4);
            var r1 = ws.Range(1, 1, 1, endCol);
            r1.Merge();
            r1.Value = gymNombre.ToUpper();
            r1.Style.Font.Bold = true;
            r1.Style.Font.FontSize = 15;
            r1.Style.Font.FontColor = XLColor.FromHtml("#0F172A");

            var r2 = ws.Range(2, 1, 2, endCol);
            r2.Merge();
            r2.Value = tituloReporte;
            r2.Style.Font.Bold = true;
            r2.Style.Font.FontSize = 11;
            r2.Style.Font.FontColor = XLColor.FromHtml("#2563EB");

            var r3 = ws.Range(3, 1, 3, endCol);
            r3.Merge();
            r3.Value = $"Período: {periodo}  |  Fecha de emisión: {DateTime.Now:dd/MM/yyyy HH:mm}";
            r3.Style.Font.FontSize = 9;
            r3.Style.Font.FontColor = XLColor.FromHtml("#64748B");
        }

        return startDataRow;
    }

    /// <summary>
    /// Genera el reporte XLSX nativo del Directorio de Socios.
    /// </summary>
    public static byte[] GenerarSociosXlsx(List<Socio> socios, string gymNombre, byte[]? logoBytes)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Socios");
        ConfigurarPaginaCarta(ws, XLPageOrientation.Portrait);

        int row = AgregarEncabezado(ws, gymNombre, "DIRECTORIO GENERAL DE SOCIOS", $"Total: {socios.Count} socios registrados", logoBytes, 6);

        // Encabezados de tabla
        string[] headers = { "ID", "Apellidos", "Nombre(s)", "Teléfono", "Estado", "Fecha Registro" };
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(row, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E293B");
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            if (i == 0 || i >= 3) cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }
        ws.Row(row).Height = 24;

        // Filas de datos
        int dataStartRow = row + 1;
        foreach (var s in socios)
        {
            row++;
            ws.Row(row).Height = 20;
            var esActivo = s.IdEstado == 1;

            ws.Cell(row, 1).Value = s.IdSocio;
            ws.Cell(row, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(row, 1).Style.Font.Bold = true;

            ws.Cell(row, 2).Value = $"{s.Paterno} {s.Materno}".Trim();
            ws.Cell(row, 3).Value = s.Nombre ?? "";
            ws.Cell(row, 3).Style.Font.Bold = true;

            ws.Cell(row, 4).Value = s.Telefono ?? "—";
            ws.Cell(row, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            var cellEstado = ws.Cell(row, 5);
            cellEstado.Value = esActivo ? "ACTIVO" : "INACTIVO";
            cellEstado.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cellEstado.Style.Font.Bold = true;
            cellEstado.Style.Font.FontColor = esActivo ? XLColor.FromHtml("#16A34A") : XLColor.FromHtml("#DC2626");

            var cellFecha = ws.Cell(row, 6);
            if (s.FechaCreacion.HasValue)
            {
                cellFecha.Value = s.FechaCreacion.Value.ToString("dd/MM/yyyy");
            }
            else
            {
                cellFecha.Value = "—";
            }
            cellFecha.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            // Zebra striping
            if ((row - dataStartRow) % 2 == 1)
            {
                ws.Range(row, 1, row, headers.Length).Style.Fill.BackgroundColor = XLColor.FromHtml("#F8FAFC");
            }
        }

        // Bordes de la tabla
        var dataRange = ws.Range(dataStartRow - 1, 1, Math.Max(row, dataStartRow), headers.Length);
        dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        dataRange.Style.Border.InsideBorderColor = XLColor.FromHtml("#E2E8F0");
        dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
        dataRange.Style.Border.OutsideBorderColor = XLColor.FromHtml("#1E293B");

        ws.Column(1).Width = 8;
        ws.Column(2).Width = 18;
        ws.Column(3).Width = 18;
        ws.Column(4).Width = 14;
        ws.Column(5).Width = 11;
        ws.Column(6).Width = 13;

        using var msOut = new MemoryStream();
        wb.SaveAs(msOut);
        return msOut.ToArray();
    }

    /// <summary>
    /// Genera el reporte XLSX nativo del Historial de Pagos.
    /// </summary>
    public static byte[] GenerarPagosXlsx(List<Pago> pagos, string gymNombre, string periodo, byte[]? logoBytes)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Pagos");
        ConfigurarPaginaCarta(ws, XLPageOrientation.Portrait);

        int row = AgregarEncabezado(ws, gymNombre, "HISTORIAL GENERAL DE PAGOS Y COBROS", periodo, logoBytes, 6);

        // Encabezados
        string[] headers = { "Fecha", "Socio / Cliente", "Plan / Concepto", "Método de Pago", "Monto", "Observaciones" };
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(row, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E293B");
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            if (i == 0 || i == 3) cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            if (i == 4) cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        }
        ws.Row(row).Height = 24;

        int dataStartRow = row + 1;
        decimal total = 0;

        foreach (var p in pagos)
        {
            row++;
            ws.Row(row).Height = 20;
            total += p.Monto;

            var cellFecha = ws.Cell(row, 1);
            cellFecha.Value = p.FechaPago.ToString("dd/MM/yyyy");
            cellFecha.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            var cellSocio = ws.Cell(row, 2);
            cellSocio.Value = $"{p.IdSocioNavigation?.Nombre} {p.IdSocioNavigation?.Paterno}".Trim();
            cellSocio.Style.Font.Bold = true;

            ws.Cell(row, 3).Value = p.IdMembresiaNavigation?.Nombre ?? "Membresía / Acceso";

            var cellMetodo = ws.Cell(row, 4);
            cellMetodo.Value = p.MetodoPago ?? "Efectivo";
            cellMetodo.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            var cellMonto = ws.Cell(row, 5);
            cellMonto.Value = (double)p.Monto;
            cellMonto.Style.NumberFormat.Format = "$#,##0.00";
            cellMonto.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            ws.Cell(row, 6).Value = p.Notas ?? "—";

            if ((row - dataStartRow) % 2 == 1)
            {
                ws.Range(row, 1, row, headers.Length).Style.Fill.BackgroundColor = XLColor.FromHtml("#F8FAFC");
            }
        }

        // Fila Total
        row++;
        ws.Row(row).Height = 24;
        var rangeTotalLabel = ws.Range(row, 1, row, 4);
        rangeTotalLabel.Merge();
        rangeTotalLabel.Value = $"TOTAL RECAUDADO ({pagos.Count} pagos):";
        rangeTotalLabel.Style.Font.Bold = true;
        rangeTotalLabel.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        rangeTotalLabel.Style.Fill.BackgroundColor = XLColor.FromHtml("#E2E8F0");

        var cellTotalMonto = ws.Cell(row, 5);
        cellTotalMonto.Value = (double)total;
        cellTotalMonto.Style.NumberFormat.Format = "$#,##0.00";
        cellTotalMonto.Style.Font.Bold = true;
        cellTotalMonto.Style.Font.FontColor = XLColor.FromHtml("#16A34A");
        cellTotalMonto.Style.Fill.BackgroundColor = XLColor.FromHtml("#E2E8F0");
        cellTotalMonto.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

        ws.Cell(row, 6).Style.Fill.BackgroundColor = XLColor.FromHtml("#E2E8F0");

        // Bordes
        var dataRange = ws.Range(dataStartRow - 1, 1, row, headers.Length);
        dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        dataRange.Style.Border.InsideBorderColor = XLColor.FromHtml("#E2E8F0");
        dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
        dataRange.Style.Border.OutsideBorderColor = XLColor.FromHtml("#1E293B");

        ws.Column(1).Width = 13;
        ws.Column(2).Width = 22;
        ws.Column(3).Width = 16;
        ws.Column(4).Width = 13;
        ws.Column(5).Width = 12;
        ws.Column(6).Width = 16;

        using var msOut = new MemoryStream();
        wb.SaveAs(msOut);
        return msOut.ToArray();
    }

    /// <summary>
    /// Genera el reporte XLSX nativo del Corte de Caja (Día o Mes).
    /// </summary>
    public static byte[] GenerarCorteXlsx(List<Registro> visitas, List<Detallesalidum> ventas, List<Pago> pagos,
                                          string gymNombre, string tipoCorte, string periodoLabel, byte[]? logoBytes)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Corte");
        ConfigurarPaginaCarta(ws, XLPageOrientation.Portrait);

        decimal totVisitas = visitas.Sum(v => v.PrecioVisita);
        decimal totVentas  = ventas.Sum(d => (d.PrecioUnitario ?? 0) * d.Cantidad);
        decimal totPagos   = pagos.Sum(p => p.Monto);
        decimal totGeneral = totVisitas + totVentas + totPagos;

        int row = AgregarEncabezado(ws, gymNombre, $"CORTE DE CAJA DEL {tipoCorte.ToUpper()}", periodoLabel, logoBytes, 5);

        // Tarjetas Resumen Ejecutivo (KPIs)
        row++;
        ws.Row(row).Height = 35;
        
        ws.Cell(row, 1).Value = "VISITAS DE DÍA\n" + totVisitas.ToString("C2");
        ws.Cell(row, 1).Style.Alignment.WrapText = true;
        ws.Cell(row, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#EFF6FF");
        ws.Cell(row, 1).Style.Font.FontColor = XLColor.FromHtml("#1E40AF");
        ws.Cell(row, 1).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

        ws.Cell(row, 2).Value = "VENTA PRODUCTOS\n" + totVentas.ToString("C2");
        ws.Cell(row, 2).Style.Alignment.WrapText = true;
        ws.Cell(row, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.Cell(row, 2).Style.Font.Bold = true;
        ws.Cell(row, 2).Style.Fill.BackgroundColor = XLColor.FromHtml("#F0FDF4");
        ws.Cell(row, 2).Style.Font.FontColor = XLColor.FromHtml("#166534");
        ws.Cell(row, 2).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

        ws.Cell(row, 3).Value = "MEMBRESÍAS\n" + totPagos.ToString("C2");
        ws.Cell(row, 3).Style.Alignment.WrapText = true;
        ws.Cell(row, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.Cell(row, 3).Style.Font.Bold = true;
        ws.Cell(row, 3).Style.Fill.BackgroundColor = XLColor.FromHtml("#FAF5FF");
        ws.Cell(row, 3).Style.Font.FontColor = XLColor.FromHtml("#6B21A8");
        ws.Cell(row, 3).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

        var rangeTotalKpi = ws.Range(row, 4, row, 5);
        rangeTotalKpi.Merge();
        rangeTotalKpi.Value = "TOTAL RECAUDADO\n" + totGeneral.ToString("C2");
        rangeTotalKpi.Style.Alignment.WrapText = true;
        rangeTotalKpi.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        rangeTotalKpi.Style.Font.Bold = true;
        rangeTotalKpi.Style.Fill.BackgroundColor = XLColor.FromHtml("#0F172A");
        rangeTotalKpi.Style.Font.FontColor = XLColor.FromHtml("#38BDF8");
        rangeTotalKpi.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

        row += 2; // Espacio

        // Sección 1: Visitas de Día
        ws.Cell(row, 1).Value = $"1. Visitas de Día ({visitas.Count})";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 1).Style.Font.FontSize = 11;
        ws.Cell(row, 1).Style.Font.FontColor = XLColor.FromHtml("#1E293B");
        row++;

        string[] headersVisitas = { "Fecha", "Hora", "Visitante", "Monto" };
        for (int i = 0; i < headersVisitas.Length; i++)
        {
            var c = ws.Cell(row, i + 1);
            c.Value = headersVisitas[i];
            c.Style.Font.Bold = true;
            c.Style.Font.FontColor = XLColor.White;
            c.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E293B");
            if (i <= 1) c.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            if (i == 3) c.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        }

        int startSec1 = row + 1;
        if (visitas.Count == 0)
        {
            row++;
            ws.Cell(row, 1).Value = "Sin visitas registradas en este período.";
            ws.Range(row, 1, row, headersVisitas.Length).Merge();
            ws.Cell(row, 1).Style.Font.Italic = true;
            ws.Cell(row, 1).Style.Font.FontColor = XLColor.FromHtml("#94A3B8");
        }
        else
        {
            foreach (var v in visitas)
            {
                row++;
                ws.Cell(row, 1).Value = v.FechaCreacion?.ToString("dd/MM/yyyy") ?? "—";
                ws.Cell(row, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                ws.Cell(row, 2).Value = v.FechaCreacion?.ToString("HH:mm") ?? "—";
                ws.Cell(row, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                ws.Cell(row, 3).Value = string.IsNullOrWhiteSpace(v.NombreVisita) ? "Visita individual" : v.NombreVisita;

                var cMonto = ws.Cell(row, 4);
                cMonto.Value = (double)v.PrecioVisita;
                cMonto.Style.NumberFormat.Format = "$#,##0.00";
                cMonto.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                if ((row - startSec1) % 2 == 1)
                    ws.Range(row, 1, row, headersVisitas.Length).Style.Fill.BackgroundColor = XLColor.FromHtml("#F8FAFC");
            }
        }
        row++;
        ws.Range(row, 1, row, 3).Merge().Value = "Subtotal Visitas:";
        ws.Range(row, 1, row, 3).Style.Font.Bold = true;
        ws.Range(row, 1, row, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        ws.Range(row, 1, row, 3).Style.Fill.BackgroundColor = XLColor.FromHtml("#F1F5F9");
        var cSubVis = ws.Cell(row, 4);
        cSubVis.Value = (double)totVisitas;
        cSubVis.Style.NumberFormat.Format = "$#,##0.00";
        cSubVis.Style.Font.Bold = true;
        cSubVis.Style.Fill.BackgroundColor = XLColor.FromHtml("#F1F5F9");

        row += 2; // Espacio

        // Sección 2: Venta de Productos
        ws.Cell(row, 1).Value = $"2. Venta de Productos ({ventas.Count} partidas)";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 1).Style.Font.FontSize = 11;
        ws.Cell(row, 1).Style.Font.FontColor = XLColor.FromHtml("#1E293B");
        row++;

        string[] headersVentas = { "Fecha", "Producto", "Cant.", "P. Unitario", "Importe" };
        for (int i = 0; i < headersVentas.Length; i++)
        {
            var c = ws.Cell(row, i + 1);
            c.Value = headersVentas[i];
            c.Style.Font.Bold = true;
            c.Style.Font.FontColor = XLColor.White;
            c.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E293B");
            if (i == 0 || i == 2) c.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            if (i >= 3) c.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        }

        int startSec2 = row + 1;
        if (ventas.Count == 0)
        {
            row++;
            ws.Cell(row, 1).Value = "Sin ventas de productos registradas en este período.";
            ws.Range(row, 1, row, headersVentas.Length).Merge();
            ws.Cell(row, 1).Style.Font.Italic = true;
            ws.Cell(row, 1).Style.Font.FontColor = XLColor.FromHtml("#94A3B8");
        }
        else
        {
            foreach (var d in ventas)
            {
                row++;
                decimal pu = d.PrecioUnitario ?? 0;
                decimal sub = pu * d.Cantidad;

                ws.Cell(row, 1).Value = d.IdSalidaNavigation?.FechaCreacion?.ToString("dd/MM/yyyy HH:mm") ?? "—";
                ws.Cell(row, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                ws.Cell(row, 2).Value = d.IdProductoNavigation?.Nombre ?? $"Producto #{d.IdProducto}";

                ws.Cell(row, 3).Value = d.Cantidad;
                ws.Cell(row, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                var cPu = ws.Cell(row, 4);
                cPu.Value = (double)pu;
                cPu.Style.NumberFormat.Format = "$#,##0.00";
                cPu.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                var cSub = ws.Cell(row, 5);
                cSub.Value = (double)sub;
                cSub.Style.NumberFormat.Format = "$#,##0.00";
                cSub.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                if ((row - startSec2) % 2 == 1)
                    ws.Range(row, 1, row, headersVentas.Length).Style.Fill.BackgroundColor = XLColor.FromHtml("#F8FAFC");
            }
        }
        row++;
        ws.Range(row, 1, row, 4).Merge().Value = "Subtotal Productos:";
        ws.Range(row, 1, row, 4).Style.Font.Bold = true;
        ws.Range(row, 1, row, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        ws.Range(row, 1, row, 4).Style.Fill.BackgroundColor = XLColor.FromHtml("#F1F5F9");
        var cSubProd = ws.Cell(row, 5);
        cSubProd.Value = (double)totVentas;
        cSubProd.Style.NumberFormat.Format = "$#,##0.00";
        cSubProd.Style.Font.Bold = true;
        cSubProd.Style.Fill.BackgroundColor = XLColor.FromHtml("#F1F5F9");

        row += 2; // Espacio

        // Sección 3: Cobros de Membresías
        ws.Cell(row, 1).Value = $"3. Cobros de Membresías ({pagos.Count})";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 1).Style.Font.FontSize = 11;
        ws.Cell(row, 1).Style.Font.FontColor = XLColor.FromHtml("#1E293B");
        row++;

        string[] headersPagos = { "Fecha", "Socio", "Membresía", "Método", "Monto" };
        for (int i = 0; i < headersPagos.Length; i++)
        {
            var c = ws.Cell(row, i + 1);
            c.Value = headersPagos[i];
            c.Style.Font.Bold = true;
            c.Style.Font.FontColor = XLColor.White;
            c.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E293B");
            if (i == 0 || i == 3) c.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            if (i == 4) c.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        }

        int startSec3 = row + 1;
        if (pagos.Count == 0)
        {
            row++;
            ws.Cell(row, 1).Value = "Sin pagos de membresías registrados en este período.";
            ws.Range(row, 1, row, headersPagos.Length).Merge();
            ws.Cell(row, 1).Style.Font.Italic = true;
            ws.Cell(row, 1).Style.Font.FontColor = XLColor.FromHtml("#94A3B8");
        }
        else
        {
            foreach (var p in pagos)
            {
                row++;
                ws.Cell(row, 1).Value = p.FechaPago.ToString("dd/MM/yyyy");
                ws.Cell(row, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                ws.Cell(row, 2).Value = $"{p.IdSocioNavigation?.Nombre} {p.IdSocioNavigation?.Paterno}".Trim();
                ws.Cell(row, 3).Value = p.IdMembresiaNavigation?.Nombre ?? "Membresía";

                ws.Cell(row, 4).Value = p.MetodoPago ?? "Efectivo";
                ws.Cell(row, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                var cM = ws.Cell(row, 5);
                cM.Value = (double)p.Monto;
                cM.Style.NumberFormat.Format = "$#,##0.00";
                cM.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                if ((row - startSec3) % 2 == 1)
                    ws.Range(row, 1, row, headersPagos.Length).Style.Fill.BackgroundColor = XLColor.FromHtml("#F8FAFC");
            }
        }
        row++;
        ws.Range(row, 1, row, 4).Merge().Value = "Subtotal Membresías:";
        ws.Range(row, 1, row, 4).Style.Font.Bold = true;
        ws.Range(row, 1, row, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        ws.Range(row, 1, row, 4).Style.Fill.BackgroundColor = XLColor.FromHtml("#F1F5F9");
        var cSubMem = ws.Cell(row, 5);
        cSubMem.Value = (double)totPagos;
        cSubMem.Style.NumberFormat.Format = "$#,##0.00";
        cSubMem.Style.Font.Bold = true;
        cSubMem.Style.Fill.BackgroundColor = XLColor.FromHtml("#F1F5F9");

        // Fila Total General
        row += 2;
        ws.Row(row).Height = 26;
        ws.Range(row, 1, row, 4).Merge().Value = "TOTAL GENERAL DEL CORTE:";
        ws.Range(row, 1, row, 4).Style.Font.Bold = true;
        ws.Range(row, 1, row, 4).Style.Font.FontSize = 11;
        ws.Range(row, 1, row, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        ws.Range(row, 1, row, 4).Style.Fill.BackgroundColor = XLColor.FromHtml("#E2E8F0");
        ws.Range(row, 1, row, 4).Style.Border.TopBorder = XLBorderStyleValues.Medium;
        ws.Range(row, 1, row, 4).Style.Border.BottomBorder = XLBorderStyleValues.Double;

        var cTotGen = ws.Cell(row, 5);
        cTotGen.Value = (double)totGeneral;
        cTotGen.Style.NumberFormat.Format = "$#,##0.00";
        cTotGen.Style.Font.Bold = true;
        cTotGen.Style.Font.FontSize = 12;
        cTotGen.Style.Font.FontColor = XLColor.FromHtml("#16A34A");
        cTotGen.Style.Fill.BackgroundColor = XLColor.FromHtml("#E2E8F0");
        cTotGen.Style.Border.TopBorder = XLBorderStyleValues.Medium;
        cTotGen.Style.Border.BottomBorder = XLBorderStyleValues.Double;

        ws.Column(1).Width = 13;
        ws.Column(2).Width = 26;
        ws.Column(3).Width = 11;
        ws.Column(4).Width = 13;
        ws.Column(5).Width = 15;

        using var msOut = new MemoryStream();
        wb.SaveAs(msOut);
        return msOut.ToArray();
    }
}
