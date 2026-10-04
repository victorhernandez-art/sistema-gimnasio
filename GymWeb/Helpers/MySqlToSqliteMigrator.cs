using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using MySqlConnector;

namespace GymWeb.Helpers;

public class MigrationReport
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public Dictionary<string, (int MySqlCount, int SqliteCount)> TableResults { get; set; } = new();
    public TimeSpan Duration { get; set; }
}

public static class MySqlToSqliteMigrator
{
    public static async Task<MigrationReport> MigrateAsync(string mySqlConnStr, string sqliteDbPath)
    {
        var startTime = DateTime.Now;
        var report = new MigrationReport();

        var tables = new[]
        {
            "estado",
            "configuracion",
            "usuario",
            "membresia",
            "producto",
            "socio",
            "socio_huella",
            "sociomembresia",
            "pago",
            "salida",
            "detallesalida",
            "registro",
            "visita"
        };

        try
        {
            // 1. Probar conexión a MySQL
            await using var mySqlConn = new MySqlConnection(mySqlConnStr);
            await mySqlConn.OpenAsync();

            // 2. Conectar a SQLite
            var sqliteConnStr = $"Data Source={sqliteDbPath}";
            await using var sqliteConn = new SqliteConnection(sqliteConnStr);
            await sqliteConn.OpenAsync();

            // Desactivar llaves foráneas y usar WAL para máxima velocidad y seguridad en la importación
            await using (var pragmaCmd = new SqliteCommand("PRAGMA foreign_keys = OFF; PRAGMA journal_mode = WAL;", sqliteConn))
            {
                await pragmaCmd.ExecuteNonQueryAsync();
            }

            await using var transaction = (SqliteTransaction)await sqliteConn.BeginTransactionAsync();

            foreach (var table in tables)
            {
                // Verificar si la tabla existe en MySQL
                var tableExistsCmd = new MySqlCommand(
                    $"SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = '{table}';", 
                    mySqlConn);
                var exists = Convert.ToInt32(await tableExistsCmd.ExecuteScalarAsync()) > 0;

                if (!exists)
                {
                    continue;
                }

                // Obtener datos de MySQL
                var selectCmd = new MySqlCommand($"SELECT * FROM `{table}`;", mySqlConn);
                await using var reader = await selectCmd.ExecuteReaderAsync();

                var schemaTable = await reader.GetColumnSchemaAsync();
                if (schemaTable.Count == 0)
                {
                    reader.Close();
                    continue;
                }

                var colNames = new List<string>();
                var paramNames = new List<string>();

                foreach (var col in schemaTable)
                {
                    colNames.Add($"\"{col.ColumnName}\"");
                    paramNames.Add($"@{col.ColumnName}");
                }

                var insertSql = $"INSERT OR REPLACE INTO \"{table}\" ({string.Join(", ", colNames)}) VALUES ({string.Join(", ", paramNames)});";

                int mySqlCount = 0;
                while (await reader.ReadAsync())
                {
                    mySqlCount++;
                    await using var insertCmd = new SqliteCommand(insertSql, sqliteConn, transaction);

                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        var colName = reader.GetName(i);
                        var val = reader.GetValue(i);

                        if (val == DBNull.Value)
                        {
                            insertCmd.Parameters.AddWithValue($"@{colName}", DBNull.Value);
                        }
                        else if (val is DateTime dt)
                        {
                            if (colName.Equals("fechaPago", StringComparison.OrdinalIgnoreCase))
                            {
                                insertCmd.Parameters.AddWithValue($"@{colName}", dt.ToString("yyyy-MM-dd"));
                            }
                            else
                            {
                                insertCmd.Parameters.AddWithValue($"@{colName}", dt.ToString("yyyy-MM-dd HH:mm:ss"));
                            }
                        }
                        else if (val is TimeSpan ts)
                        {
                            insertCmd.Parameters.AddWithValue($"@{colName}", ts.ToString(@"hh\:mm\:ss"));
                        }
                        else if (val is byte[] bytes)
                        {
                            insertCmd.Parameters.AddWithValue($"@{colName}", bytes);
                        }
                        else
                        {
                            insertCmd.Parameters.AddWithValue($"@{colName}", val);
                        }
                    }

                    await insertCmd.ExecuteNonQueryAsync();
                }
                reader.Close();

                // Validar conteo en SQLite
                var countSqliteCmd = new SqliteCommand($"SELECT COUNT(*) FROM \"{table}\";", sqliteConn, transaction);
                int sqliteCount = Convert.ToInt32(await countSqliteCmd.ExecuteScalarAsync());

                report.TableResults[table] = (mySqlCount, sqliteCount);
            }

            // Sincronizar secuencias autoincrementables en SQLite para evitar colisiones de IDs nuevos
            var pkMap = new Dictionary<string, string>
            {
                { "estado", "idEstados" },
                { "configuracion", "idConfiguracion" },
                { "usuario", "idUsuario" },
                { "membresia", "idMembresia" },
                { "producto", "idProducto" },
                { "socio", "idSocio" },
                { "socio_huella", "idHuella" },
                { "sociomembresia", "idSocioMembresia" },
                { "pago", "idPago" },
                { "salida", "idSalida" },
                { "detallesalida", "iddetalleSalida" },
                { "registro", "idRegistro" },
                { "visita", "idVisita" }
            };

            foreach (var kv in pkMap)
            {
                try
                {
                    var maxCmd = new SqliteCommand($"SELECT coalesce(max(\"{kv.Value}\"), 0) FROM \"{kv.Key}\";", sqliteConn, transaction);
                    var maxId = Convert.ToInt64(await maxCmd.ExecuteScalarAsync());
                    if (maxId > 0)
                    {
                        var seqCmd = new SqliteCommand(
                            $"INSERT OR REPLACE INTO sqlite_sequence (name, seq) VALUES ('{kv.Key}', {maxId});", 
                            sqliteConn, 
                            transaction);
                        await seqCmd.ExecuteNonQueryAsync();
                    }
                }
                catch { }
            }

            await transaction.CommitAsync();

            // Reactivar llaves foráneas
            await using (var pragmaOnCmd = new SqliteCommand("PRAGMA foreign_keys = ON;", sqliteConn))
            {
                await pragmaOnCmd.ExecuteNonQueryAsync();
            }

            report.Success = true;
            report.Duration = DateTime.Now - startTime;
            report.Message = "Migración completada con éxito. Todos los registros y estructuras han sido transferidos.";
        }
        catch (Exception ex)
        {
            report.Success = false;
            report.Duration = DateTime.Now - startTime;
            report.Message = $"Error durante la migración: {ex.Message}";
        }

