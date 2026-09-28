using System.Management;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace GymWeb.Helpers;

public class BiometricDeviceInfo
{
    public string Name { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty; // WindowsHello, DigitalPersona, ZKTeco, SecuGen, Generic
    public string Status { get; set; } = string.Empty;   // Ready, NeedsDriver, Info
    public string Recommendation { get; set; } = string.Empty;
}

public class HardwareDiagnosticResult
{
    public bool HasPhysicalUsbDevice { get; set; }
    public bool HasWbfSensor { get; set; }
    public string ActiveBackend { get; set; } = string.Empty;
    public List<BiometricDeviceInfo> Devices { get; set; } = new();
    public string SummaryMessage { get; set; } = string.Empty;
}

[SupportedOSPlatform("windows")]
public static class HardwareDetector
{
    private const int WINBIO_TYPE_FINGERPRINT = 0x00000008;

    [DllImport("winbio.dll")]
    private static extern int WinBioEnumBiometricUnits(int Factor, out IntPtr UnitSchemaArray, out int UnitCount);

    [DllImport("winbio.dll")]
    private static extern int WinBioFree(IntPtr Address);

    public static HardwareDiagnosticResult RunDiagnostics(string currentBackendName)
    {
        var result = new HardwareDiagnosticResult
        {
            ActiveBackend = currentBackendName
        };

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            result.SummaryMessage = "El servicio biométrico requiere Windows.";
            return result;
        }

        // 1. Verificar WBF (Windows Biometric Framework)
        int wbfCount = 0;
        try
        {
            int hr = WinBioEnumBiometricUnits(WINBIO_TYPE_FINGERPRINT, out var array, out int count);
            if (hr == 0 && count > 0)
            {
                wbfCount = count;
                result.HasWbfSensor = true;
                WinBioFree(array);
            }
        }
        catch { }

        // 2. Escanear dispositivos PnP de Windows (USB y Biometric)
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT DeviceID, PNPClass, Description, Name, Manufacturer FROM Win32_PnPEntity WHERE " +
                "PNPClass = 'Biometric' OR " +
                "Description LIKE '%Fingerprint%' OR Name LIKE '%Fingerprint%' OR " +
                "Description LIKE '%Biometric%' OR Name LIKE '%Biometric%' OR " +
                "Description LIKE '%ZKTeco%' OR Name LIKE '%ZKTeco%' OR Description LIKE '%ZK9500%' OR Description LIKE '%ZK4500%' OR " +
                "Description LIKE '%DigitalPersona%' OR Name LIKE '%DigitalPersona%' OR Description LIKE '%U.are.U%' OR " +
                "Description LIKE '%SecuGen%' OR Name LIKE '%SecuGen%' OR " +
                "DeviceID LIKE '%VID_1B55%' OR DeviceID LIKE '%VID_05BA%' OR DeviceID LIKE '%VID_1162%' OR DeviceID LIKE '%VID_0A5C%'");

            using var collection = searcher.Get();
            foreach (ManagementObject obj in collection)
            {
                var name = obj["Name"]?.ToString() ?? obj["Description"]?.ToString() ?? "Dispositivo Biométrico";
                var devId = obj["DeviceID"]?.ToString() ?? "";
                var pnpClass = obj["PNPClass"]?.ToString() ?? "";

                var info = new BiometricDeviceInfo
                {
                    Name = name,
                    DeviceId = devId
                };

                // Clasificar por fabricante / tipo
                if (devId.Contains("VID_05BA", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("DigitalPersona", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("U.are.U", StringComparison.OrdinalIgnoreCase))
                {
                    info.Category = "DigitalPersona";
                    if (pnpClass.Equals("Biometric", StringComparison.OrdinalIgnoreCase) || wbfCount > 0)
                    {
                        info.Status = "Ready";
                        info.Recommendation = "Lector DigitalPersona reconocido y listo para capturar huellas.";
                    }
                    else
                    {
                        info.Status = "NeedsDriver";
                        info.Recommendation = "Lector DigitalPersona detectado por USB. Para activarlo universalmente, instala el 'DigitalPersona WBF Driver'.";
                    }
                }
                else if (devId.Contains("VID_1B55", StringComparison.OrdinalIgnoreCase) ||
                         devId.Contains("VID_0A5C", StringComparison.OrdinalIgnoreCase) ||
                         name.Contains("ZK9500", StringComparison.OrdinalIgnoreCase) ||
                         name.Contains("ZK4500", StringComparison.OrdinalIgnoreCase) ||
                         name.Contains("ZKTeco", StringComparison.OrdinalIgnoreCase))
                {
                    info.Category = "ZKTeco";
                    info.Status = "NeedsDriver";
                    info.Recommendation = "Lector ZKTeco conectado por USB. Requiere el SDK ZKFinger.";
                }
                else if (devId.Contains("VID_1162", StringComparison.OrdinalIgnoreCase) ||
                         name.Contains("SecuGen", StringComparison.OrdinalIgnoreCase))
                {
                    info.Category = "SecuGen";
                    info.Status = pnpClass.Equals("Biometric", StringComparison.OrdinalIgnoreCase) ? "Ready" : "NeedsDriver";
                    info.Recommendation = info.Status == "Ready" ? "Lector SecuGen listo." : "Instala el controlador SecuGen WBF.";
                }
                else if (pnpClass.Equals("Biometric", StringComparison.OrdinalIgnoreCase))
                {
                    info.Category = "WindowsHello";
                    info.Status = "Ready";
                    info.Recommendation = "Lector compatible con Windows Hello detectado y listo para capturar huellas.";
                }
                else
                {
                    info.Category = "Generic";
                    info.Status = "Info";
                    info.Recommendation = "Dispositivo biométrico USB detectado.";
                }

                result.Devices.Add(info);
                result.HasPhysicalUsbDevice = true;
            }
        }
        catch (Exception ex)
        {
            result.SummaryMessage = $"Aviso de consulta: {ex.Message}";
        }

        // Generar mensaje resumen
        if (result.HasWbfSensor || result.Devices.Any(d => d.Status == "Ready"))
        {
            result.SummaryMessage = "Lector de huellas conectado y listo para operar.";
        }
        else if (result.HasPhysicalUsbDevice)
        {
            var firstDev = result.Devices.First();
            result.SummaryMessage = $"Dispositivo conectado: {firstDev.Name}. {firstDev.Recommendation}";
        }
        else
        {
            result.SummaryMessage = "No se detecta ningún lector de huellas conectado por USB. (Modo PIN/Teclado o Simulación activo)";
        }

        return result;
    }
}
