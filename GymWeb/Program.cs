using GymWeb.Helpers;
using GymWeb.Models;
using Microsoft.EntityFrameworkCore;
using Serilog;

// ── Fijar directorio de trabajo al directorio base del ejecutable ────────────
Directory.SetCurrentDirectory(AppContext.BaseDirectory);

// ── Configurar Serilog antes de todo ────────────────────────────────────────
var logDir = Path.Combine(AppContext.BaseDirectory, "logs");
Directory.CreateDirectory(logDir);
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File(
        Path.Combine(logDir, "gymweb-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30)
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();
builder.Host.UseWindowsService();

// Detección automática del motor de Base de Datos (MySQL vs SQLite)
var connStr = builder.Configuration.GetConnectionString("GymDb") ?? "Data Source=gym.db";
bool isMySql = connStr.Contains("Server=", StringComparison.OrdinalIgnoreCase) || 
               connStr.Contains("Database=", StringComparison.OrdinalIgnoreCase);

if (!isMySql)
{
    string dbRaw = connStr.Replace("Data Source=", "", StringComparison.OrdinalIgnoreCase).Trim();
    if (!Path.IsPathRooted(dbRaw))
    {
        string projectDb = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", dbRaw));
        string currentDb = Path.Combine(AppContext.BaseDirectory, dbRaw);
        string absoluteDbPath = File.Exists(projectDb) ? projectDb : currentDb;
        connStr = $"Data Source={absoluteDbPath}";
    }
}

// Manejo de comando CLI para migración de datos de MySQL a SQLite sin levantar la web
if (args.Contains("--migrar-mysql", StringComparer.OrdinalIgnoreCase))
{
    Console.WriteLine("============================================================");
    Console.WriteLine("  GymWeb - Migrador Automatico de Datos: MySQL -> SQLite");
    Console.WriteLine("============================================================");

    var mySqlConnStr = isMySql ? connStr : "Server=localhost;Database=gym;User=root;Password=;";
    var sqliteDbPath = Path.Combine(builder.Environment.ContentRootPath, "gym.db");

    Console.WriteLine($"[1/4] Base de datos origen (MySQL) : {mySqlConnStr}");
    Console.WriteLine($"[2/4] Base de datos destino (SQLite): {sqliteDbPath}");
    Console.WriteLine("[3/4] Inicializando estructura de tablas y vistas SQLite...");

    var optBuilder = new DbContextOptionsBuilder<GymContext>();
    optBuilder.UseSqlite($"Data Source={sqliteDbPath}");
    using (var initDb = new GymContext(optBuilder.Options))
    {
        DbInitializer.Initialize(initDb);
    }

    Console.WriteLine("[4/4] Transfiriendo registros y archivos binarios...");
    var report = await MySqlToSqliteMigrator.MigrateAsync(mySqlConnStr, sqliteDbPath);

    if (report.Success)
    {
        Console.WriteLine("\n[EXITO] " + report.Message);
        Console.WriteLine($"Tiempo total: {report.Duration.TotalSeconds:F2} segundos.\n");
        Console.WriteLine("Resumen detallado por tabla:");
        foreach (var kv in report.TableResults)
        {
            var match = kv.Value.MySqlCount == kv.Value.SqliteCount ? "OK" : "DIFERENCIA";
            Console.WriteLine($"  * {kv.Key,-18}: MySQL={kv.Value.MySqlCount,5} | SQLite={kv.Value.SqliteCount,5} [{match}]");
        }

        if (args.Contains("--switch-config", StringComparer.OrdinalIgnoreCase))
        {
            var configPath = Path.Combine(builder.Environment.ContentRootPath, "appsettings.json");
            MySqlToSqliteMigrator.SwitchToSqliteConfig(configPath, "gym.db");
            var prodConfigPath = Path.Combine(builder.Environment.ContentRootPath, "appsettings.Production.json");
            if (File.Exists(prodConfigPath))
            {
                MySqlToSqliteMigrator.SwitchToSqliteConfig(prodConfigPath, "gym.db");
            }
            Console.WriteLine("\n[CONFIGURACION] Configuracion actualizada a: Data Source=gym.db");
            Console.WriteLine("A partir de ahora el sistema trabajará 100% en modo autónomo sin XAMPP.");
        }
    }
    else
    {
        Console.WriteLine("\n[ERROR] No se pudo completar la migración:\n" + report.Message);
    }

    return;
}

builder.Services.AddDbContext<GymContext>(options =>
{
    if (isMySql)
    {
        options.UseMySql(connStr, ServerVersion.AutoDetect(connStr));
    }
    else
    {
        options.UseSqlite(connStr);
    }
});

// Session-based authentication
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(60);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddHttpClient();
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Load persistent settings from DB & Auto-migration
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<GymContext>();

    // Inicialización según el motor activo
    if (!isMySql)
    {
        // SQLite: Asegurar tablas locales, vistas de SQLite y datos semilla iniciales
        DbInitializer.Initialize(db);
    }
    else
    {
        // MySQL: Auto-migración para asegurar tablas y columnas añadidas en v2.0+
        try
        {
            db.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS socio_huella (
                  idHuella int(11) NOT NULL AUTO_INCREMENT,
                  idSocio int(11) NOT NULL,
                  pin int(11) NOT NULL,
                  template longblob DEFAULT NULL,
                  idDispositivo varchar(50) DEFAULT NULL,
                  idEstado int(11) DEFAULT 1,
                  fechaRegistro datetime DEFAULT CURRENT_TIMESTAMP,
                  PRIMARY KEY (idHuella),
                  UNIQUE KEY uq_huella_socio (idSocio),
                  UNIQUE KEY uq_huella_pin (pin)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
            ");
            db.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS pago (
                  idPago int(11) NOT NULL AUTO_INCREMENT,
                  idSocio int(11) NOT NULL,
                  idMembresia int(11) DEFAULT NULL,
                  monto decimal(10,2) NOT NULL,
                  metodoPago varchar(50) NOT NULL DEFAULT 'Efectivo',
                  fechaPago date NOT NULL,
                  notas text DEFAULT NULL,
                  idEstado int(11) NOT NULL DEFAULT 1,
                  fechaCreacion datetime DEFAULT CURRENT_TIMESTAMP,
                  idUsuarioCreo int(11) DEFAULT NULL,
                  PRIMARY KEY (idPago)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
            ");
            db.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS visita (
                  idVisita int(11) NOT NULL AUTO_INCREMENT,
                  idSocio int(11) DEFAULT NULL,
                  fechaCreacion datetime DEFAULT CURRENT_TIMESTAMP,
                  precioVisita decimal(8,2) DEFAULT NULL,
                  PRIMARY KEY (idVisita)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
            ");
        }
        catch { }

        try { db.Database.ExecuteSqlRaw("ALTER TABLE socio_huella ADD COLUMN IF NOT EXISTS template LONGBLOB NULL COMMENT 'Template biometrico' AFTER pin;"); } catch { }
        try { db.Database.ExecuteSqlRaw("ALTER TABLE producto ADD COLUMN IF NOT EXISTS categoria VARCHAR(50) DEFAULT 'Otro';"); } catch { }
        try { db.Database.ExecuteSqlRaw("ALTER TABLE producto ADD COLUMN IF NOT EXISTS stock INT DEFAULT 0;"); } catch { }
        try { db.Database.ExecuteSqlRaw("ALTER TABLE producto ADD COLUMN IF NOT EXISTS stockMinimo INT DEFAULT 5;"); } catch { }
        try { db.Database.ExecuteSqlRaw("ALTER TABLE producto ADD COLUMN IF NOT EXISTS imagen_url VARCHAR(255) NULL;"); } catch { }

        try { db.Database.ExecuteSqlRaw("ALTER TABLE salida ADD COLUMN IF NOT EXISTS metodoPago VARCHAR(50) DEFAULT 'Efectivo';"); } catch { }
        try { db.Database.ExecuteSqlRaw("ALTER TABLE salida ADD COLUMN IF NOT EXISTS folio VARCHAR(50) NULL;"); } catch { }
        try { db.Database.ExecuteSqlRaw("ALTER TABLE salida ADD COLUMN IF NOT EXISTS descuento DECIMAL(8,2) DEFAULT 0;"); } catch { }
        try { db.Database.ExecuteSqlRaw("ALTER TABLE salida ADD COLUMN IF NOT EXISTS montoRecibido DECIMAL(8,2) NULL;"); } catch { }
        try { db.Database.ExecuteSqlRaw("ALTER TABLE salida ADD COLUMN IF NOT EXISTS cambio DECIMAL(8,2) NULL;"); } catch { }
        try { db.Database.ExecuteSqlRaw("ALTER TABLE salida ADD COLUMN IF NOT EXISTS referencia VARCHAR(100) NULL;"); } catch { }
        try { db.Database.ExecuteSqlRaw("ALTER TABLE salida ADD COLUMN IF NOT EXISTS notas VARCHAR(255) NULL;"); } catch { }
        try { db.Database.ExecuteSqlRaw("ALTER TABLE salida ADD COLUMN IF NOT EXISTS idSocio INT NULL;"); } catch { }
        try { db.Database.ExecuteSqlRaw("ALTER TABLE salida ADD COLUMN IF NOT EXISTS esCredito TINYINT(1) DEFAULT 0;"); } catch { }
        try { db.Database.ExecuteSqlRaw("ALTER TABLE salida ADD COLUMN IF NOT EXISTS saldoPendiente DECIMAL(8,2) DEFAULT 0;"); } catch { }
        try { db.Database.ExecuteSqlRaw("ALTER TABLE salida ADD COLUMN IF NOT EXISTS fechaLiquidacion DATETIME NULL;"); } catch { }

        try { db.Database.ExecuteSqlRaw("ALTER TABLE configuracion ADD COLUMN IF NOT EXISTS precio_visita DECIMAL(8,2) DEFAULT 0;"); } catch { }
        try { db.Database.ExecuteSqlRaw("ALTER TABLE registro ADD COLUMN IF NOT EXISTS nombre_visita VARCHAR(150) NULL;"); } catch { }
        try { db.Database.ExecuteSqlRaw("ALTER TABLE registro ADD COLUMN IF NOT EXISTS precio_visita DECIMAL(8,2) NULL;"); } catch { }
    }

    var cfg = db.Configuracions.FirstOrDefault();
    if (!string.IsNullOrWhiteSpace(cfg?.NombreGimnacio))
        AppSettings.GymNombre = cfg.NombreGimnacio;
    if (!string.IsNullOrWhiteSpace(cfg?.Domicilio))
        AppSettings.GymDomicilio = cfg.Domicilio;
    if (!string.IsNullOrWhiteSpace(cfg?.Telefono))
        AppSettings.GymTelefono = cfg.Telefono;
    if (!string.IsNullOrWhiteSpace(cfg?.Mensaje))
        AppSettings.GymPieTicket = cfg.Mensaje;
    if (cfg?.MensajeVencimiento.HasValue == true)
        AppSettings.DiasAviso = cfg.MensajeVencimiento.Value;

    var webRoot = app.Environment.WebRootPath ?? Path.Combine(AppContext.BaseDirectory, "wwwroot");
    var logoPath = Path.Combine(webRoot, "img", "logo-custom.png");
    if (!File.Exists(logoPath) && cfg?.Logo != null && cfg.Logo.Length > 0)
    {
        try
        {
            File.WriteAllBytes(logoPath, cfg.Logo);
        }
        catch { }
    }
}
catch (Exception ex)
{
    Log.Warning(ex, "[DB Startup] Advertencia al inicializar la base de datos: {Mensaje}", ex.Message);
}
var activeWebRoot = app.Environment.WebRootPath ?? Path.Combine(AppContext.BaseDirectory, "wwwroot");
AppSettings.HasCustomBg = File.Exists(
    Path.Combine(activeWebRoot, "img", "fondo-custom.jpg"));
AppSettings.HasCustomLogo = File.Exists(
    Path.Combine(activeWebRoot, "img", "logo-custom.png"));
AppSettings.LicenseWhatsApp = app.Configuration["LicenseWhatsApp"] ?? "";

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthorization();

// ── Middleware de Verificación de Licencia (Bloqueo tras 7 días de prueba) ──
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? "";

    // 1. Permitir siempre archivos estáticos
    if (path.StartsWith("/css", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/js", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/lib", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/img", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/favicon", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".ico", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".woff", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".woff2", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase))
    {
        await next();
        return;
    }

    // 2. Permitir siempre autenticación (Login / Logout)
    if (path.StartsWith("/Account", StringComparison.OrdinalIgnoreCase))
    {
        await next();
        return;
    }

    // 3. Si no ha iniciado sesión, redirigir la raíz a /Account/Login
    if (string.IsNullOrEmpty(context.Session.GetString("UsuarioId")))
    {
        if (path == "/" || path == "")
        {
            context.Response.Redirect("/Account/Login");
            return;
        }
        await next();
        return;
    }

    // 4. Verificar estado de la licencia
    var lic = LicenseService.GetStatus();
    if (lic.Valid)
    {
        await next();
        return;
    }

    // 5. Si la licencia expiró o no es válida:
    // Permitir acceso a la pantalla de Configuración y activación de clave
    if (path.StartsWith("/Configuracion", StringComparison.OrdinalIgnoreCase))
    {
        await next();
        return;
    }

    // 6. Para cualquier otra ruta operativa (Dashboard, Socios, Pagos, Ventas, Reportes, etc.):
    bool isAjax = context.Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
                 (context.Request.Headers.Accept.ToString()?.Contains("application/json") ?? false);

    if (isAjax)
    {
        context.Response.StatusCode = 403;
        context.Response.ContentType = "application/json; charset=utf-8";
        string ajaxMsg = lic.IsFirstInstall
            ? "¡Bienvenido a GymPro! Por favor activa tu prueba gratuita de 7 días en Configuración > Licencia."
            : "Tu período de prueba de 7 días ha finalizado. Por favor ingresa a Configuración > Licencia para activar tu clave vitalicia.";

        await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(new
        {
            ok = false,
            expired = lic.IsTrialExpired,
            isFirstInstall = lic.IsFirstInstall,
            msg = ajaxMsg
        }));
        return;
    }

    // Redirigir navegación normal a la pestaña de Licencia en Configuración
    context.Response.Redirect("/Configuracion/Index?tab=licencia");
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
