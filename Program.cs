using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaChotaExpress.Data;
using SistemaChotaExpress.Models;
using SistemaChotaExpress.Services;

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

// 2. REGISTRAR CONEXION EF CORE - POSTGRESQL (Neon.tech en Railway)
// En Railway: variable de entorno DATABASE_URL con formato PostgreSQL de Neon.tech
// En desarrollo local: cadena en appsettings.json
var connectionString =
    Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? builder.Configuration.GetConnectionString("CadenaChotaExpress")
    ?? throw new InvalidOperationException("No se encontro la cadena de conexion de la base de datos.");

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(connectionString);
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

// Aplicar cabeceras de proxy inverso de inmediato (convierte http a https y detecta dominio publico)
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
        // (incluyendo TipoComprobante, RucEmpresa, MetodoPago, NombreVehiculo, etc.)
        context.Database.EnsureCreated();

        // Asegurar salidas en segundo plano sin bloquear el arranque del servidor web
        _ = Task.Run(async () =>
        {
            try
            {
                using var bgScope = app.Services.CreateScope();
                var bgContext = bgScope.ServiceProvider.GetRequiredService<AppDbContext>();
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
