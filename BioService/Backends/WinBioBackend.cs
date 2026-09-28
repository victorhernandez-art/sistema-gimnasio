using System.Runtime.InteropServices;
using SourceAFIS;

namespace BioService.Backends;

/// <summary>
/// Backend que usa Windows Biometric Framework (WinBio).
/// Funciona con CUALQUIER lector que tenga driver WBF/Windows Hello:
///   - DigitalPersona U.are.U 4500/5100/5200/5300
///   - Eikon Touch/Mini
///   - Lectores integrados de laptops
///   - Cualquier dispositivo con driver "Windows Hello Fingerprint"
///
/// Requiere ejecutarse con permisos de Administrador para acceso completo.
/// </summary>
public class WinBioBackend : IFingerBackend
{
    public string Name => "Windows Biometric Framework";
    public bool IsAvailable { get; private set; }

    private IntPtr _sessionHandle = IntPtr.Zero;

    // ── WinBio P/Invoke ─────────────────────────────────────────────
    private const int WINBIO_TYPE_FINGERPRINT = 0x00000008;
    private const int WINBIO_POOL_SYSTEM      = 0x00000001;
    private const int WINBIO_FLAG_DEFAULT      = 0x00000000;
    private const int WINBIO_FLAG_RAW          = 0x00000001;
    private const int WINBIO_ID_TYPE_SID       = 3;

    [DllImport("winbio.dll")] private static extern int WinBioOpenSession(
        int Factor, int PoolType, int Flags, IntPtr UnitArray, int UnitCount,
        IntPtr DatabaseId, out IntPtr SessionHandle);

    [DllImport("winbio.dll")] private static extern int WinBioCloseSession(IntPtr SessionHandle);

    [DllImport("winbio.dll")] private static extern int WinBioLocateSensor(
        IntPtr SessionHandle, out uint UnitId);

    [DllImport("winbio.dll")] private static extern int WinBioCaptureSample(
        IntPtr SessionHandle, int Purpose, int Flags,
        out uint UnitId, out IntPtr Sample, out int SampleSize,
        out int RejectDetail);

    [DllImport("winbio.dll")] private static extern int WinBioEnumBiometricUnits(
        int Factor, out IntPtr UnitSchemaArray, out int UnitCount);

    [DllImport("winbio.dll")] private static extern int WinBioFree(IntPtr Address);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WINBIO_UNIT_SCHEMA
    {
        public uint UnitId;
        public int PoolType;
        public uint BiometricFactor;
        public uint SensorSubType;
        public uint Capabilities;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string DeviceInstanceId;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string Description;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string Manufacturer;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string Model;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string SerialNumber;
        public WINBIO_VERSION FirmwareVersion;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WINBIO_VERSION
    {
        public uint MajorVersion;
        public uint MinorVersion;
    }

    // ── Constructor ─────────────────────────────────────────────────
    public WinBioBackend()
    {
        try
        {
            // Verificar que estamos en Windows y que hay al menos un sensor
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                IsAvailable = false;
                return;
            }

            int hr = WinBioEnumBiometricUnits(WINBIO_TYPE_FINGERPRINT, out var array, out int count);
            if (hr == 0 && count > 0)
            {
                WinBioFree(array);
                IsAvailable = true;
                Console.WriteLine($"[WinBio] {count} sensor(es) de huella detectado(s) ✓");
            }
            else
            {
                IsAvailable = false;
                Console.WriteLine("[WinBio] No se detectaron sensores de huella WBF");
            }
        }
        catch (Exception ex)
        {
            IsAvailable = false;
            Console.WriteLine($"[WinBio] Error al verificar: {ex.Message}");
        }
    }

    public Task<List<DeviceInfo>> GetDevicesAsync()
    {
        var devices = new List<DeviceInfo>();
        if (!IsAvailable) return Task.FromResult(devices);

        try
        {
            int hr = WinBioEnumBiometricUnits(WINBIO_TYPE_FINGERPRINT, out var array, out int count);
            if (hr != 0) return Task.FromResult(devices);

            int structSize = Marshal.SizeOf<WINBIO_UNIT_SCHEMA>();
            for (int i = 0; i < count; i++)
            {
                var ptr = IntPtr.Add(array, i * structSize);
                var schema = Marshal.PtrToStructure<WINBIO_UNIT_SCHEMA>(ptr);
                devices.Add(new DeviceInfo(
                    schema.UnitId.ToString(),
                    $"{schema.Manufacturer} {schema.Description}".Trim()
                ));
            }
            WinBioFree(array);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WinBio] Error enumerando dispositivos: {ex.Message}");
        }

