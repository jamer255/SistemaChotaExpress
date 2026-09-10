using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SistemaChotaExpress.Data;
using SistemaChotaExpress.Models;
using SistemaChotaExpress.Services;
using SistemaChotaExpress.ViewModels;

namespace SistemaChotaExpress.Controllers
{
    [Authorize(Roles = "Gerente")]
    public class GerenteController : Controller
    {
        private readonly AppDbContext _context;

        public GerenteController(AppDbContext context)
        {
            _context = context;
        }

        // ==========================================
        // 1. DASHBOARD & REPORTES DE VENTAS
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {
            // Valores por defecto seguros (si falla alguna consulta, el Dashboard igual carga)
            ViewBag.VentasHoy = 0m;
            ViewBag.CantidadHoy = 0;
            ViewBag.VentasMes = 0m;
            ViewBag.VentasAnio = 0m;
            ViewBag.BusesCount = 0;
            ViewBag.RutasCount = 0;
            ViewBag.ViajesCount = 0;
            ViewBag.TrabajadoresCount = 0;
            ViewBag.DiasLabels = Array.Empty<string>();
            ViewBag.DiasValores = Array.Empty<decimal>();
            ViewBag.MesesLabels = new[] { "Ene","Feb","Mar","Abr","May","Jun","Jul","Ago","Sep","Oct","Nov","Dic" };
            ViewBag.MesesValores = new decimal[12];
            ViewBag.ViajesMasVendidos = new List<RutaMasVendidaVM>();
            // Encomiendas defaults
            ViewBag.EncomiendaHoy = 0;
            ViewBag.EncomiendaMes = 0m;
            ViewBag.EncomiendaPendientes = 0;

            try
            {
                var hoy = DateTime.Today;
                var manana = hoy.AddDays(1);
                var inicioMes = new DateTime(hoy.Year, hoy.Month, 1);
                var inicioAnio = new DateTime(hoy.Year, 1, 1);

                // KPIs
                ViewBag.VentasHoy = await _context.Ventas
                    .Where(v => v.Estado == "Vendido" && v.FechaVenta >= hoy && v.FechaVenta < manana)
                    .SumAsync(v => (decimal?)v.PrecioPagado) ?? 0;

                ViewBag.CantidadHoy = await _context.Ventas
                    .Where(v => v.Estado == "Vendido" && v.FechaVenta >= hoy && v.FechaVenta < manana)
                    .CountAsync();

                ViewBag.VentasMes = await _context.Ventas
                    .Where(v => v.Estado == "Vendido" && v.FechaVenta >= inicioMes)
                    .SumAsync(v => (decimal?)v.PrecioPagado) ?? 0;

                ViewBag.VentasAnio = await _context.Ventas
                    .Where(v => v.Estado == "Vendido" && v.FechaVenta >= inicioAnio)
                    .SumAsync(v => (decimal?)v.PrecioPagado) ?? 0;

                ViewBag.BusesCount = await _context.Buses.CountAsync();
                ViewBag.RutasCount = await _context.Rutas.CountAsync();
                ViewBag.ViajesCount = await _context.Viajes.Where(v => v.Estado == "Programado").CountAsync();
                ViewBag.TrabajadoresCount = await _context.Usuarios.Where(u => u.Id_Rol == 2).CountAsync();

                // Ventas diarias (últimos 15 días) - en memoria para PostgreSQL
                var limiteDias = DateTime.Today.AddDays(-14);
                var ventasRaw = await _context.Ventas
                    .Where(v => v.Estado == "Vendido" && v.FechaVenta >= limiteDias)
                    .Select(v => new { v.FechaVenta, v.PrecioPagado })
                    .ToListAsync();

                var ventasDiariasList = ventasRaw
                    .GroupBy(v => v.FechaVenta.Date)
                    .Select(g => new { Fecha = g.Key, Total = g.Sum(v => v.PrecioPagado) })
                    .OrderBy(g => g.Fecha)
                    .ToList();

                var fechasGrafico = Enumerable.Range(0, 15)
                    .Select(offset => DateTime.Today.AddDays(-14 + offset))
                    .ToList();

                var datosGraficoDias = fechasGrafico.Select(f => new {
                    Label = f.ToString("dd/MM"),
                    Total = ventasDiariasList.FirstOrDefault(vd => vd.Fecha == f)?.Total ?? 0
                }).ToList();

                ViewBag.DiasLabels = datosGraficoDias.Select(d => d.Label).ToArray();
                ViewBag.DiasValores = datosGraficoDias.Select(d => d.Total).ToArray();

                // Ventas mensuales (año actual) - en memoria para PostgreSQL
                var ventasMesesRaw = await _context.Ventas
                    .Where(v => v.Estado == "Vendido" && v.FechaVenta >= inicioAnio)
                    .Select(v => new { v.FechaVenta, v.PrecioPagado })
                    .ToListAsync();

                var ventasMensualesList = ventasMesesRaw
                    .GroupBy(v => v.FechaVenta.Month)
                    .Select(g => new { Mes = g.Key, Total = g.Sum(v => v.PrecioPagado) })
                    .ToList();

                string[] nombreMeses = { "Ene","Feb","Mar","Abr","May","Jun","Jul","Ago","Sep","Oct","Nov","Dic" };
                decimal[] valoresMeses = new decimal[12];
                for (int i = 0; i < 12; i++)
                {
                    var mesNum = i + 1;
                    valoresMeses[i] = ventasMensualesList.FirstOrDefault(vm => vm.Mes == mesNum)?.Total ?? 0;
                }
                ViewBag.MesesLabels = nombreMeses;
                ViewBag.MesesValores = valoresMeses;

                // Top rutas - en memoria para PostgreSQL
                var ventasConRutas = await _context.Ventas
                    .Where(v => v.Estado == "Vendido" && v.Id_Viaje != null)
                    .Select(v => new {
                        v.PrecioPagado,
                        v.ObjetoViaje
                    })
                    .ToListAsync();

                var topRutas = ventasConRutas
                    .Where(v => v.ObjetoViaje?.ObjetoRuta != null)
                    .GroupBy(v => new {
                        Origen = v.ObjetoViaje!.ObjetoRuta!.Origen,
                        Destino = v.ObjetoViaje.ObjetoRuta.Destino
                    })
                    .Select(g => new RutaMasVendidaVM {
                        Ruta = $"{g.Key.Origen} - {g.Key.Destino}",
                        Cantidad = g.Count(),
                        Monto = g.Sum(v => v.PrecioPagado)
                    })
                    .OrderByDescending(g => g.Monto)
                    .Take(5)
                    .ToList();

                ViewBag.ViajesMasVendidos = topRutas;

                // KPIs de Encomiendas
                ViewBag.EncomiendaHoy = await _context.Encomiendas
                    .Where(e => e.FechaRegistro >= hoy && e.FechaRegistro < manana)
                    .CountAsync();

                ViewBag.EncomiendaMes = await _context.Encomiendas
                    .Where(e => e.FechaRegistro >= inicioMes)
                    .SumAsync(e => (decimal?)e.PrecioEnvio) ?? 0;

                ViewBag.EncomiendaPendientes = await _context.Encomiendas
                    .Where(e => e.Estado == "Registrado" || e.Estado == "En transito")
                    .CountAsync();
            }
            catch (Exception ex)
            {
                // Log el error pero no rompe la página — el Dashboard carga con valores en cero
                var logger = HttpContext.RequestServices.GetRequiredService<ILogger<GerenteController>>();
                logger.LogError(ex, "Error cargando datos del Dashboard del Gerente");
            }


            return View();
        }