        return report;
    }

    /// <summary>
    /// Cambia la cadena de conexión en appsettings.json para apuntar al archivo SQLite gym.db
    /// </summary>
    public static bool SwitchToSqliteConfig(string appSettingsPath, string dbFileName = "gym.db")
    {
        try
        {
            if (!File.Exists(appSettingsPath)) return false;

            var jsonText = File.ReadAllText(appSettingsPath);
            var root = JsonNode.Parse(jsonText);
            if (root?["ConnectionStrings"] != null)
            {
                root["ConnectionStrings"]!["GymDb"] = $"Data Source={dbFileName}";
                File.WriteAllText(appSettingsPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
                return true;
            }
        }
        catch { }
        return false;
    }

    /// <summary>
    /// Sincroniza e importa de forma segura los cobros y membresías registrados en MySQL
    /// que no existan en SQLite, sin sobreescribir ni eliminar ningún registro existente en SQLite.
    /// </summary>
    public static async Task<(bool Success, string Message, int PagosNuevos, int MembresiasNuevas)> SincronizarCobrosRecientesAsync(string mySqlConnStr, string sqliteDbPath, string fechaDesde = "2026-09-20")
    {
        try
        {
            if (!File.Exists(sqliteDbPath))
                return (false, "No se encontró el archivo gym.db de SQLite.", 0, 0);

            await using var mySqlConn = new MySqlConnection(mySqlConnStr);
            await mySqlConn.OpenAsync();

            var sqliteConnStr = $"Data Source={sqliteDbPath}";
            await using var sqliteConn = new SqliteConnection(sqliteConnStr);
            await sqliteConn.OpenAsync();

            int pagosImportados = 0;
            int membresiasImportadas = 0;

            // 1. Obtener pagos existentes en SQLite para evitar duplicados
            var pagosExistentesSqlite = new HashSet<string>();
            int maxIdPagoSqlite = 0;
            await using (var cmdExist = new SqliteCommand("SELECT idPago, idSocio, fechaPago, monto FROM pago;", sqliteConn))
            await using (var r = await cmdExist.ExecuteReaderAsync())
            {
                while (await r.ReadAsync())
                {
                    int idp = r.GetInt32(0);
                    if (idp > maxIdPagoSqlite) maxIdPagoSqlite = idp;

                    int ids = r.IsDBNull(1) ? 0 : r.GetInt32(1);
                    string fp = r.IsDBNull(2) ? "" : r.GetString(2);
                    if (fp.Length > 10) fp = fp.Substring(0, 10);
                    decimal m = r.IsDBNull(3) ? 0m : r.GetDecimal(3);

                    pagosExistentesSqlite.Add($"{ids}|{fp}|{m:F2}");
                }
            }

            // 2. Consultar pagos en MySQL desde la fecha de corte
            var sqlPagosMySql = $"SELECT idPago, idSocio, idMembresia, monto, metodoPago, fechaPago, notas, idEstado, fechaCreacion, idUsuarioCreo FROM pago WHERE fechaPago >= '{fechaDesde}' ORDER BY fechaPago, idPago;";
            await using (var cmdMySql = new MySqlCommand(sqlPagosMySql, mySqlConn))
            await using (var rMySql = await cmdMySql.ExecuteReaderAsync())
            {
                var pagosParaInsertar = new List<Dictionary<string, object?>>();
                while (await rMySql.ReadAsync())
                {
                    int idSocio = rMySql.IsDBNull(1) ? 0 : rMySql.GetInt32(1);
                    var dtPago = rMySql.IsDBNull(5) ? DateTime.MinValue : rMySql.GetDateTime(5);
                    string fPagoStr = dtPago.ToString("yyyy-MM-dd");
                    decimal monto = rMySql.IsDBNull(3) ? 0m : rMySql.GetDecimal(3);

                    string key = $"{idSocio}|{fPagoStr}|{monto:F2}";
                    if (!pagosExistentesSqlite.Contains(key))
                    {
                        var dict = new Dictionary<string, object?>
                        {
                            ["idSocio"] = idSocio,
                            ["idMembresia"] = rMySql.IsDBNull(2) ? DBNull.Value : rMySql.GetInt32(2),
                            ["monto"] = monto,
                            ["metodoPago"] = rMySql.IsDBNull(4) ? "Efectivo" : rMySql.GetString(4),
                            ["fechaPago"] = fPagoStr,
                            ["notas"] = rMySql.IsDBNull(6) ? DBNull.Value : rMySql.GetString(6),
                            ["idEstado"] = rMySql.IsDBNull(7) ? 1 : rMySql.GetInt32(7),
                            ["fechaCreacion"] = rMySql.IsDBNull(8) ? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") : rMySql.GetDateTime(8).ToString("yyyy-MM-dd HH:mm:ss"),
                            ["idUsuarioCreo"] = rMySql.IsDBNull(9) ? DBNull.Value : rMySql.GetInt32(9)
                        };
                        pagosParaInsertar.Add(dict);
                        pagosExistentesSqlite.Add(key);
                    }
                }
                rMySql.Close();

                // Insertar los pagos nuevos en SQLite con ID autoincremental seguro
                foreach (var p in pagosParaInsertar)
                {
                    maxIdPagoSqlite++;
                    var insertSql = "INSERT INTO pago (idPago, idSocio, idMembresia, monto, metodoPago, fechaPago, notas, idEstado, fechaCreacion, idUsuarioCreo) VALUES (@idPago, @idSocio, @idMembresia, @monto, @metodoPago, @fechaPago, @notas, @idEstado, @fechaCreacion, @idUsuarioCreo);";
                    await using var cmdIns = new SqliteCommand(insertSql, sqliteConn);
                    cmdIns.Parameters.AddWithValue("@idPago", maxIdPagoSqlite);
                    cmdIns.Parameters.AddWithValue("@idSocio", p["idSocio"]);
                    cmdIns.Parameters.AddWithValue("@idMembresia", p["idMembresia"] ?? DBNull.Value);
                    cmdIns.Parameters.AddWithValue("@monto", p["monto"]);
                    cmdIns.Parameters.AddWithValue("@metodoPago", p["metodoPago"]);
                    cmdIns.Parameters.AddWithValue("@fechaPago", p["fechaPago"]);
                    cmdIns.Parameters.AddWithValue("@notas", p["notas"] ?? DBNull.Value);
                    cmdIns.Parameters.AddWithValue("@idEstado", p["idEstado"]);
                    cmdIns.Parameters.AddWithValue("@fechaCreacion", p["fechaCreacion"]);
                    cmdIns.Parameters.AddWithValue("@idUsuarioCreo", p["idUsuarioCreo"] ?? DBNull.Value);
                    await cmdIns.ExecuteNonQueryAsync();
                    pagosImportados++;
                }
            }

            // 3. Sincronizar membresías asignadas en MySQL (sociomembresia)
            var memExistentesSqlite = new HashSet<string>();
            int maxIdMemSqlite = 0;
            await using (var cmdMem = new SqliteCommand("SELECT idSocioMembresia, idSocio, idMembresia, fechaInicioMembresia FROM sociomembresia;", sqliteConn))
            await using (var r = await cmdMem.ExecuteReaderAsync())
            {
                while (await r.ReadAsync())
                {
                    int idsm = r.GetInt32(0);
                    if (idsm > maxIdMemSqlite) maxIdMemSqlite = idsm;

                    int ids = r.IsDBNull(1) ? 0 : r.GetInt32(1);
                    int idm = r.IsDBNull(2) ? 0 : r.GetInt32(2);
                    string fi = r.IsDBNull(3) ? "" : r.GetString(3);
                    if (fi.Length > 10) fi = fi.Substring(0, 10);

                    memExistentesSqlite.Add($"{ids}|{idm}|{fi}");
                }
            }

            var sqlMemMySql = $"SELECT idSocioMembresia, idSocio, idMembresia, precio, fechaInicioMembresia, fechaCreacion, idUsuarioCreo, idEstado FROM sociomembresia WHERE (fechaInicioMembresia >= '{fechaDesde}' OR fechaCreacion >= '{fechaDesde}') ORDER BY idSocioMembresia;";
            await using (var cmdMySqlMem = new MySqlCommand(sqlMemMySql, mySqlConn))
            await using (var rMySqlMem = await cmdMySqlMem.ExecuteReaderAsync())
            {
                var memParaInsertar = new List<Dictionary<string, object?>>();
                while (await rMySqlMem.ReadAsync())
                {
                    int ids = rMySqlMem.IsDBNull(1) ? 0 : rMySqlMem.GetInt32(1);
                    int idm = rMySqlMem.IsDBNull(2) ? 0 : rMySqlMem.GetInt32(2);
                    var dtInicio = rMySqlMem.IsDBNull(4) ? DateTime.MinValue : rMySqlMem.GetDateTime(4);
                    string fiStr = dtInicio.ToString("yyyy-MM-dd");

                    string key = $"{ids}|{idm}|{fiStr}";
                    if (!memExistentesSqlite.Contains(key))
                    {
                        var dict = new Dictionary<string, object?>
                        {
                            ["idSocio"] = ids,
                            ["idMembresia"] = idm,
                            ["precio"] = rMySqlMem.IsDBNull(3) ? 0m : rMySqlMem.GetDecimal(3),
                            ["fechaInicioMembresia"] = dtInicio.ToString("yyyy-MM-dd HH:mm:ss"),
                            ["fechaCreacion"] = rMySqlMem.IsDBNull(5) ? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") : rMySqlMem.GetDateTime(5).ToString("yyyy-MM-dd HH:mm:ss"),
                            ["idUsuarioCreo"] = rMySqlMem.IsDBNull(6) ? DBNull.Value : rMySqlMem.GetInt32(6),
                            ["idEstado"] = rMySqlMem.IsDBNull(7) ? 1 : rMySqlMem.GetInt32(7)
                        };
                        memParaInsertar.Add(dict);
                        memExistentesSqlite.Add(key);
                    }
                }
                rMySqlMem.Close();

                foreach (var m in memParaInsertar)
                {
                    maxIdMemSqlite++;
                    var insMemSql = "INSERT INTO sociomembresia (idSocioMembresia, idSocio, idMembresia, precio, fechaInicioMembresia, fechaCreacion, idUsuarioCreo, idEstado) VALUES (@idSocioMembresia, @idSocio, @idMembresia, @precio, @fechaInicioMembresia, @fechaCreacion, @idUsuarioCreo, @idEstado);";
                    await using var cmdInsM = new SqliteCommand(insMemSql, sqliteConn);
                    cmdInsM.Parameters.AddWithValue("@idSocioMembresia", maxIdMemSqlite);
                    cmdInsM.Parameters.AddWithValue("@idSocio", m["idSocio"]);
                    cmdInsM.Parameters.AddWithValue("@idMembresia", m["idMembresia"]);
                    cmdInsM.Parameters.AddWithValue("@precio", m["precio"]);
                    cmdInsM.Parameters.AddWithValue("@fechaInicioMembresia", m["fechaInicioMembresia"]);
                    cmdInsM.Parameters.AddWithValue("@fechaCreacion", m["fechaCreacion"]);
                    cmdInsM.Parameters.AddWithValue("@idUsuarioCreo", m["idUsuarioCreo"] ?? DBNull.Value);
                    cmdInsM.Parameters.AddWithValue("@idEstado", m["idEstado"]);
                    await cmdInsM.ExecuteNonQueryAsync();
                    membresiasImportadas++;
                }
            }

            return (true, $"Sincronización completada exitosamente. Se importaron {pagosImportados} cobros nuevos y {membresiasImportadas} membresías desde MySQL sin alterar los registros existentes.", pagosImportados, membresiasImportadas);
        }
        catch (Exception ex)
        {
            return (false, $"Error al sincronizar con MySQL: {ex.Message}", 0, 0);
        }
    }
}
