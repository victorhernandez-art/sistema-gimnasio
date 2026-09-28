namespace BioService.Backends;

/// <summary>
/// Resultado de una captura de huella dactilar.
/// </summary>
public record CaptureResult(bool Ok, byte[]? Template = null, string? Msg = null);

/// <summary>
/// Resultado de verificación 1:N.
/// </summary>
public record IdentifyResult(bool Ok, int? Pin = null, double Score = 0, string? Msg = null);

/// <summary>
/// Información de un dispositivo biométrico detectado.
/// </summary>
public record DeviceInfo(string Id, string Nombre);

/// <summary>
/// Interfaz común para cualquier backend de lectura de huellas.
/// Permite intercambiar la implementación sin tocar el resto del código.
/// </summary>
public interface IFingerBackend
{
    /// <summary>Nombre legible del backend (para UI).</summary>
    string Name { get; }

    /// <summary>true si el backend tiene hardware disponible.</summary>
    bool IsAvailable { get; }

    /// <summary>Lista los dispositivos biométricos conectados.</summary>
    Task<List<DeviceInfo>> GetDevicesAsync();

    /// <summary>
    /// Captura una huella y devuelve el template SourceAFIS serializado.
    /// </summary>
    Task<CaptureResult> CaptureAsync(CancellationToken ct = default);

    /// <summary>
    /// Verifica la huella capturada contra una lista de templates almacenados.
    /// Devuelve el PIN del socio identificado.
    /// </summary>
    Task<IdentifyResult> IdentifyAsync(byte[] probeTemplate, Dictionary<int, byte[]> storedTemplates);

    /// <summary>
    /// Inicia la escucha continua del lector.
    /// Cuando identifica una huella emite el PIN vía el callback.
    /// </summary>
    Task StartListeningAsync(Func<int, Task> onFingerIdentified, CancellationToken ct = default);
}