        // ==========================================
        // 2. TRABAJADORES (CRUD)
        // ==========================================
        // ==========================================
        // 2. CUENTAS DE USUARIOS / TRABAJADORES (CRUD)
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Trabajadores()
        {
            var usuarios = await _context.Usuarios
                .Include(u => u.ObjetoRol)
                .OrderBy(u => u.Id_Rol)
                .ThenBy(u => u.Name)
                .ToListAsync();
            return View(usuarios);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearTrabajador(VendedorVM modelo)
        {
            if (string.IsNullOrWhiteSpace(modelo.Name) || string.IsNullOrWhiteSpace(modelo.Password) || string.IsNullOrWhiteSpace(modelo.Email))
            {
                TempData["ErrorMessage"] = "Todos los campos son obligatorios.";
                return RedirectToAction("Trabajadores");
            }

            if (modelo.Password != modelo.RepetPassword)
            {
                TempData["ErrorMessage"] = "Las contraseñas no coinciden.";
                return RedirectToAction("Trabajadores");
            }

            var emailLimpio = modelo.Email.Trim().ToLower();
            var existe = await _context.Usuarios.AnyAsync(u => u.Email == emailLimpio);
            if (existe)
            {
                TempData["ErrorMessage"] = "El correo electrónico ya está registrado.";
                return RedirectToAction("Trabajadores");
            }

            var rolAsignado = (modelo.Id_Rol == 1 || modelo.Id_Rol == 2) ? modelo.Id_Rol : 2;
            var nombreRol = (rolAsignado == 1) ? "Gerente" : "Vendedor";

            var nuevoUsuario = new Usuario
            {
                Name = modelo.Name.Trim(),
                Email = emailLimpio,
                Password = modelo.Password,
                Telefono = modelo.Telefono ?? "999999999",
                Id_Rol = rolAsignado
            };

            _context.Usuarios.Add(nuevoUsuario);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"¡Cuenta de {nombreRol} '{modelo.Name}' creada con éxito! Ya puede iniciar sesión.";
            return RedirectToAction("Trabajadores");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarTrabajador(int id)
        {
            var usuario = await _context.Usuarios.FindAsync(id);
            if (usuario == null) return NotFound();

            // Evitar que el usuario actual se elimine a sí mismo
            var emailActual = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
            if (!string.IsNullOrEmpty(emailActual) && usuario.Email.Equals(emailActual, StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = "No puede eliminar la cuenta con la que ha iniciado sesión actualmente.";
                return RedirectToAction("Trabajadores");
            }

            try
            {
                // Si el usuario tiene ventas asociadas, reasignar a la cuenta del Gerente actual
                var ventasUsuario = await _context.Ventas.Where(v => v.Id_Usuario == id).ToListAsync();
                if (ventasUsuario.Any())
                {
                    var usuarioReemplazo = await _context.Usuarios.FirstOrDefaultAsync(u => u.Email == emailActual)
                                         ?? await _context.Usuarios.FirstOrDefaultAsync(u => u.Id_Rol == 1 && u.Id_Usuario != id);
                    if (usuarioReemplazo != null)
                    {
                        foreach (var v in ventasUsuario)
                        {
                            v.Id_Usuario = usuarioReemplazo.Id_Usuario;
                        }
                        await _context.SaveChangesAsync();
                    }
                }

                _context.Usuarios.Remove(usuario);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Cuenta de '{usuario.Name}' eliminada con éxito.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"No se pudo eliminar la cuenta: {ex.Message}";
            }

            return RedirectToAction("Trabajadores");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(int id, string newPassword)
        {
            if (string.IsNullOrWhiteSpace(newPassword))
            {
                TempData["ErrorMessage"] = "La nueva contraseña no puede estar vacía.";
                return RedirectToAction("Trabajadores");
            }

            var usuario = await _context.Usuarios.FindAsync(id);
            if (usuario == null) return NotFound();

            usuario.Password = newPassword;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Contraseña de {usuario.Name} restablecida con éxito.";
            return RedirectToAction("Trabajadores");
        }

        // ==========================================
        // 3. BUSES (CRUD)
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Buses()
        {
            var buses = await _context.Buses.ToListAsync();
            return View(buses);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearBus(Bus model)
        {
            ModelState.Remove("Placa");

            if (string.IsNullOrWhiteSpace(model.Placa))
            {
                model.Placa = "S/P";
            }

            if (string.IsNullOrWhiteSpace(model.NombreVehiculo))
            {
                model.NombreVehiculo = "Vehículo " + (await _context.Buses.CountAsync() + 1);
            }

            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Datos del vehículo inválidos.";
                return RedirectToAction("Buses");
            }

            _context.Buses.Add(model);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Vehículo registrado exitosamente.";
            return RedirectToAction("Buses");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarBus(int id)
        {
            var bus = await _context.Buses.FindAsync(id);
            if (bus == null) return NotFound();

            var tieneViajes = await _context.Viajes.AnyAsync(v => v.Id_Bus == id);
            if (tieneViajes)
            {
                TempData["ErrorMessage"] = "No se puede eliminar el vehículo porque tiene viajes programados.";
                return RedirectToAction("Buses");
            }

            _context.Buses.Remove(bus);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Vehículo eliminado exitosamente.";
            return RedirectToAction("Buses");
        }

        // ==========================================
        // 4. RUTAS (CRUD)
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Rutas()
        {
            var rutas = await _context.Rutas.ToListAsync();
            return View(rutas);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearRuta(Ruta model)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Datos de la ruta inválidos.";
                return RedirectToAction("Rutas");
            }

            _context.Rutas.Add(model);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Ruta registrada exitosamente.";
            return RedirectToAction("Rutas");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarRuta(int id)
        {
            var ruta = await _context.Rutas.FindAsync(id);
            if (ruta == null) return NotFound();

            var tieneViajes = await _context.Viajes.AnyAsync(v => v.Id_Ruta == id);
            if (tieneViajes)
            {
                TempData["ErrorMessage"] = "No se puede eliminar la ruta porque está asociada a viajes.";
                return RedirectToAction("Rutas");
            }

            _context.Rutas.Remove(ruta);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Ruta eliminada exitosamente.";
            return RedirectToAction("Rutas");
        }

        // ==========================================
        // 5. VIAJES (CRUD)
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Viajes(int? idRuta, string? fecha)
        {
            DateTime selectedFecha;
            if (!DateTime.TryParse(fecha, out selectedFecha))
            {
                selectedFecha = DateTime.Today;
            }

            var query = _context.Viajes
                .Include(v => v.ObjetoBus)
                .Include(v => v.ObjetoRuta)
                .AsQueryable();

            if (idRuta.HasValue && idRuta.Value > 0)
            {
                query = query.Where(v => v.Id_Ruta == idRuta.Value);
            }

            var startOfDay = selectedFecha.Date;
            var endOfDay = startOfDay.AddDays(1);
            query = query.Where(v => v.FechaHoraSalida >= startOfDay && v.FechaHoraSalida < endOfDay);

            var viajes = await query
                .OrderBy(v => v.FechaHoraSalida)
                .ToListAsync();

            var busesList = await _context.Buses
                .Select(b => new { Id = b.Id_Bus, Texto = $"{b.NombreVehiculo} (Conductor: {b.NombreConductor ?? "Sin asignar"})" })
                .ToListAsync();
            ViewBag.Buses = new SelectList(busesList, "Id", "Texto");

            var rutasSelect = await _context.Rutas
                .Select(r => new { Id = r.Id_Ruta, Texto = $"{r.Origen} - {r.Destino} ({r.DuracionHoras} hrs)" })
                .ToListAsync();
            ViewBag.Rutas = new SelectList(rutasSelect, "Id", "Texto", idRuta);

            ViewBag.SelectedRuta = idRuta;
            ViewBag.SelectedFecha = selectedFecha.ToString("yyyy-MM-dd");

            return View(viajes);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearViaje(Viaje model)
        {
            ModelState.Remove("ObjetoBus");
            ModelState.Remove("ObjetoRuta");
            ModelState.Remove("Ventas");

            if (!ModelState.IsValid || !model.Id_Ruta.HasValue || model.Id_Ruta.Value <= 0)
            {
                TempData["ErrorMessage"] = "Debe seleccionar una ruta de viaje válida.";
                return RedirectToAction("Viajes");
            }

            if (model.Id_Bus.HasValue)
            {
                var bus = await _context.Buses.FindAsync(model.Id_Bus.Value);
                if (bus != null)
                {
                    model.PlacaVehiculo = bus.Placa;
                    if (string.IsNullOrWhiteSpace(model.NombreConductor))
                    {
                        model.NombreConductor = bus.NombreConductor;
                    }
                }
            }

            model.Estado = "Programado";
            _context.Viajes.Add(model);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Viaje programado con éxito.";
            return RedirectToAction("Viajes", new { idRuta = model.Id_Ruta, fecha = model.FechaHoraSalida.ToString("yyyy-MM-dd") });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CambiarEstadoViaje(int id, string estado)
        {
            var viaje = await _context.Viajes.FindAsync(id);
            if (viaje == null) return NotFound();

            if (estado != "Programado" && estado != "Completado" && estado != "Cancelado")
            {
                TempData["ErrorMessage"] = "Estado inválido.";
                return RedirectToAction("Viajes");
            }

            viaje.Estado = estado;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Estado del viaje cambiado a '{estado}' con éxito.";
            return RedirectToAction("Viajes");
        }
    }
}
