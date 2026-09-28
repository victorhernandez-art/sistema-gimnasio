$publish = "$PSScriptRoot\Publish"

# Vamos a compilar y probar un mini ejecutable con dotnet
$code = @'
using System;
using System.IO;
using ClosedXML.Excel;

class Program
{
    static void Main()
    {
        // Prueba 1: FitToPages(1, 1)
        using (var wb = new XLWorkbook())
        {
            var ws = wb.Worksheets.Add("P1");
            ws.PageSetup.PaperSize = XLPaperSize.LetterPaper;
            ws.PageSetup.PageOrientation = XLPageOrientation.Portrait;
            ws.PageSetup.FitToPages(1, 1);
            wb.SaveAs("test_p1.xlsx");
        }

        // Prueba 2: Sin FitToPages, solo PaperSize y Margenes
        using (var wb = new XLWorkbook())
        {
            var ws = wb.Worksheets.Add("P2");
            ws.PageSetup.PaperSize = XLPaperSize.LetterPaper;
            ws.PageSetup.PageOrientation = XLPageOrientation.Portrait;
            ws.PageSetup.AdjustTo(100);
            wb.SaveAs("test_p2.xlsx");
        }
    }
}
'@

# Vamos a probar cómo serializa ClosedXML
