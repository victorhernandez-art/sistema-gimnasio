namespace BioService.Backends;

/// <summary>
/// Selector de backend dinámico que detecta dispositivos conectados en caliente (Hot-Plug).
/// Si un usuario conecta su lector USB después de que el servicio haya iniciado,
/// el backend se actualiza automáticamente sin reiniciar el servicio ni la PC.
/// </summary>
public class DynamicBackendSelector : IFingerBackend
{
    private IFingerBackend _activeBackend;
    private DateTime _lastCheck = DateTime.MinValue;
    private readonly object _lock = new();

    public DynamicBackendSelector()
    {
        _activeBackend = BackendSelector.DetectBest();
        _lastCheck = DateTime.Now;
    }

    public string Name => GetActive().Name;
    public bool IsAvailable => GetActive().IsAvailable;

    public IFingerBackend GetActive(bool forceRecheck = false)
    {
        lock (_lock)
        {
            // Si el backend actual es PIN o se fuerza el re-chequeo, o pasaron más de 8 segundos sin lector
            bool needsRecheck = forceRecheck 
                             || (_activeBackend is PinOnlyBackend && DateTime.Now - _lastCheck > TimeSpan.FromSeconds(8))
                             || !_activeBackend.IsAvailable;

            if (needsRecheck)
            {
                _lastCheck = DateTime.Now;
                var detected = BackendSelector.DetectBest();
                if (detected.GetType() != _activeBackend.GetType())
                {
                    Console.WriteLine($"[DynamicBackend] Cambio detectado: {_activeBackend.Name} -> {detected.Name}");
                    _activeBackend = detected;
                }
            }

            return _activeBackend;
        }
    }

    public Task<List<DeviceInfo>> GetDevicesAsync()
    {
        return GetActive(forceRecheck: true).GetDevicesAsync();
    }

    public Task<CaptureResult> CaptureAsync(CancellationToken ct = default)
    {
        return GetActive(forceRecheck: true).CaptureAsync(ct);
    }

    public Task<IdentifyResult> IdentifyAsync(byte[] probeTemplate, Dictionary<int, byte[]> storedTemplates)
    {
        return GetActive().IdentifyAsync(probeTemplate, storedTemplates);
    }

    public Task StartListeningAsync(Func<int, Task> onFingerIdentified, CancellationToken ct = default)
    {
        return GetActive().StartListeningAsync(onFingerIdentified, ct);
    }
}
