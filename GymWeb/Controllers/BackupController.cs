using GymWeb.Helpers;
using GymWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace GymWeb.Controllers;

public class BackupController : SuperAdminController
{
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;
    private readonly GymContext _db;

    public BackupController(IConfiguration config, IWebHostEnvironment env, GymContext db)
    {
        _config = config;
        _env    = env;
        _db     = db;
    }

    private bool CheckIsMySql()
    {
        var connStr = _config.GetConnectionString("GymDb") ?? "";
        return connStr.Contains("Server=", StringComparison.OrdinalIgnoreCase) || 
               connStr.Contains("Database=", StringComparison.OrdinalIgnoreCase);
    }

    public IActionResult Index()
    {
        ViewData["Title"] = "Respaldo y Base de Datos";
        ViewData["IsMySql"] = CheckIsMySql();

        var backupDir = Path.Combine(_env.WebRootPath, "backups");
        var files = Directory.Exists(backupDir)
            ? new DirectoryInfo(backupDir)
                .GetFiles("*.*")
                .Where(f => f.Extension.Equals(".db", StringComparison.OrdinalIgnoreCase) || 
                            f.Extension.Equals(".sql", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(f => f.CreationTime)
                .Select(f => new BackupFileInfo
                {
                    FileName = f.Name,
                    SizeKb   = (int)(f.Length / 1024),
                    Created  = f.CreationTime
                }).ToList()
            : new List<BackupFileInfo>();

        return View(files);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Generate()
    {
        var isMySql = CheckIsMySql();
        var backupDir = Path.Combine(_env.WebRootPath, "backups");
        Directory.CreateDirectory(backupDir);

        if (!isMySql)
        {
            // MODO SQLITE: Respaldo nativo y atómico
            var fileName = $"gym_backup_{DateTime.Now:yyyyMMdd_HHmmss}.db";
            var filePath = Path.Combine(backupDir, fileName);

            try
            {
                var normalizedPath = filePath.Replace('\\', '/');
                await _db.Database.ExecuteSqlInterpolatedAsync($"VACUUM INTO {normalizedPath};");
            }
            catch
            {
                try
                {
                    var connStr = _config.GetConnectionString("GymDb") ?? "Data Source=gym.db";
                    var dbSource = ParseConnValue(connStr, "Data Source") ?? "gym.db";
                    if (!Path.IsPathRooted(dbSource))
                        dbSource = Path.Combine(_env.ContentRootPath, dbSource);

                    if (System.IO.File.Exists(dbSource))
                    {
                        System.IO.File.Copy(dbSource, filePath, true);
                    }
                    else
                    {
                        TempData["Error"] = "No se encontró el archivo de base de datos origen SQLite.";
                        return RedirectToAction("Index");
                    }
                }
                catch (Exception ex)
                {
                    TempData["Error"] = $"Error al generar el respaldo SQLite: {ex.Message}";
                    return RedirectToAction("Index");
                }
            }

            TempData["Success"] = $"Respaldo SQLite generado exitosamente: {fileName}";
            return RedirectToAction("Index");
        }
        else
        {
            // MODO MYSQL: Respaldo mediante mysqldump con fallback nativo C#
            var connStr = _config.GetConnectionString("GymDb") ?? "";
            var server   = ParseConnValue(connStr, "Server")   ?? "localhost";
            var database = ParseConnValue(connStr, "Database") ?? "gym";
            var user     = ParseConnValue(connStr, "User")     ?? "root";
            var password = ParseConnValue(connStr, "Password") ?? "";

            var fileName  = $"gym_backup_{DateTime.Now:yyyyMMdd_HHmmss}.sql";
            var filePath  = Path.Combine(backupDir, fileName);

            var candidates = new List<string>
            {
                @"C:\xampp\mysql\bin\mysqldump.exe",
                @"D:\xampp\mysql\bin\mysqldump.exe",
                @"E:\xampp\mysql\bin\mysqldump.exe",
                @"C:\xampp8\mysql\bin\mysqldump.exe",
                @"D:\xampp8\mysql\bin\mysqldump.exe",
                @"C:\wamp64\bin\mysql\mysql8.0.31\bin\mysqldump.exe",
                @"C:\Program Files\MySQL\MySQL Server 8.0\bin\mysqldump.exe",
                @"C:\Program Files\MySQL\MySQL Server 5.7\bin\mysqldump.exe",
                @"C:\Program Files (x86)\MySQL\MySQL Server 5.7\bin\mysqldump.exe"
            };

            var mysqldump = candidates.FirstOrDefault(p => System.IO.File.Exists(p));
            bool dumpSuccess = false;

            if (mysqldump != null)
            {
                try
                {
                    var optFile = Path.GetTempFileName();
                    try
                    {
                        await System.IO.File.WriteAllTextAsync(optFile, $"[client]\npassword={password}\n");
                        var psi = new ProcessStartInfo
                        {
                            FileName              = mysqldump,
                            Arguments             = $"--defaults-extra-file=\"{optFile}\" -h {server} -u {user} --routines --triggers {database} -r \"{filePath}\"",
                            RedirectStandardError = true,
                            UseShellExecute       = false,
                            CreateNoWindow        = true
                        };

                        using var process = Process.Start(psi);
                        if (process != null)
                        {
                            var stderr = await process.StandardError.ReadToEndAsync();
                            await process.WaitForExitAsync();
                            if (process.ExitCode == 0)
                            {
                                dumpSuccess = true;
                                TempData["Success"] = $"Respaldo MySQL generado exitosamente: {fileName}";
                            }
                        }
                    }
                    finally
                    {
                        if (System.IO.File.Exists(optFile)) System.IO.File.Delete(optFile);
                    }
                }
                catch (Exception ex)
                {
                    Serilog.Log.Warning(ex, "Fallo al ejecutar mysqldump externo. Se usará el exportador nativo C#.");
                }
            }

            // Fallback nativo: Si mysqldump no existe o falló, exportar directamente con C# y MySqlConnection
            if (!dumpSuccess)
            {
                try
                {
                    var nativeOk = await ExportMySqlNativeAsync(connStr, filePath);
                    if (nativeOk)
                    {
                        TempData["Success"] = $"Respaldo MySQL generado exitosamente (modo nativo): {fileName}";
                    }
                    else
                    {
                        TempData["Error"] = "No se pudo generar el respaldo MySQL. Verifica que el servicio MySQL/XAMPP esté activo.";
                    }
                }
                catch (Exception ex)
                {
                    TempData["Error"] = $"Error al generar el respaldo nativo de MySQL: {ex.Message}";
                }
            }

            return RedirectToAction("Index");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MigrarASqlite()
    {
        var mySqlConnStr = _config.GetConnectionString("GymDb") ?? "Server=localhost;Database=gym;User=root;Password=;";
        var sqliteDbPath = Path.Combine(_env.ContentRootPath, "gym.db");

        // Inicializar estructura previa en SQLite
        try
        {
            var optBuilder = new DbContextOptionsBuilder<GymContext>();
            optBuilder.UseSqlite($"Data Source={sqliteDbPath}");
            using (var initDb = new GymContext(optBuilder.Options))
            {
                DbInitializer.Initialize(initDb);
            }
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"No se pudo preparar la base de datos SQLite: {ex.Message}";
            return RedirectToAction("Index");
        }

        // Ejecutar migración completa de registros
        var report = await MySqlToSqliteMigrator.MigrateAsync(mySqlConnStr, sqliteDbPath);
        if (report.Success)
        {
            // Actualizar appsettings.json y appsettings.Production.json para apuntar a SQLite
            var appSettingsPath = Path.Combine(_env.ContentRootPath, "appsettings.json");
            MySqlToSqliteMigrator.SwitchToSqliteConfig(appSettingsPath, "gym.db");
            var prodConfigPath = Path.Combine(_env.ContentRootPath, "appsettings.Production.json");
            if (System.IO.File.Exists(prodConfigPath))
            {
                MySqlToSqliteMigrator.SwitchToSqliteConfig(prodConfigPath, "gym.db");
            }

            var summary = string.Join(", ", report.TableResults.Select(r => $"{r.Key}: {r.Value.SqliteCount}"));
            TempData["Success"] = $"🎉 ¡Migración completada exitosamente en {report.Duration.TotalSeconds:F1}s! Datos transferidos: {summary}. Cierra y vuelve a abrir GymWeb para operar en Modo Autónomo sin XAMPP.";
        }
        else
        {
            TempData["Error"] = $"Fallo al migrar datos: {report.Message}";
        }

        return RedirectToAction("Index");
    }

    public IActionResult Download(string fileName)
    {
        if (string.IsNullOrEmpty(fileName) || !Regex.IsMatch(fileName, @"^[\w\-\.]+$"))
            return BadRequest();

        var filePath = Path.Combine(_env.WebRootPath, "backups", fileName);
        if (!System.IO.File.Exists(filePath))
            return NotFound();

        var bytes = System.IO.File.ReadAllBytes(filePath);
        return File(bytes, "application/octet-stream", fileName);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(string fileName)
    {
        if (string.IsNullOrEmpty(fileName) || !Regex.IsMatch(fileName, @"^[\w\-\.]+$"))
            return BadRequest();

        var filePath = Path.Combine(_env.WebRootPath, "backups", fileName);
        if (System.IO.File.Exists(filePath))
            System.IO.File.Delete(filePath);

        TempData["Success"] = "Respaldo eliminado.";
        return RedirectToAction("Index");
    }

    // ── RESTAURAR DESDE ARCHIVO EXISTENTE EN EL HISTORIAL ─────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RestoreFile(string fileName)
    {
        if (string.IsNullOrEmpty(fileName) || !Regex.IsMatch(fileName, @"^[\w\-\.]+$"))
        {
            TempData["Error"] = "Nombre de archivo no válido.";
            return RedirectToAction("Index");
        }

        var filePath = Path.Combine(_env.WebRootPath, "backups", fileName);
        if (!System.IO.File.Exists(filePath))
        {
            TempData["Error"] = "El archivo de respaldo seleccionado ya no existe en el disco.";
            return RedirectToAction("Index");
        }

        var isMySql = CheckIsMySql();
        var (ok, msg) = isMySql
            ? await EjecutarRestauracionMySqlAsync(filePath, fileName)
            : await EjecutarRestauracionSqliteAsync(filePath, fileName);

        if (ok)
            TempData["Success"] = msg;
        else
            TempData["Error"] = msg;

        return RedirectToAction("Index");
    }

    // ── IMPORTAR Y RESTAURAR ARCHIVO SUBIDO DESDE LA PC ───────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadAndRestore(IFormFile archivoRespaldo)
    {
        if (archivoRespaldo == null || archivoRespaldo.Length == 0)
        {
            TempData["Error"] = "Debes seleccionar un archivo de respaldo válido desde tu computadora.";
            return RedirectToAction("Index");
        }

        var ext = Path.GetExtension(archivoRespaldo.FileName).ToLowerInvariant();
        var isMySql = CheckIsMySql();

        if (!isMySql && ext != ".db" && ext != ".sqlite" && ext != ".sqlite3")
        {
            TempData["Error"] = "Formato no compatible. Para el modo autónomo SQLite debes seleccionar un archivo .db.";
            return RedirectToAction("Index");
        }
        else if (isMySql && ext != ".sql")
        {
            TempData["Error"] = "Formato no compatible. Para el modo MySQL debes seleccionar un archivo de texto .sql.";
            return RedirectToAction("Index");
        }

        // Guardar temporalmente para validar
        var backupDir = Path.Combine(_env.WebRootPath, "backups");
        Directory.CreateDirectory(backupDir);

        var safeFileName = $"gym_imported_{DateTime.Now:yyyyMMdd_HHmmss}{ext}";
        var tempUploadedPath = Path.Combine(backupDir, safeFileName);

        try
        {
            using (var stream = new FileStream(tempUploadedPath, FileMode.Create))
            {
                await archivoRespaldo.CopyToAsync(stream);
            }

            var (ok, msg) = isMySql
                ? await EjecutarRestauracionMySqlAsync(tempUploadedPath, archivoRespaldo.FileName)
                : await EjecutarRestauracionSqliteAsync(tempUploadedPath, archivoRespaldo.FileName);

            if (ok)
            {
                TempData["Success"] = $"¡Restauración exitosa! {msg}";
            }
            else
            {
                // Si la validación falló, eliminamos el archivo subido corrupto
                if (System.IO.File.Exists(tempUploadedPath))
                    System.IO.File.Delete(tempUploadedPath);

                TempData["Error"] = msg;
            }
        }
        catch (Exception ex)
        {
            if (System.IO.File.Exists(tempUploadedPath))
                System.IO.File.Delete(tempUploadedPath);

            TempData["Error"] = $"Error al procesar el archivo subido: {ex.Message}";
        }

        return RedirectToAction("Index");
    }

    // ── LÓGICA DE RESTAURACIÓN ROBUSTA SQLITE (MÁXIMA SEGURIDAD) ─────────
    private async Task<(bool ok, string msg)> EjecutarRestauracionSqliteAsync(string backupFilePath, string nombreParaHistorial)
    {
        // 1. Validar existencia y tamaño
        if (!System.IO.File.Exists(backupFilePath) || new FileInfo(backupFilePath).Length < 100)
            return (false, "El archivo de respaldo está vacío o incompleto.");

        // 2. Validar firma oficial SQLite (primeros 16 bytes: 'SQLite format 3\0')
        byte[] header = new byte[16];
        using (var fs = System.IO.File.OpenRead(backupFilePath))
        {
            int bytesRead = await fs.ReadAsync(header, 0, 16);
            if (bytesRead < 16)
                return (false, "El archivo no es una base de datos SQLite válida.");
        }
        string headerStr = System.Text.Encoding.ASCII.GetString(header);
        if (!headerStr.StartsWith("SQLite format 3\0"))
            return (false, "El archivo seleccionado no tiene la firma oficial de SQLite. Asegúrate de subir un archivo .db válido.");

        // 3. Probar apertura y verificar integridad y estructura esencial
        var testConnStr = $"Data Source={backupFilePath};Mode=ReadOnly;";
        try
        {
            using var testConn = new SqliteConnection(testConnStr);
            await testConn.OpenAsync();

            using (var cmd = testConn.CreateCommand())
            {
                cmd.CommandText = "PRAGMA quick_check;";
                var checkResult = (await cmd.ExecuteScalarAsync())?.ToString();
                if (checkResult != "ok")
                    return (false, $"El archivo de respaldo no pasó la prueba de integridad: {checkResult}");
            }

            using (var cmd = testConn.CreateCommand())
            {
                cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('usuario', 'configuracion', 'socio');";
                long tableCount = Convert.ToInt64(await cmd.ExecuteScalarAsync());
                if (tableCount < 2)
                    return (false, "El archivo no pertenece al Sistema de Gimnasio o carece de las tablas principales (usuarios, socios, configuración).");
            }
        }
        catch (Exception ex)
        {
            return (false, $"Error al verificar el archivo de respaldo: {ex.Message}");
        }

        // 4. GENERAR RESPALDO PREVENTIVO AUTOMÁTICO del estado actual (Pre-restore snapshot)
        var backupDir = Path.Combine(_env.WebRootPath, "backups");
        Directory.CreateDirectory(backupDir);
        var preRestoreFile = Path.Combine(backupDir, $"gym_auto_prerestore_{DateTime.Now:yyyyMMdd_HHmmss}.db");

        var connStr = _config.GetConnectionString("GymDb") ?? "Data Source=gym.db";
        var dbSource = ParseConnValue(connStr, "Data Source") ?? "gym.db";
        if (!Path.IsPathRooted(dbSource))
            dbSource = Path.Combine(_env.ContentRootPath, dbSource);

        try
        {
            if (System.IO.File.Exists(dbSource))
            {
                var normPre = preRestoreFile.Replace('\\', '/');
                try
                {
                    await _db.Database.ExecuteSqlInterpolatedAsync($"VACUUM INTO {normPre};");
                }
                catch
                {
                    System.IO.File.Copy(dbSource, preRestoreFile, true);
                }
            }
        }
        catch { }

        // 5. RESTAURAR ATÓMICAMENTE CON SQLite Backup API (Cero problemas de archivo en uso)
        try
        {
            SqliteConnection.ClearAllPools();

            var destConnStr = $"Data Source={dbSource};";
            using (var sourceConn = new SqliteConnection(testConnStr))
            using (var destConn = new SqliteConnection(destConnStr))
            {
                await sourceConn.OpenAsync();
                await destConn.OpenAsync();
                sourceConn.BackupDatabase(destConn);
            }

            SqliteConnection.ClearAllPools();

            // 6. Recargar en memoria la configuración del gimnasio desde la base restaurada
            try
            {
                using var scopeDb = new SqliteConnection(destConnStr);
                await scopeDb.OpenAsync();
                using var cmd = scopeDb.CreateCommand();
                cmd.CommandText = "SELECT NombreGimnacio, Domicilio, Telefono, Mensaje FROM configuracion LIMIT 1;";
                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    AppSettings.GymNombre    = reader.IsDBNull(0) ? AppSettings.GymNombre : reader.GetString(0);
                    AppSettings.GymDomicilio = reader.IsDBNull(1) ? "" : reader.GetString(1);
                    AppSettings.GymTelefono  = reader.IsDBNull(2) ? "" : reader.GetString(2);
                    AppSettings.GymPieTicket = reader.IsDBNull(3) ? "" : reader.GetString(3);
                }
            }
            catch { }

            return (true, $"Base de datos restaurada correctamente desde '{nombreParaHistorial}'. Se creó un punto de seguridad preventivo automático por protección.");
        }
        catch (Exception ex)
        {
            return (false, $"Fallo crítico durante la restauración de SQLite: {ex.Message}");
        }
    }

    // ── LÓGICA DE RESTAURACIÓN MYSQL ──────────────────────────────────────
    private async Task<(bool ok, string msg)> EjecutarRestauracionMySqlAsync(string backupFilePath, string nombreParaHistorial)
    {
        if (!System.IO.File.Exists(backupFilePath) || new FileInfo(backupFilePath).Length == 0)
            return (false, "El archivo SQL no existe o está vacío.");

        var connStr = _config.GetConnectionString("GymDb") ?? "";
        var server   = ParseConnValue(connStr, "Server")   ?? "localhost";
        var database = ParseConnValue(connStr, "Database") ?? "gym";
        var user     = ParseConnValue(connStr, "User")     ?? "root";
        var password = ParseConnValue(connStr, "Password") ?? "";

        var candidates = new[]
        {
            @"C:\xampp\mysql\bin\mysql.exe",
            @"C:\xampp8\mysql\bin\mysql.exe",
            @"C:\wamp64\bin\mysql\mysql8.0.31\bin\mysql.exe",
            "mysql"
        };

        var mysqlExe = candidates.FirstOrDefault(p => p == "mysql" || System.IO.File.Exists(p));
        if (mysqlExe == null)
            return (false, "No se encontró el ejecutable mysql.exe en el servidor.");

        var optFile = Path.GetTempFileName();
        try
        {
            await System.IO.File.WriteAllTextAsync(optFile, $"[client]\npassword={password}\n");
            var psi = new ProcessStartInfo
            {
                FileName              = mysqlExe,
                Arguments             = $"--defaults-extra-file=\"{optFile}\" -h {server} -u {user} {database}",
                RedirectStandardInput = true,
                RedirectStandardError = true,
                UseShellExecute       = false,
                CreateNoWindow        = true
            };

            using var process = Process.Start(psi);
            if (process == null) return (false, "No se pudo iniciar el proceso de restauración MySQL.");

            using (var fileStream = System.IO.File.OpenRead(backupFilePath))
            {
                await fileStream.CopyToAsync(process.StandardInput.BaseStream);
                await process.StandardInput.BaseStream.FlushAsync();
                process.StandardInput.Close();
            }

            var stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
                return (false, $"Error al restaurar MySQL: {stderr}");

            return (true, $"Base de datos MySQL restaurada correctamente desde '{nombreParaHistorial}'.");
        }
        finally
        {
            if (System.IO.File.Exists(optFile)) System.IO.File.Delete(optFile);
        }
    }

    private static string? ParseConnValue(string connStr, string key)
    {
        var match = Regex.Match(connStr, $@"(?i){Regex.Escape(key)}\s*=\s*([^;]*)");
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Limpiar(string confirmacion)
    {
        if (confirmacion != "LIMPIAR")
        {
            TempData["Error"] = "Confirmación incorrecta. No se realizó ninguna acción.";
            return RedirectToAction("Index");
        }

        var isMySql = CheckIsMySql();
        if (!isMySql)
        {
            // SQLite — borrado atómico en transacción
            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                await _db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF;");
                await _db.Database.ExecuteSqlRawAsync("DELETE FROM sociomembresia;");
                await _db.Database.ExecuteSqlRawAsync("DELETE FROM socio_huella;");
                await _db.Database.ExecuteSqlRawAsync("DELETE FROM pago;");
                await _db.Database.ExecuteSqlRawAsync("DELETE FROM registro;");
                await _db.Database.ExecuteSqlRawAsync("DELETE FROM detallesalida;");
                await _db.Database.ExecuteSqlRawAsync("DELETE FROM salida;");
                await _db.Database.ExecuteSqlRawAsync("DELETE FROM socio;");
                await _db.Database.ExecuteSqlRawAsync("DELETE FROM producto;");
                await _db.Database.ExecuteSqlRawAsync("DELETE FROM membresia;");
                await _db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = ON;");
                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                TempData["Error"] = "Error al limpiar la base de datos. No se realizó ningún cambio.";
                return RedirectToAction("Index");
            }
        }
        else
        {
            // MySQL — borrado atómico en transacción
            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                await _db.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS = 0;");
                await _db.Database.ExecuteSqlRawAsync("DELETE FROM sociomembresia;");
                await _db.Database.ExecuteSqlRawAsync("DELETE FROM socio_huella;");
                await _db.Database.ExecuteSqlRawAsync("DELETE FROM pago;");
                await _db.Database.ExecuteSqlRawAsync("DELETE FROM registro;");
                await _db.Database.ExecuteSqlRawAsync("DELETE FROM detallesalida;");
                await _db.Database.ExecuteSqlRawAsync("DELETE FROM salida;");
                await _db.Database.ExecuteSqlRawAsync("DELETE FROM socio;");
                await _db.Database.ExecuteSqlRawAsync("DELETE FROM producto;");
                await _db.Database.ExecuteSqlRawAsync("DELETE FROM membresia;");
                await _db.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS = 1;");
                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                TempData["Error"] = "Error al limpiar la base de datos. No se realizó ningún cambio.";
                return RedirectToAction("Index");
            }
        }

        TempData["Success"] = "Base de datos limpiada correctamente. Todos los registros han sido eliminados.";
        return RedirectToAction("Index");
    }

    private static async Task<bool> ExportMySqlNativeAsync(string connStr, string filePath)
    {
        try
        {
            await using var conn = new MySqlConnection(connStr);
            await conn.OpenAsync();

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("-- GymWeb Respaldo Nativo de MySQL");
            sb.AppendLine($"-- Fecha: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine("SET FOREIGN_KEY_CHECKS=0;");
            sb.AppendLine();

            var tables = new List<string>();
            await using (var cmd = new MySqlCommand("SHOW FULL TABLES WHERE Table_type = 'BASE TABLE';", conn))
            await using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    tables.Add(reader.GetString(0));
                }
            }

            foreach (var table in tables)
            {
                sb.AppendLine($"-- Estructura para la tabla `{table}`");
                sb.AppendLine($"DROP TABLE IF EXISTS `{table}`;");

                await using (var createCmd = new MySqlCommand($"SHOW CREATE TABLE `{table}`;", conn))
                await using (var createReader = await createCmd.ExecuteReaderAsync())
                {
                    if (await createReader.ReadAsync())
                    {
                        sb.AppendLine(createReader.GetString(1) + ";");
                        sb.AppendLine();
                    }
                }

                await using (var selectCmd = new MySqlCommand($"SELECT * FROM `{table}`;", conn))
                await using (var dataReader = await selectCmd.ExecuteReaderAsync())
                {
                    var colCount = dataReader.FieldCount;
                    var colNames = new List<string>();
                    for (int i = 0; i < colCount; i++) colNames.Add($"`{dataReader.GetName(i)}`");
                    var colListStr = string.Join(", ", colNames);

                    while (await dataReader.ReadAsync())
                    {
                        var values = new List<string>();
                        for (int i = 0; i < colCount; i++)
                        {
                            if (dataReader.IsDBNull(i))
                            {
                                values.Add("NULL");
                            }
                            else
                            {
                                var val = dataReader.GetValue(i);
                                if (val is byte[] bytes)
                                {
                                    values.Add("0x" + Convert.ToHexString(bytes));
                                }
                                else if (val is DateTime dt)
                                {
                                    values.Add($"'{dt:yyyy-MM-dd HH:mm:ss}'");
                                }
                                else if (val is bool b)
                                {
                                    values.Add(b ? "1" : "0");
                                }
                                else if (val is int || val is long || val is short || val is decimal || val is double || val is float)
                                {
                                    values.Add(Convert.ToString(val, System.Globalization.CultureInfo.InvariantCulture)!);
                                }
                                else
                                {
                                    var strVal = val.ToString()?.Replace("\\", "\\\\").Replace("'", "\\'") ?? "";
                                    values.Add($"'{strVal}'");
                                }
                            }
                        }
                        sb.AppendLine($"INSERT INTO `{table}` ({colListStr}) VALUES ({string.Join(", ", values)});");
                    }
                    sb.AppendLine();
                }
            }

            sb.AppendLine("SET FOREIGN_KEY_CHECKS=1;");
            await System.IO.File.WriteAllTextAsync(filePath, sb.ToString(), System.Text.Encoding.UTF8);
            return true;
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Error en ExportMySqlNativeAsync");
            return false;
        }
    }
}

public class BackupFileInfo
{
    public string   FileName { get; set; } = "";
    public int      SizeKb   { get; set; }
    public DateTime Created  { get; set; }
}
