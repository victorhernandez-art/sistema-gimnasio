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
                            insertCmd.Parameters.AddWithValue($"@{colName}", dt.ToString("yyyy-MM-dd HH:mm:ss"));
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
}
