using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DigitalPersonaBridge;

public static class WhatsAppAutomationHelper
{
    private const int VK_RETURN = 0x0D;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const int SW_RESTORE = 9;

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    /// <summary>
    /// Busca la ventana de WhatsApp Web, la enfoca y presiona automáticamente la tecla Enter tras la espera indicada.
    /// </summary>
    public static async Task<bool> PresionarEnterWhatsAppAsync(int delayMs = 5500, CancellationToken ct = default)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"[WHATSAPP AUTO] Esperando {delayMs}ms para que WhatsApp Web termine de cargar el mensaje...");
        Console.ResetColor();

        // 1. Esperar a que WhatsApp Web cargue el contacto y ponga el texto en el borrador
        if (delayMs > 0)
        {
            await Task.Delay(delayMs, ct);
        }

        // 2. Localizar ventana de WhatsApp Web
        IntPtr targetHwnd = BuscarVentanaWhatsApp();

        if (targetHwnd != IntPtr.Zero)
        {
            Console.WriteLine($"[WHATSAPP AUTO] Ventana de WhatsApp encontrada (Handle: 0x{targetHwnd:X8}). Enfocando...");
            if (IsIconic(targetHwnd))
            {
                ShowWindow(targetHwnd, SW_RESTORE);
            }
            SetForegroundWindow(targetHwnd);
            await Task.Delay(250, ct);
        }
        else
        {
            Console.WriteLine("[WHATSAPP AUTO] Usando la ventana actualmente en primer plano.");
        }

        // 3. Simular pulsación de tecla Enter a nivel del Sistema Operativo Windows
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("[WHATSAPP AUTO] Disparando tecla ENTER a nivel de sistema operativo...");
        Console.ResetColor();

        SimularPulsacionEnter();

        // Pequeño retardo y confirmación
        await Task.Delay(300, ct);
        return true;
    }

    private static void SimularPulsacionEnter()
    {
        try
        {
            // Pulsar tecla Enter (KeyDown)
            keybd_event(VK_RETURN, 0, 0, UIntPtr.Zero);
            Thread.Sleep(60);
            // Soltar tecla Enter (KeyUp)
            keybd_event(VK_RETURN, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WHATSAPP AUTO ERROR] Error al simular tecla: {ex.Message}");
        }
    }

    private static IntPtr BuscarVentanaWhatsApp()
    {
        IntPtr found = IntPtr.Zero;

        EnumWindows((hWnd, lParam) =>
        {
            int length = GetWindowTextLength(hWnd);
            if (length > 0)
            {
                var sb = new StringBuilder(length + 1);
                GetWindowText(hWnd, sb, sb.Capacity);
                string title = sb.ToString();

                // Buscar títulos típicos de WhatsApp Web o la ventana singleton creada por GymWeb
                if (title.Contains("WhatsApp", StringComparison.OrdinalIgnoreCase) ||
                    title.Contains("whatsapp_taller_win", StringComparison.OrdinalIgnoreCase))
                {
                    found = hWnd;
                    return false; // Detener enumeración
                }
            }
            return true;
        }, IntPtr.Zero);

        return found;
    }
}
