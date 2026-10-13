using System;
using System.Threading;
using System.Threading.Tasks;

namespace DigitalPersonaBridge;

class Program
{
    static async Task Main(string[] args)
    {
        Console.Title = "DigitalPersona Bridge - WebSocket ws://localhost:8080";
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        int puerto = 8080;
        foreach (var arg in args)
        {
            if (arg.StartsWith("--port=", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(arg.Substring(7), out var p))
            {
                puerto = p;
            }
        }

        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine(@"
╔════════════════════════════════════════════════════════════════╗
║     DIGITALPERSONA U.ARE.U 4500 — PUENTE LOCAL WEBSOCKET       ║
║            Integración de Hardware Biométrico USB              ║
╚════════════════════════════════════════════════════════════════╝
");
        Console.ResetColor();

        Console.WriteLine("Iniciando manejador de hardware DigitalPersona...");
        var handler = new DigitalPersonaHandler();

        var wsServer = new WebSocketServer(puerto, handler);

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            Console.WriteLine("\nCerrando puente biométrico...");
            cts.Cancel();
            wsServer.Stop();
        };

        try
        {
            await wsServer.StartAsync(cts.Token);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n[ERROR CRITICO] No se pudo iniciar en puerto {puerto}: {ex.Message}");
            Console.WriteLine("Asegúrese de que ningún otro proceso esté ocupando el puerto 8080.");
            Console.ResetColor();
        }
    }
}
