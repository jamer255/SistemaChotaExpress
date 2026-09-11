using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SistemaChotaExpress.Data;
using SistemaChotaExpress.Models;
using SistemaChotaExpress.Services;

// Soporte universal para DateTime en PostgreSQL
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// Soporte para Proxy Inverso (Railway, Cloudflare Tunnel, ngrok, etc.)
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.All;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// Configuracion de Antiforgery para permitir navegacion por tunel HTTPS y localhost
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.HttpOnly = true;
});

// 1. FILTRO ANTI-CACHE GLOBAL (Evita el boton atras tras Logout)
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new ResponseCacheAttribute
    {
        NoStore = true,
        Location = ResponseCacheLocation.None
    });
});

// 2. REGISTRAR CONEXION EF CORE - POSTGRESQL (Railway / Local)
// Resuelve tanto cadenas standard como URIs estilo postgresql://user:pass@host:port/db
string rawConn =
    Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? Environment.GetEnvironmentVariable("DATABASE_PUBLIC_URL")
    ?? builder.Configuration.GetConnectionString("CadenaChotaExpress")
    ?? "";

string finalConn = rawConn;
if (rawConn.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
    rawConn.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
{
    try
    {
        var uri = new Uri(rawConn);
        var userInfo = uri.UserInfo.Split(':');
        var csb = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "",
            Database = uri.AbsolutePath.TrimStart('/'),
            SslMode = SslMode.Prefer,
            TrustServerCertificate = true
        };
        finalConn = csb.ConnectionString;
    }
    catch { /* Mantener rawConn si falla el parseo */ }
}

builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (!string.IsNullOrWhiteSpace(finalConn))
    {
        options.UseNpgsql(finalConn);
    }
});

// Registrar Servicios de la Aplicacion
builder.Services.AddHttpClient();
builder.Services.AddScoped<IDocumentLookupService, DocumentLookupService>();
builder.Services.AddScoped<SunatService>();

// 3. REGISTRAR SESIONES EN MEMORIA
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(2);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

// 4. ESQUEMA DE SEGURIDAD POR COOKIES MANUALES
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Login/Login";
        options.AccessDeniedPath = "/Login/AccesoDenegado";
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.HttpOnly = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

// Configurar el puerto desde la variable PORT de Railway (o 8080 por defecto)
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

var app = builder.Build();

// Aplicar cabeceras de proxy inverso de inmediato
app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();
app.UseRouting();

// Orden obligatorio de Middleware
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Login}/{action=Login}/{id?}");

// Inicializacion automatica de la base de datos y datos semilla
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<AppDbContext>();

        // EnsureCreated crea todas las tablas y columnas del modelo actual en PostgreSQL
        context.Database.EnsureCreated();

        // Asegurar que la tabla Encomiendas exista en bases de datos PostgreSQL ya existentes
        try
        {
            context.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS ""Encomiendas"" (
                    ""Id_Encomienda"" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                    ""CodigoSeguimiento"" character varying(20) NOT NULL,
                    ""NombreRemitente"" character varying(100) NOT NULL,
                    ""DniRemitente"" character varying(15),
                    ""TelefonoRemitente"" character varying(15),
                    ""NombreDestinatario"" character varying(100) NOT NULL,
                    ""DniDestinatario"" character varying(15),
                    ""TelefonoDestinatario"" character varying(15),
                    ""Descripcion"" character varying(200) NOT NULL,
                    ""PesoKg"" numeric(8,2),
                    ""PrecioEnvio"" numeric(10,2) NOT NULL,
                    ""Id_Ruta"" integer,
                    ""Estado"" character varying(20) NOT NULL DEFAULT 'Registrado',
                    ""Observaciones"" character varying(300),
                    ""FechaRegistro"" timestamp without time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    ""FechaEntrega"" timestamp without time zone,
                    ""Id_Usuario"" integer
                );
                CREATE INDEX IF NOT EXISTS ""IX_Encomiendas_Id_Ruta"" ON ""Encomiendas"" (""Id_Ruta"");
                CREATE INDEX IF NOT EXISTS ""IX_Encomiendas_Id_Usuario"" ON ""Encomiendas"" (""Id_Usuario"");
            ");
        }
        catch (Exception exTable)
        {
            var logger = services.GetRequiredService<ILogger<Program>>();
            logger.LogWarning(exTable, "Aviso verificando/creando tabla Encomiendas");
        }

        // Asegurar salidas en segundo plano sin bloquear el arranque del servidor web
        _ = Task.Run(async () =>
        {
            try
            {
                using var bgScope = app.Services.CreateScope();
                var bgContext = bgScope.ServiceProvider.GetRequiredService<AppDbContext>();
                await GeneradorViajesService.AsegurarRutasOficialesAsync(bgContext);
                await GeneradorViajesService.AsegurarViajesParaFechaAsync(bgContext, DateTime.Today);
            }
            catch { }
        });
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Error durante la inicializacion o conexion con la base de datos.");
    }
}

app.Run();