        return Task.FromResult(devices);
    }

    public async Task<CaptureResult> CaptureAsync(CancellationToken ct = default)
    {
        if (!IsAvailable) return new CaptureResult(false, Msg: "WinBio no disponible.");

        return await Task.Run(() =>
        {
            try
            {
                // Abrir sesión (intentar RAW primero, luego DEFAULT)
                int hr = WinBioOpenSession(
                    WINBIO_TYPE_FINGERPRINT, WINBIO_POOL_SYSTEM, WINBIO_FLAG_RAW,
                    IntPtr.Zero, 0, IntPtr.Zero, out var session);
                if (hr != 0)
                {
                    hr = WinBioOpenSession(
                        WINBIO_TYPE_FINGERPRINT, WINBIO_POOL_SYSTEM, WINBIO_FLAG_DEFAULT,
                        IntPtr.Zero, 0, IntPtr.Zero, out session);
                }
                if (hr != 0) return new CaptureResult(false, Msg: $"Error abriendo sesión WinBio: 0x{hr:X8}");

                try
                {
                    // Localizar sensor
                    hr = WinBioLocateSensor(session, out uint unitId);
                    if (hr != 0) return new CaptureResult(false, Msg: "No se pudo localizar el sensor. Asegúrate de que el lector esté conectado.");

                    // Capturar muestra
                    const int WINBIO_PURPOSE_ENROLL = 8;
                    hr = WinBioCaptureSample(session, WINBIO_PURPOSE_ENROLL, WINBIO_FLAG_DEFAULT,
                        out _, out IntPtr sample, out int sampleSize, out int rejectDetail);

                    if (hr != 0)
                    {
                        string msg = rejectDetail != 0
                            ? $"Huella rechazada (calidad insuficiente). Inténtalo de nuevo."
                            : $"Error en captura: 0x{hr:X8}";
                        return new CaptureResult(false, Msg: msg);
                    }

                    // Copiar los datos del sample
                    byte[] imageData = new byte[sampleSize];
                    Marshal.Copy(sample, imageData, 0, sampleSize);
                    WinBioFree(sample);

                    // Intentar extraer template SourceAFIS desde la imagen
                    try
                    {
                        var fpImage = new FingerprintImage(imageData);
                        var template = new FingerprintTemplate(fpImage);
                        return new CaptureResult(true, Template: template.ToByteArray());
                    }
                    catch
                    {
                        // Fallback: almacenar el buffer BIR directamente como template biométrico
                        return new CaptureResult(true, Template: imageData);
                    }
                }
                finally
                {
                    WinBioCloseSession(session);
                }
            }
            catch (Exception ex)
            {
                return new CaptureResult(false, Msg: $"Error WinBio: {ex.Message}");
            }
        }, ct);
    }

    private readonly Dictionary<int, byte[]> _localTemplates = new();

    public void RegisterTemplate(int pin, byte[] template)
    {
        _localTemplates[pin] = template;
    }

    public Task<IdentifyResult> IdentifyAsync(byte[] probeTemplate, Dictionary<int, byte[]> storedTemplates)
    {
        // Actualizar cache local
        foreach (var (pin, bytes) in storedTemplates)
            _localTemplates[pin] = bytes;

        // Usar SourceAFIS para el matching 1:N
        return SourceAfisHelper.IdentifyAsync(probeTemplate, storedTemplates);
    }

    public async Task StartListeningAsync(Func<int, Task> onFingerIdentified, CancellationToken ct = default)
    {
        if (!IsAvailable) return;

        Console.WriteLine("[WinBio] Listening activo en espera de huellas en el sensor...");

        await Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    int hr = WinBioOpenSession(
                        WINBIO_TYPE_FINGERPRINT, WINBIO_POOL_SYSTEM, WINBIO_FLAG_DEFAULT,
                        IntPtr.Zero, 0, IntPtr.Zero, out var session);

                    if (hr != 0)
                    {
                        await Task.Delay(2000, ct);
                        continue;
                    }

                    try
                    {
                        hr = WinBioLocateSensor(session, out _);
                        if (hr != 0)
                        {
                            await Task.Delay(2000, ct);
                            continue;
                        }

                        const int WINBIO_PURPOSE_IDENTIFY = 2;
                        hr = WinBioCaptureSample(session, WINBIO_PURPOSE_IDENTIFY, WINBIO_FLAG_DEFAULT,
                            out _, out IntPtr sample, out int sampleSize, out int rejectDetail);

                        if (hr == 0 && sampleSize > 0)
                        {
                            byte[] imageData = new byte[sampleSize];
                            Marshal.Copy(sample, imageData, 0, sampleSize);
                            WinBioFree(sample);

                            var fpImage = new FingerprintImage(imageData);
                            var probeTemplate = new FingerprintTemplate(fpImage);

                            if (_localTemplates.Count > 0)
                            {
                                var identifyRes = await SourceAfisHelper.IdentifyAsync(probeTemplate.ToByteArray(), _localTemplates);
                                if (identifyRes.Ok && identifyRes.Pin.HasValue)
                                {
                                    Console.WriteLine($"[WinBio] Huella reconocida -> PIN {identifyRes.Pin.Value} (Score: {identifyRes.Score:F1})");
                                    await onFingerIdentified(identifyRes.Pin.Value);
                                }
                            }

                            await Task.Delay(1200, ct); // debounce
                        }
                        else
                        {
                            await Task.Delay(400, ct);
                        }
                    }
                    finally
                    {
                        WinBioCloseSession(session);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[WinBio Listener] {ex.Message}");
                    await Task.Delay(2000, ct);
                }
            }
        }, ct);
    }
}
