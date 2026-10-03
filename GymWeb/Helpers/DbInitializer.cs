using GymWeb.Models;
using Microsoft.EntityFrameworkCore;

namespace GymWeb.Helpers;

public static class DbInitializer
{
    public static void Initialize(GymContext db)
    {
        // 1. Asegurar que las tablas físicas existan según los modelos EF Core
        db.Database.EnsureCreated();

        // 2. Crear tabla auxiliar visita (utilizada en controladores sin modelo EF específico)
        db.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS visita (
                idVisita INTEGER PRIMARY KEY AUTOINCREMENT,
                idSocio INTEGER NULL,
                fechaCreacion TEXT NULL,
                precioVisita REAL NULL
            );
        ");

        // 2b. Asegurar columnas en la tabla configuracion (Logo, Domicilio, Telefono, Mensaje)
        try { db.Database.ExecuteSqlRaw("ALTER TABLE configuracion ADD COLUMN Logo BLOB NULL;"); } catch { }
        try { db.Database.ExecuteSqlRaw("ALTER TABLE configuracion ADD COLUMN Domicilio TEXT NULL;"); } catch { }
        try { db.Database.ExecuteSqlRaw("ALTER TABLE configuracion ADD COLUMN Telefono TEXT NULL;"); } catch { }
        try { db.Database.ExecuteSqlRaw("ALTER TABLE configuracion ADD COLUMN Mensaje TEXT NULL;"); } catch { }

        // 2c. Asegurar columnas de salida (metodoPago, folio, descuento, montoRecibido, cambio, referencia, notas)
        string[] alterSalida = new[]
        {
            "ALTER TABLE salida ADD COLUMN metodoPago TEXT NULL DEFAULT 'Efectivo';",
            "ALTER TABLE salida ADD COLUMN folio TEXT NULL;",
            "ALTER TABLE salida ADD COLUMN descuento REAL NULL DEFAULT 0;",
            "ALTER TABLE salida ADD COLUMN montoRecibido REAL NULL;",
            "ALTER TABLE salida ADD COLUMN cambio REAL NULL;",
            "ALTER TABLE salida ADD COLUMN referencia TEXT NULL;",
            "ALTER TABLE salida ADD COLUMN notas TEXT NULL;",
            "ALTER TABLE salida ADD COLUMN idSocio INTEGER NULL;",
            "ALTER TABLE salida ADD COLUMN esCredito INTEGER NULL DEFAULT 0;",
            "ALTER TABLE salida ADD COLUMN saldoPendiente REAL NULL DEFAULT 0;",
            "ALTER TABLE salida ADD COLUMN fechaLiquidacion TEXT NULL;"
        };
        foreach (var sql in alterSalida)
        {
            try { db.Database.ExecuteSqlRaw(sql); } catch { }
        }

        // 2d. Asegurar columna imagen_url en producto
        try { db.Database.ExecuteSqlRaw("ALTER TABLE producto ADD COLUMN imagen_url TEXT NULL;"); } catch { }

        // 3. Configurar secuencia para que los socios comiencen en la clave 1001 (si la tabla está vacía)
        try
        {
            var totalSocios = db.Socios.Count();
            if (totalSocios == 0)
            {
                db.Database.ExecuteSqlRaw(@"
                    INSERT OR REPLACE INTO sqlite_sequence (name, seq) VALUES ('socio', 1000);
                ");
            }
        }
        catch { }

        // 4. Crear las vistas de SQLite requeridas por la aplicación
        CreateViews(db);

        // 5. Sembrar datos iniciales si no existen
        SeedData(db);
    }

    private static void CreateViews(GymContext db)
    {
        // Vista de Socios con su Estado
        db.Database.ExecuteSqlRaw(@"
            CREATE VIEW IF NOT EXISTS vwsocios AS
            SELECT 
                s.idSocio,
                s.idEstado,
                s.fechaCreacion,
                s.Nombre,
                s.Paterno,
                s.Materno,
                s.Telefono,
                s.Observaciones,
                s.idUsuarioCreo,
                s.foto,
                e.Estado
            FROM socio s
            JOIN estado e ON s.idEstado = e.idEstados;
        ");

        // Vista de Membresías con Estado
        db.Database.ExecuteSqlRaw(@"
            CREATE VIEW IF NOT EXISTS vwmembresias AS
            SELECT 
                m.idMembresia,
                m.Nombre,
                m.idEstado,
                m.fechaCreacion,
                m.Precio,
                m.idUsuarioCreo,
                m.meses,
                m.horaInicio,
                m.horaFinal,
                e.Estado
            FROM membresia m
            JOIN estado e ON m.idEstado = e.idEstados;
        ");

        // Vista de Productos con Estado
        db.Database.ExecuteSqlRaw(@"
            CREATE VIEW IF NOT EXISTS vwproductos AS
            SELECT 
                p.idProducto,
                p.Nombre,
                p.Descripcion,
                p.idEstado,
                p.fechaCreacion,
                p.Precio,
                p.idUsuarioCreo,
                e.Estado,
                p.Costo
            FROM producto p
            JOIN estado e ON p.idEstado = e.idEstados;
        ");

        // Vista de Historial de Membresías de Socios
        db.Database.ExecuteSqlRaw(@"
            CREATE VIEW IF NOT EXISTS vwsociomembresias AS
            SELECT 
                sm.idSocioMembresia,
                sm.idEstado,
                sm.fechaCreacion,
                sm.idUsuarioCreo,
                sm.idSocio,
                sm.idMembresia,
                sm.Precio,
                sm.fechaInicioMembresia,
                e.Estado,
                m.Nombre AS NombreMembresia,
                m.meses,
                s.Nombre AS NombreSocio,
                s.Paterno,
                s.Materno,
                s.Telefono,
                s.Observaciones,
                datetime(sm.fechaInicioMembresia, '+' || coalesce(m.meses, 0) || ' days') AS Vencimiento,
                s.foto,
                m.horaInicio,
                m.horaFinal
            FROM sociomembresia sm
            JOIN estado e ON sm.idEstado = e.idEstados
            LEFT JOIN membresia m ON sm.idMembresia = m.idMembresia
            LEFT JOIN socio s ON sm.idSocio = s.idSocio;
        ");

        // Vista de Última Membresía por Socio
        db.Database.ExecuteSqlRaw(@"
            CREATE VIEW IF NOT EXISTS vwultimamembresia AS
            SELECT 
                max(sm.idSocioMembresia) AS idSocioMembresia,
                sm.idSocio
            FROM sociomembresia sm
            JOIN socio s ON sm.idSocio = s.idSocio
            WHERE sm.idEstado = 1 AND s.idEstado = 1
            GROUP BY sm.idSocio;
        ");

        // Vista Detallada de la Última Membresía por Socio (Check-in y Dashboard)
        db.Database.ExecuteSqlRaw(@"
            CREATE VIEW IF NOT EXISTS vwultimamembresiadetallada AS
            SELECT 
                um.idSocioMembresia,
                um.idSocio,
                vsm.idEstado,
                vsm.fechaCreacion,
                vsm.idUsuarioCreo,
                vsm.idMembresia,
                vsm.Precio,
                vsm.Estado,
                vsm.NombreMembresia,
                vsm.fechaInicioMembresia,
                vsm.meses,
                vsm.Paterno,
                vsm.NombreSocio,
                vsm.Materno,
                vsm.Telefono,
                vsm.Observaciones,
                vsm.Vencimiento,
                vsm.foto,
                vsm.horaInicio,
                vsm.horaFinal
            FROM vwultimamembresia um
            JOIN vwsociomembresias vsm ON vsm.idSocioMembresia = um.idSocioMembresia;
        ");

        // Vista de Usuarios
        db.Database.ExecuteSqlRaw(@"
            CREATE VIEW IF NOT EXISTS vwusuarios AS
            SELECT 
                e.Estado,
                u.idUsuario,
                u.idEstado,
                u.Usuario,
                u.Nombre,
                u.fechaCreacion,
                u.Password
            FROM usuario u
            JOIN estado e ON u.idEstado = e.idEstados
            WHERE u.idEstado IN (1, 2);
        ");
    }

    private static void SeedData(GymContext db)
    {
        // 1. Estados
        if (!db.Estados.Any())
        {
            db.Estados.AddRange(
                new Estado { IdEstados = 1, Estado1 = "Activo" },
                new Estado { IdEstados = 2, Estado1 = "Inactivo" }
            );
            db.SaveChanges();
        }

        // 2. Configuración General inicial limpia
        if (!db.Configuracions.Any())
        {
            db.Configuracions.Add(new Configuracion
            {
                IdConfiguracion = 1,
                NombreGimnacio = "Mi Gimnasio",
                Domicilio = "",
                Telefono = "",
                Logo = null,
                MensajeVencimiento = 5,
                PrecioVisita = 50.00m,
                FechaModificacion = DateTime.Now
            });
            db.SaveChanges();
        }

        // 3. Usuario Administrador por defecto con clave 'a'
        if (!db.Usuarios.Any())
        {
            db.Usuarios.Add(new Usuario
            {
                IdUsuario = 1,
                IdEstado = 1,
                Usuario1 = "admin",
                Nombre = "Administrador",
                Rol = "superadmin",
                // Clave por defecto 'a'
                Password = BCrypt.Net.BCrypt.HashPassword("a"),
                FechaCreacion = DateTime.Now
            });
            db.SaveChanges();
        }

        // 4. Membresías iniciales si no hay ninguna
        if (!db.Membresia.Any())
        {
            db.Membresia.AddRange(
                new Membresium
                {
                    IdMembresia = 1,
                    Nombre = "Visita de Día",
                    Meses = 1, // 1 día
                    Precio = 50.00m,
                    IdEstado = 1,
                    IdUsuarioCreo = 1,
                    FechaCreacion = DateTime.Now
                },
                new Membresium
                {
                    IdMembresia = 2,
                    Nombre = "Mensualidad Completa",
                    Meses = 30, // 30 días
                    Precio = 450.00m,
                    IdEstado = 1,
                    IdUsuarioCreo = 1,
                    FechaCreacion = DateTime.Now
                }
            );
            db.SaveChanges();
        }
    }
}
