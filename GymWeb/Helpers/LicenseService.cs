using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using Microsoft.Data.Sqlite;
using Microsoft.Win32;

namespace GymWeb.Helpers;

public class LicenseInfo
{
    public bool Valid { get; set; }
    public string Type { get; set; } = "DEMO"; // VITALICIA | PRUEBA | DEMO
    public bool Expired { get; set; }
    public DateTime? ExpireDate { get; set; }
    public string? ExpireDateFormatted { get; set; }
    public int? DaysLeft { get; set; }
    public string? Reason { get; set; }
    public string? PcId { get; set; }
    public string? ClaveActivacion { get; set; }
    public DateTime? FechaActivacion { get; set; }

    public bool IsFirstInstall => !Valid && !Expired && string.IsNullOrEmpty(ClaveActivacion);
    public bool IsTrialActive => Valid && Type == "PRUEBA" && !Expired;
    public bool IsTrialExpired => Expired || (!Valid && !IsFirstInstall);
    public bool IsLifetime => Valid && Type == "VITALICIA";
}

public static class LicenseService
{
    // Clave secreta oficial exclusiva para el Sistema de Gimnasio
    public const string GymLicenseSecret = "SistemaGymPro2025-OficialSalt!XZ9k";
    // Clave secreta legacy compatible con Sistema Taller
    public const string LegacyLicenseSecret = "SistemaTaller2025-OficialSalt!XZ9k";

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    private static string? _cachedPcId = null;
    private static LicenseInfo? _cachedStatus = null;
    private static DateTime _lastStatusCheck = DateTime.MinValue;

    /// <summary>
    /// Genera el ID único y determinístico basado en el hardware del equipo.
    /// Idéntico al algoritmo de Node.js: SHA-256(hostname | cpu_model | totalmem | platform | arch).
    /// </summary>
    public static string GetPcId()
    {
        if (!string.IsNullOrEmpty(_cachedPcId)) return _cachedPcId;

        string hostname = Environment.MachineName.ToLowerInvariant();
        string cpu = "unknown";
        try
        {
            if (OperatingSystem.IsWindows())
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
                if (key != null)
                {
                    var val = key.GetValue("ProcessorNameString") as string;
                    if (!string.IsNullOrWhiteSpace(val)) cpu = val.Trim();
                }
            }
        }
        catch { }

        if (cpu == "unknown")
        {
            cpu = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "unknown";
        }

        cpu = Regex.Replace(cpu, @"\s+", "-");

        ulong totalMem = 0;
        if (OperatingSystem.IsWindows())
        {
            try
            {
                var mem = new MEMORYSTATUSEX();
                mem.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
                if (GlobalMemoryStatusEx(ref mem))
                {
                    totalMem = mem.ullTotalPhys;
                }
            }
            catch { }
        }

        if (totalMem == 0)
        {
            totalMem = (ulong)GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        }

        string platform = OperatingSystem.IsWindows() ? "win32" : (OperatingSystem.IsLinux() ? "linux" : "darwin");
        string arch = Environment.Is64BitOperatingSystem ? "x64" : "ia32";

        string raw = $"{hostname}|{cpu}|{totalMem}|{platform}|{arch}";

