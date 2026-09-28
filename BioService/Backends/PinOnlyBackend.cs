namespace BioService.Backends;

/// <summary>
/// Backend de fallback: no usa hardware biométrico.
/// Solo identifica socios por su PIN (ingresado manualmente o vía
/// lector de barcode/teclado numérico).
///
/// Este backend SIEMPRE está disponible y se usa cuando no hay
/// ningún lector de huellas conectado.
/// </summary>
public class PinOnlyBackend : IFingerBackend
{
    public string Name => "PIN / Teclado (sin lector de huellas)";
    public bool IsAvailable => true; // Siempre disponible

    public Task<List<DeviceInfo>> GetDevicesAsync()
    {
        return Task.FromResult(new List<DeviceInfo>
        {
            new("PIN-INPUT", "Entrada manual / Teclado numérico / Lector de barcode")
        });
    }

    public Task<CaptureResult> CaptureAsync(CancellationToken ct = default)
    {
        // No hay captura de huella en este modo
        return Task.FromResult(new CaptureResult(
            false,
            Msg: "Este modo no usa lector de huellas. Ingresa la clave del socio manualmente."
        ));
    }

    public Task<IdentifyResult> IdentifyAsync(byte[] probeTemplate, Dictionary<int, byte[]> storedTemplates)
    {
        return Task.FromResult(new IdentifyResult(
            false,
            Msg: "Matching biométrico no disponible en modo PIN."
        ));
    }

    public async Task StartListeningAsync(Func<int, Task> onFingerIdentified, CancellationToken ct = default)
    {
        // En modo PIN no hay listener de hardware — mantener vivo
        Console.WriteLine("[PIN] Modo manual activo. Los socios ingresan su clave por teclado.");
        try
        {
            await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
        }
        catch (TaskCanceledException) { }
    }
}
