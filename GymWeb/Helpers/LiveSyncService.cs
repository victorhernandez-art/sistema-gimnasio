using System;

namespace GymWeb.Helpers;

/// <summary>
/// Servicio de sincronización en tiempo real para reflejar cambios automáticos
/// entre la PC principal, celulares y dispositivos en red local (LAN).
/// </summary>
public static class LiveSyncService
{
    private static long _versionSocios = DateTime.UtcNow.Ticks;
    private static long _versionVisitas = DateTime.UtcNow.Ticks;
    private static long _versionVentas = DateTime.UtcNow.Ticks;
    private static long _versionGlobal = DateTime.UtcNow.Ticks;

    public static long VersionSocios => _versionSocios;
    public static long VersionVisitas => _versionVisitas;
    public static long VersionVentas => _versionVentas;
    public static long VersionGlobal => _versionGlobal;

    public static void NotifySociosChanged()
    {
        _versionSocios = DateTime.UtcNow.Ticks;
        _versionGlobal = _versionSocios;
    }

    public static void NotifyVisitasChanged()
    {
        _versionVisitas = DateTime.UtcNow.Ticks;
        _versionGlobal = _versionVisitas;
    }

    public static void NotifyVentasChanged()
    {
        _versionVentas = DateTime.UtcNow.Ticks;
        _versionGlobal = _versionVentas;
    }

    public static void NotifyGlobalChanged()
    {
        _versionGlobal = DateTime.UtcNow.Ticks;
        _versionSocios = _versionGlobal;
        _versionVisitas = _versionGlobal;
        _versionVentas = _versionGlobal;
    }
}