        using var sha = SHA256.Create();
        byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
        string hex = Convert.ToHexString(bytes).Substring(0, 32).ToUpperInvariant();
        _cachedPcId = hex;
        return hex;
    }

    /// <summary>
    /// Formatea el PC ID con guiones (A1B2-C3D4-...)
    /// </summary>
    public static string FormatPcId(string pcId)
    {
        string clean = Regex.Replace(pcId ?? "", @"[^a-zA-Z0-9]", "").ToUpperInvariant();
        var chunks = Enumerable.Range(0, (clean.Length + 3) / 4)
            .Select(i => clean.Substring(i * 4, Math.Min(4, clean.Length - i * 4)));
        return string.Join("-", chunks);
    }

    /// <summary>
    /// Genera la clave de licencia vitalicia para el PC ID especificado.
    /// Formato: GYM-XXXX-XXXX-XXXX-XXXX
    /// </summary>
    public static string GenerateLicenseKey(string pcId, bool legacy = false)
    {
        string cleanId = Regex.Replace(pcId ?? "", @"[^a-zA-Z0-9]", "").ToUpperInvariant();
        string secret = legacy ? LegacyLicenseSecret : GymLicenseSecret;
        string prefix = legacy ? "ST-" : "GYM-";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(cleanId));
        string hmacHex = Convert.ToHexString(hash).Substring(0, 16).ToUpperInvariant();

        var grupos = Enumerable.Range(0, hmacHex.Length / 4)
            .Select(i => hmacHex.Substring(i * 4, 4));
        return prefix + string.Join("-", grupos);
    }

    /// <summary>
    /// Genera una clave de prueba por los días indicados (default 7 días).
    /// Formato: GYM7-YYYYMMDD-XXXX-XXXX
    /// </summary>
    public static string GenerateTrialKey(string pcId, int days = 7, bool legacy = false)
    {
        string cleanId = Regex.Replace(pcId ?? "", @"[^a-zA-Z0-9]", "").ToUpperInvariant();
        var expDate = DateTime.Today.AddDays(days);
        string dateStr = expDate.ToString("yyyyMMdd");
        string secret = legacy ? LegacyLicenseSecret : GymLicenseSecret;
        string prefix = legacy ? "ST7-" : "GYM7-";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes($"{cleanId}:TRIAL:{dateStr}"));
        string hmacHex = Convert.ToHexString(hash).Substring(0, 8).ToUpperInvariant();
        string g1 = hmacHex.Substring(0, 4);
        string g2 = hmacHex.Substring(4, 4);
        return $"{prefix}{dateStr}-{g1}-{g2}";
    }

    /// <summary>
    /// Valida e inspecciona una clave de licencia frente al PC ID del equipo.
    /// Soporta tanto claves oficiales GYM como claves legacy ST.
    /// </summary>
    public static LicenseInfo GetLicenseInfo(string pcId, string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return new LicenseInfo { Valid = false, Reason = "Clave vacía o inválida", Type = "DEMO" };
        }

        string cleanId = Regex.Replace(pcId ?? "", @"[^a-zA-Z0-9]", "").ToUpperInvariant();
        string k = key.Trim().ToUpperInvariant();

        // 1. CLAVE DE PRUEBA DE 7 DÍAS (GYM7-YYYYMMDD-XXXX-XXXX o ST7-YYYYMMDD-XXXX-XXXX)
        if (k.StartsWith("GYM7-") || k.StartsWith("ST7-"))
        {
            bool isLegacy = k.StartsWith("ST7-");
            string secret = isLegacy ? LegacyLicenseSecret : GymLicenseSecret;

            var parts = k.Split('-');
            if (parts.Length == 4)
            {
                string dateStr = parts[1]; // YYYYMMDD
                string signature = parts[2] + parts[3];

                if (Regex.IsMatch(dateStr, @"^\d{8}$"))
                {
                    if (int.TryParse(dateStr.Substring(0, 4), out int yyyy) &&
                        int.TryParse(dateStr.Substring(4, 2), out int mm) &&
                        int.TryParse(dateStr.Substring(6, 2), out int dd))
                    {
                        var expDate = new DateTime(yyyy, mm, dd, 23, 59, 59, DateTimeKind.Local);
                        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
                        byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes($"{cleanId}:TRIAL:{dateStr}"));
                        string expectedHmac = Convert.ToHexString(hash).Substring(0, 8).ToUpperInvariant();

                        if (signature.Equals(expectedHmac, StringComparison.OrdinalIgnoreCase))
                        {
                            var now = DateTime.Now;
                            bool isExpired = now > expDate;
                            int daysLeft = isExpired ? 0 : Math.Max(0, (int)Math.Ceiling((expDate - now).TotalDays));

                            return new LicenseInfo
                            {
                                Valid = !isExpired,
                                Type = "PRUEBA",
                                Expired = isExpired,
                                ExpireDate = expDate,
                                ExpireDateFormatted = $"{dd:D2}/{mm:D2}/{yyyy}",
                                DaysLeft = daysLeft,
                                PcId = cleanId,
                                ClaveActivacion = k
                            };
                        }
                    }
                }
            }
            return new LicenseInfo { Valid = false, Reason = "Clave de prueba inválida o no corresponde a esta PC", Type = "PRUEBA" };
        }

        // 2. CLAVE VITALICIA OFICIAL GYM (GYM-XXXX-XXXX-XXXX-XXXX)
        string expectedGym = GenerateLicenseKey(cleanId, legacy: false);
        if (k.Equals(expectedGym, StringComparison.OrdinalIgnoreCase))
        {
            return new LicenseInfo
            {
                Valid = true,
                Type = "VITALICIA",
                Expired = false,
                ExpireDate = null,
                ExpireDateFormatted = "Vitalicia (De por vida)",
                DaysLeft = null,
                PcId = cleanId,
                ClaveActivacion = k
            };
        }

        // 3. CLAVE VITALICIA LEGACY TALLER (ST-XXXX-XXXX-XXXX-XXXX)
        string expectedLegacy = GenerateLicenseKey(cleanId, legacy: true);
        if (k.Equals(expectedLegacy, StringComparison.OrdinalIgnoreCase))
        {
            return new LicenseInfo
            {
                Valid = true,
                Type = "VITALICIA",
                Expired = false,
                ExpireDate = null,
                ExpireDateFormatted = "Vitalicia (De por vida)",
                DaysLeft = null,
                PcId = cleanId,
                ClaveActivacion = k
            };
        }

        return new LicenseInfo { Valid = false, Reason = "Clave no válida para este equipo", Type = "DEMO" };
    }

    /// <summary>
    /// Obtiene el estado actual de la licencia en el equipo.
    /// </summary>
    public static LicenseInfo GetStatus(string? dbPath = null)
    {
        if (_cachedStatus != null && (DateTime.Now - _lastStatusCheck).TotalSeconds < 10)
        {
            return _cachedStatus;
        }

        string currentPcId = GetPcId();
        string? clave = null;
        DateTime? fechaAct = null;

        // 1. Intentar leer de SQLite
        try
        {
            string actualDbPath = dbPath ?? GetDefaultDbPath();
            if (File.Exists(actualDbPath))
            {
                using var conn = new SqliteConnection($"Data Source={actualDbPath}");
                conn.Open();

                using var cmdCheck = conn.CreateCommand();
                cmdCheck.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='licencia'";
                var tableExists = cmdCheck.ExecuteScalar();

                if (tableExists != null)
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT pc_id, clave_activacion, fecha_activacion FROM licencia WHERE id = 1";
                    using var reader = cmd.ExecuteReader();
                    if (reader.Read())
                    {
                        string dbPcId = reader.GetString(0);
                        if (dbPcId.Equals(currentPcId, StringComparison.OrdinalIgnoreCase))
                        {
                            clave = reader.GetString(1);
                            if (!reader.IsDBNull(2) && DateTime.TryParse(reader.GetString(2), out var dt))
                            {
                                fechaAct = dt;
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("[LicenseService] Error leyendo SQLite: " + ex.Message);
        }

        // 2. Si no está en DB, intentar leer de license.json
        if (string.IsNullOrWhiteSpace(clave))
        {
            try
            {
                string jsonPath = Path.Combine(AppContext.BaseDirectory, "license.json");
                if (!File.Exists(jsonPath))
                {
                    jsonPath = Path.Combine(Directory.GetCurrentDirectory(), "license.json");
                }

                if (File.Exists(jsonPath))
                {
                    string json = File.ReadAllText(jsonPath);
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("pc_id", out var pElem) &&
                        pElem.GetString()?.Equals(currentPcId, StringComparison.OrdinalIgnoreCase) == true &&
                        root.TryGetProperty("clave_activacion", out var kElem))
                    {
                        clave = kElem.GetString();
                        if (root.TryGetProperty("fecha_activacion", out var fElem) &&
                            DateTime.TryParse(fElem.GetString(), out var dt))
                        {
                            fechaAct = dt;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[LicenseService] Error leyendo license.json: " + ex.Message);
            }
        }

        if (string.IsNullOrWhiteSpace(clave))
        {
            _cachedStatus = new LicenseInfo
            {
                Valid = false,
                Type = "DEMO",
                Expired = false,
                DaysLeft = 0,
                PcId = currentPcId,
                Reason = "Sin licencia activada"
            };
            _lastStatusCheck = DateTime.Now;
            return _cachedStatus;
        }

        var status = GetLicenseInfo(currentPcId, clave);
        status.FechaActivacion = fechaAct;
        _cachedStatus = status;
        _lastStatusCheck = DateTime.Now;
        return status;
    }

    /// <summary>
    /// Activa la licencia con la clave proporcionada.
    /// </summary>
    public static (bool Success, string Message, LicenseInfo Info) Activate(string key, string? dbPath = null)
    {
        string currentPcId = GetPcId();
        var info = GetLicenseInfo(currentPcId, key);

        if (!info.Valid)
        {
            if (info.Expired)
            {
                return (false, "Esta clave de prueba ya ha expirado. Por favor ingresa una clave vitalicia oficial.", info);
            }
            return (false, "Clave de activación inválida o no corresponde a este equipo.", info);
        }

        info.FechaActivacion = DateTime.Now;
        string nowIso = DateTime.Now.ToString("o");

        // 1. Guardar en SQLite
        try
        {
            string actualDbPath = dbPath ?? GetDefaultDbPath();
            using var conn = new SqliteConnection($"Data Source={actualDbPath}");
            conn.Open();

            using var cmdCreate = conn.CreateCommand();
            cmdCreate.CommandText = @"
                CREATE TABLE IF NOT EXISTS licencia (
                    id INTEGER PRIMARY KEY,
                    pc_id TEXT NOT NULL,
                    clave_activacion TEXT NOT NULL,
                    fecha_activacion TEXT NOT NULL,
                    modo TEXT NOT NULL,
                    datos_extra TEXT
                );";
            cmdCreate.ExecuteNonQuery();

            string extra = JsonSerializer.Serialize(new
            {
                type = info.Type,
                expireDate = info.ExpireDate,
                expireDateFormatted = info.ExpireDateFormatted,
                daysLeft = info.DaysLeft
            });

            using var cmdInsert = conn.CreateCommand();
            cmdInsert.CommandText = @"
                INSERT OR REPLACE INTO licencia (id, pc_id, clave_activacion, fecha_activacion, modo, datos_extra)
                VALUES (1, @pcId, @clave, @fecha, 'COMPLETO', @extra);";
            cmdInsert.Parameters.AddWithValue("@pcId", currentPcId);
            cmdInsert.Parameters.AddWithValue("@clave", info.ClaveActivacion ?? key.Trim().ToUpperInvariant());
            cmdInsert.Parameters.AddWithValue("@fecha", nowIso);
            cmdInsert.Parameters.AddWithValue("@extra", extra);
            cmdInsert.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            Console.WriteLine("[LicenseService] Error guardando en SQLite: " + ex.Message);
        }

        // 2. Guardar en license.json en directorio base y directorio de trabajo
        try
        {
            var jsonPayload = new
            {
                pc_id = currentPcId,
                clave_activacion = info.ClaveActivacion ?? key.Trim().ToUpperInvariant(),
                fecha_activacion = nowIso,
                modo = "COMPLETO",
                license_type = info.Type,
                license_expire = info.ExpireDate?.ToString("o"),
                license_expire_fmt = info.ExpireDateFormatted
            };
            string jsonString = JsonSerializer.Serialize(jsonPayload, new JsonSerializerOptions { WriteIndented = true });

            string path1 = Path.Combine(AppContext.BaseDirectory, "license.json");
            File.WriteAllText(path1, jsonString);

            string path2 = Path.Combine(Directory.GetCurrentDirectory(), "license.json");
            if (path1 != path2)
            {
                File.WriteAllText(path2, jsonString);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("[LicenseService] Error guardando license.json: " + ex.Message);
        }

        _cachedStatus = info;
        _lastStatusCheck = DateTime.Now;

        string msg = info.Type == "VITALICIA"
            ? "¡Licencia vitalicia oficial activada permanentemente para este equipo!"
            : $"¡Licencia de prueba activada con éxito! Válida hasta el {info.ExpireDateFormatted}.";

        return (true, msg, info);
    }

    private static string GetDefaultDbPath()
    {
        string p1 = Path.Combine(Directory.GetCurrentDirectory(), "gym.db");
        if (File.Exists(p1)) return p1;

        string p2 = Path.Combine(Directory.GetCurrentDirectory(), "GymWeb", "gym.db");
        if (File.Exists(p2)) return p2;

        string p3 = Path.Combine(AppContext.BaseDirectory, "gym.db");
        return p3;
    }
}
