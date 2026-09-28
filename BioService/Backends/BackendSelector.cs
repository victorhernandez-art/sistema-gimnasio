using System.Runtime.InteropServices;

namespace BioService.Backends;

/// <summary>
/// Auto-detecta el mejor backend biométrico disponible en el sistema.
/// Orden de prioridad:
///   1. WinBio (Windows Biometric Framework) — mejor opción, universal
///   2. PIN/Teclado — fallback cuando no hay lector
/// </summary>
public static class BackendSelector
{
    public static IFingerBackend DetectBest()
    {
        Console.WriteLine("════════════════════════════════════════════════════");
        Console.WriteLine("  Detectando backend biométrico...");
        Console.WriteLine("════════════════════════════════════════════════════");

        // 1. Intentar ZKTeco SDK / USB
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                var zkteco = new ZKTecoBackend();
                if (zkteco.IsAvailable)
                {
                    Console.WriteLine("  ✓ Usando: ZKTeco Native SDK / USB");
                    Console.WriteLine("    Compatible con: ZK9500, ZK4500, SLK20R, ZK9700");
                    Console.WriteLine("════════════════════════════════════════════════════\n");
                    return zkteco;
                }
                Console.WriteLine($"  ✗ ZKTeco: {zkteco.DiagnosticMessage}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ✗ ZKTeco: {ex.Message}");
            }

            // 2. Intentar WinBio (Windows Biometric Framework)
            try
            {
                var winbio = new WinBioBackend();
                if (winbio.IsAvailable)
                {
                    Console.WriteLine("  ✓ Usando: Windows Biometric Framework");
                    Console.WriteLine("    Compatible con: DigitalPersona, Eikon, Windows Hello, etc.");
                    Console.WriteLine("════════════════════════════════════════════════════\n");
                    return winbio;
                }
                Console.WriteLine("  ✗ WinBio: no se detectaron lectores WBF");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ✗ WinBio: {ex.Message}");
            }
        }
        else
        {
            Console.WriteLine("  ✗ Lector biométrico: solo disponible en Windows");
        }

        // 3. Fallback: PIN / teclado
        Console.WriteLine("  → Usando: Modo PIN / Teclado (sin lector de huellas)");
        Console.WriteLine("    Los socios ingresan su clave manualmente.");
        Console.WriteLine("    Para habilitar huellas, conecta un lector compatible (ZK9500/ZK4500).");
        Console.WriteLine("════════════════════════════════════════════════════\n");
        return new PinOnlyBackend();
    }
}
