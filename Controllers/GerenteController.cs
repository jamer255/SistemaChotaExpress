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
            var hoy = DateTime.Today;
            var manana = hoy.AddDays(1);
            var inicioMes = new DateTime(hoy.Year, hoy.Month, 1);
            var inicioAnio = new DateTime(hoy.Year, 1, 1);

            // KPIs
             var ventasHoy = await _context.Ventas
                .Where(v => v.Estado == "Vendido" && v.FechaVenta >= hoy && v.FechaVenta < manana)
                .SumAsync(v => (decimal?)v.PrecioPagado) ?? 0;

            var cantidadHoy = await _context.Ventas
                .Where(v => v.Estado == "Vendido" && v.FechaVenta >= hoy && v.FechaVenta < manana)
                .CountAsync();

            var ventasMes = await _context.Ventas
                .Where(v => v.Estado == "Vendido" && v.FechaVenta >= inicioMes)
                .SumAsync(v => (decimal?)v.PrecioPagado) ?? 0;

            var ventasAnio = await _context.Ventas
                .Where(v => v.Estado == "Vendido" && v.FechaVenta >= inicioAnio)
                .SumAsync(v => (decimal?)v.PrecioPagado) ?? 0;

            ViewBag.VentasHoy = ventasHoy;
            ViewBag.CantidadHoy = cantidadHoy;
            ViewBag.VentasMes = ventasMes;
            ViewBag.VentasAnio = ventasAnio;

            ViewBag.BusesCount = await _context.Buses.CountAsync();
            ViewBag.RutasCount = await _context.Rutas.CountAsync();
            ViewBag.ViajesCount = await _context.Viajes.Where(v => v.Estado == "Programado").CountAsync();
            ViewBag.TrabajadoresCount = await _context.Usuarios.Where(u => u.Id_Rol == 2).CountAsync();

            // Ventas diarias para gráfico (últimos 15 días)
            // Se traen a memoria primero para poder agrupar por .Date en C# (PostgreSQL no lo soporta directo)
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

            // Ventas mensuales para gráfico (año actual) - también en memoria
            var ventasMesesRaw = await _context.Ventas
                .Where(v => v.Estado == "Vendido" && v.FechaVenta >= inicioAnio)
                .Select(v => new { v.FechaVenta, v.PrecioPagado })
                .ToListAsync();

            var ventasMensualesList = ventasMesesRaw
                .GroupBy(v => v.FechaVenta.Month)
                .Select(g => new { Mes = g.Key, Total = g.Sum(v => v.PrecioPagado) })
                .ToList();

            string[] nombreMeses = { "Ene", "Feb", "Mar", "Abr", "May", "Jun", "Jul", "Ago", "Sep", "Oct", "Nov", "Dic" };
            decimal[] valoresMeses = new decimal[12];
            for (int i = 0; i < 12; i++)
            {
                var mesNum = i + 1;
                valoresMeses[i] = ventasMensualesList.FirstOrDefault(vm => vm.Mes == mesNum)?.Total ?? 0;
            }

            ViewBag.MesesLabels = nombreMeses;
            ViewBag.MesesValores = valoresMeses;

            // Top rutas más vendidas - materializar en memoria para evitar errores PostgreSQL con navegación
            var ventasConRutas = await _context.Ventas
                .Include(v => v.ObjetoViaje)
                    .ThenInclude(v => v!.ObjetoRuta)
                .Where(v => v.Estado == "Vendido" && v.ObjetoViaje != null && v.ObjetoViaje.ObjetoRuta != null)
                .Select(v => new {
                    Origen = v.ObjetoViaje!.ObjetoRuta!.Origen,
                    Destino = v.ObjetoViaje.ObjetoRuta.Destino,
                    v.PrecioPagado
                })
                .ToListAsync();

            var topRutas = ventasConRutas
                .GroupBy(v => new { v.Origen, v.Destino })
                .Select(g => new RutaMasVendidaVM {
                    Ruta = $"{g.Key.Origen} - {g.Key.Destino}",
                    Cantidad = g.Count(),
                    Monto = g.Sum(v => v.PrecioPagado)
                })
                .OrderByDescending(g => g.Monto)
                .Take(5)
                .ToList();

            ViewBag.ViajesMasVendidos = topRutas;

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
