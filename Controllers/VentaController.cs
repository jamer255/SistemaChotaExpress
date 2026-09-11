using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SistemaChotaExpress.Data;
using SistemaChotaExpress.Models;
using SistemaChotaExpress.Services;

namespace SistemaChotaExpress.Controllers
{
    [Authorize]
    public class VentaController : Controller
    {
        private readonly AppDbContext _context;
        private readonly SunatService _sunatService;

        public VentaController(AppDbContext context, SunatService sunatService)
        {
            _context = context;
            _sunatService = sunatService;
        }

        // ==========================================
        // 0. PANTALLA DE INICIO / BIENVENIDA
        // ==========================================
        [HttpGet]
        public IActionResult Inicio()
        {
            return View();
        }

        // ==========================================
        // 1. BUSCAR VIAJES & TABLERO DE VENTAS
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> BuscarViajes(string? origen, string? destino, string? fecha, int? viajeId, int? idRuta, bool reset = false)
        {
            // Purga preventiva de rutas inválidas (Origen == Destino) de la base de datos
            var rutasInvalidas = await _context.Rutas
                .Where(r => r.Origen.ToLower() == r.Destino.ToLower())
                .ToListAsync();
            if (rutasInvalidas.Any())
            {
                _context.Rutas.RemoveRange(rutasInvalidas);
                await _context.SaveChangesAsync();
            }

            if (reset)
            {
                HttpContext.Session.Remove("LastSelectedOrigen");
                HttpContext.Session.Remove("LastSelectedDestino");
                origen = null;
                destino = null;
                idRuta = null;
            }
            else if (idRuta.HasValue && idRuta.Value > 0)
            {
                var rutaDirecta = await _context.Rutas.FindAsync(idRuta.Value);
                if (rutaDirecta != null)
                {
                    origen = rutaDirecta.Origen;
                    destino = rutaDirecta.Destino;
                }
            }
            else if (string.IsNullOrWhiteSpace(origen) && string.IsNullOrWhiteSpace(destino))
            {
                // Solo recuperar de sesión si el usuario no especificó ruta ni origen/destino
                origen = HttpContext.Session.GetString("LastSelectedOrigen");
                destino = HttpContext.Session.GetString("LastSelectedDestino");
            }

            // Validar que Origen y Destino no sean la misma ciudad
            if (!string.IsNullOrWhiteSpace(origen) && !string.IsNullOrWhiteSpace(destino) && string.Equals(origen.Trim(), destino.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = "El Origen y el Destino no pueden ser la misma ciudad. Por favor elija un destino diferente.";
                // Preservar el origen elegido y solo resetear el destino
                destino = null;
                HttpContext.Session.Remove("LastSelectedDestino");
            }

            // Si ambos valores son válidos, asegurar su existencia como Ruta registrada y guardarla en sesión
            Ruta? rutaActual = null;
            if (!string.IsNullOrWhiteSpace(origen) && !string.IsNullOrWhiteSpace(destino))
            {
                var origL = origen.Trim().ToLower();
                var destL = destino.Trim().ToLower();
                rutaActual = await _context.Rutas
                    .FirstOrDefaultAsync(r => r.Origen.ToLower() == origL && r.Destino.ToLower() == destL);
                
                if (rutaActual == null)
                {
                    rutaActual = new Ruta
                    {
                        Origen = origen.Trim(),
                        Destino = destino.Trim(),
                        DuracionHoras = 2.0
                    };
                    _context.Rutas.Add(rutaActual);
                    await _context.SaveChangesAsync();
                }

                HttpContext.Session.SetString("LastSelectedOrigen", origen.Trim());
                HttpContext.Session.SetString("LastSelectedDestino", destino.Trim());
            }
            else if (!string.IsNullOrWhiteSpace(origen))
            {
                HttpContext.Session.SetString("LastSelectedOrigen", origen.Trim());
            }

            ViewBag.IdRutaActual = rutaActual?.Id_Ruta;

            var rutasCompletas = await _context.Rutas
                .Where(r => r.Origen.ToLower() != r.Destino.ToLower())
                .OrderBy(r => r.Origen)
                .ThenBy(r => r.Destino)
                .ToListAsync();
            ViewBag.RutasCompletas = rutasCompletas;

            DateTime selectedFecha;
            if (!DateTime.TryParse(fecha, out selectedFecha))
            {
                selectedFecha = DateTime.Today;
            }

            // REGLA: Solo se puede emitir / consultar pasajes hasta 3 días hacia atrás (no más)
            var minFechaPermitida = DateTime.Today.AddDays(-3);
            if (selectedFecha.Date < minFechaPermitida)
            {
                selectedFecha = minFechaPermitida;
                TempData["ErrorMessage"] = "Solo se permite consultar o emitir ventas de pasajes hasta con un máximo de 3 días de antigüedad.";
            }

            ViewBag.SelectedFecha = selectedFecha.ToString("yyyy-MM-dd");

            // Lista oficial de los 14 lugares autorizados para Tours Chota Express
            var todosLosLugares = GeneradorViajesService.LugaresOficiales.OrderBy(l => l).ToList();

            ViewBag.LugaresDisponibles = todosLosLugares;
            ViewBag.Origines = new SelectList(todosLosLugares, origen);
            ViewBag.Destinos = new SelectList(todosLosLugares, destino);

            ViewBag.SelectedOrigen = origen ?? string.Empty;
            ViewBag.SelectedDestino = destino ?? string.Empty;

            var startOfDay = selectedFecha.Date;
            var endOfDay = startOfDay.AddDays(1);

            List<Viaje> viajes = new List<Viaje>();

            // Si hay ruta seleccionada, aseguramos y consultamos salidas ESPECÍFICAS para esa ruta
            if (rutaActual != null)
            {
                await GeneradorViajesService.AsegurarViajesParaFechaYRutaAsync(_context, selectedFecha.Date, rutaActual.Id_Ruta);

                viajes = await _context.Viajes
                    .Include(v => v.ObjetoBus)
                    .Include(v => v.ObjetoRuta)
                    .Where(v => v.Id_Ruta == rutaActual.Id_Ruta && v.Estado == "Programado" && v.FechaHoraSalida >= startOfDay && v.FechaHoraSalida < endOfDay)
                    .OrderBy(v => v.FechaHoraSalida)
                    .ToListAsync();
            }

            Viaje? selectedViaje = null;
            if (viajeId.HasValue && viajeId.Value > 0)
            {
                // Buscar el viaje seleccionado por su identificador
                var viajeRef = await _context.Viajes
                    .Include(v => v.ObjetoBus)
                    .Include(v => v.ObjetoRuta)
                    .FirstOrDefaultAsync(v => v.Id_Viaje == viajeId.Value);

                if (viajeRef != null)
                {
                    // Si el usuario cambió la ruta (destino u origen) en los filtros mientras tenía este vehículo activo,
                    // actualizamos el viaje para que se mantenga el MISMO VEHÍCULO y el MISMO PLANO con sus asientos!
                    if (rutaActual != null && viajeRef.Id_Ruta != rutaActual.Id_Ruta)
                    {
                        viajeRef.Id_Ruta = rutaActual.Id_Ruta;
                        viajeRef.ObjetoRuta = rutaActual;
                        await _context.SaveChangesAsync();
                    }

                    selectedViaje = viajeRef;

                    // Si el viaje no estaba en la lista filtrada de la ruta, agregarlo para que aparezca en los horarios
                    if (!viajes.Any(v => v.Id_Viaje == selectedViaje.Id_Viaje))
                    {
                        viajes.Add(selectedViaje);
                        viajes = viajes.OrderBy(v => v.FechaHoraSalida).ToList();
                    }
                }
                else if (viajes.Any())
                {
                    selectedViaje = viajes.First();
                }
            }

            var viajesDisponibles = new List<ViajeConDisponibilidad>();
            foreach (var v in viajes)
            {
                var asientosOcupadosCount = await _context.Ventas
                    .Where(vt => vt.Id_Viaje == v.Id_Viaje && vt.Estado != "Cancelado")
                    .CountAsync();

                var cap = v.ObjetoBus?.Capacidad ?? 15;
                viajesDisponibles.Add(new ViajeConDisponibilidad
                {
                    Viaje = v,
                    AsientosDisponibles = Math.Max(0, cap - asientosOcupadosCount)
                });
            }

            // El trabajador elige explícitamente la hora de salida
            if (selectedViaje != null)
            {
                var ventasActivas = await _context.Ventas
                    .Include(v => v.ObjetoPasajero)
                    .Where(v => v.Id_Viaje == selectedViaje.Id_Viaje && v.Estado != "Cancelado")
                    .OrderBy(v => v.NumeroAsiento)
                    .ToListAsync();

                ViewBag.AsientosOcupados = ventasActivas.Where(v => v.Estado == "Vendido").Select(v => v.NumeroAsiento).ToList();
                ViewBag.AsientosReservados = ventasActivas.Where(v => v.Estado == "Reservado").Select(v => v.NumeroAsiento).ToList();
                ViewBag.VentasActivasDetalle = ventasActivas;
                ViewBag.SelectedViaje = selectedViaje;

                var viajesEnMismaHora = viajesDisponibles
                    .Where(v => v.Viaje.FechaHoraSalida.Hour == selectedViaje.FechaHoraSalida.Hour)
                    .ToList();

                if (!viajesEnMismaHora.Any(v => v.Viaje.Id_Viaje == selectedViaje.Id_Viaje))
                {
                    var countOcup = await _context.Ventas.CountAsync(vt => vt.Id_Viaje == selectedViaje.Id_Viaje && vt.Estado != "Cancelado");
                    var cap = selectedViaje.ObjetoBus?.Capacidad ?? 15;
                    viajesEnMismaHora.Add(new ViajeConDisponibilidad {
                        Viaje = selectedViaje,
                        AsientosDisponibles = Math.Max(0, cap - countOcup)
                    });
                }

                ViewBag.ViajesEnMismaHora = viajesEnMismaHora;
            }

            // Datos para el modal de Programación y Rotación de Vehículos por el trabajador
            var busesLista = await _context.Buses
                .Select(b => new { Id = b.Id_Bus, Nombre = $"{b.NombreVehiculo} (Conductor: {b.NombreConductor ?? "Sin asignar"})" })
                .ToListAsync();
            ViewBag.BusesDisponibles = new SelectList(busesLista, "Id", "Nombre");
            var rutasLista = await _context.Rutas
                .Select(r => new { Id = r.Id_Ruta, Nombre = $"{r.Origen} - {r.Destino} ({r.DuracionHoras} hrs)" })
                .ToListAsync();
            ViewBag.RutasDisponibles = new SelectList(rutasLista, "Id", "Nombre");

            return View(viajesDisponibles);
        }

        public class ViajeConDisponibilidad
        {
            public Viaje Viaje { get; set; } = null!;
            public int AsientosDisponibles { get; set; }
        }

        // ==========================================
        // 2. PROGRAMAR SALIDA ADICIONAL (Vendedor & Gerente)
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProgramarSalidaRotativa(int idRuta, DateTime fechaHoraSalida, decimal precio)
        {
            if (idRuta <= 0)
            {
                TempData["ErrorMessage"] = "Debe especificar una ruta para programar la salida.";
                return RedirectToAction("BuscarViajes");
            }

            var nuevoViaje = new Viaje
            {
                Id_Ruta = idRuta,
                Id_Bus = null,
                FechaHoraSalida = fechaHoraSalida,
                Precio = precio,
                Estado = "Programado"
            };

            _context.Viajes.Add(nuevoViaje);
            await _context.SaveChangesAsync();

            var rutaObj = await _context.Rutas.FindAsync(idRuta);

            TempData["SuccessMessage"] = $"¡Salida programada exitosamente para las {fechaHoraSalida:hh:mm tt}!";
            return RedirectToAction("BuscarViajes", new {
                origen = rutaObj?.Origen,
                destino = rutaObj?.Destino,
                fecha = fechaHoraSalida.ToString("yyyy-MM-dd"),
                viajeId = nuevoViaje.Id_Viaje
            });
        }

        // ==========================================
        // 2.1 AGREGAR COMBI ADICIONAL EN MISMO HORARIO (+)
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AgregarSalidaAdicional(int? viajeId, string? origen, string? destino, string? fecha, int? hora)
        {
            // Determinar la fecha y hora del nuevo turno
            DateTime fechaHoraSalida;
            int? idRutaRef = null;
            decimal precioBase = 0.00m;

            if (!string.IsNullOrWhiteSpace(origen) && !string.IsNullOrWhiteSpace(destino))
            {
                var rObj = await _context.Rutas.FirstOrDefaultAsync(r => r.Origen.ToLower() == origen.Trim().ToLower() && r.Destino.ToLower() == destino.Trim().ToLower());
                if (rObj != null)
                {
                    idRutaRef = rObj.Id_Ruta;
                }
            }

            Viaje? viajeBase = null;
            if (viajeId.HasValue && viajeId.Value > 0)
            {
                viajeBase = await _context.Viajes
                    .Include(v => v.ObjetoRuta)
                    .FirstOrDefaultAsync(v => v.Id_Viaje == viajeId.Value);

                if (viajeBase != null)
                {
                    fechaHoraSalida = viajeBase.FechaHoraSalida;
                    if (!idRutaRef.HasValue) idRutaRef = viajeBase.Id_Ruta;
                }
                else
                {
                    TempData["ErrorMessage"] = "No se encontró el viaje de referencia.";
                    return RedirectToAction("BuscarViajes", new { origen, destino, fecha });
                }
            }
            else
            {
                DateTime selectedFecha;
                if (!DateTime.TryParse(fecha, out selectedFecha)) selectedFecha = DateTime.Today;
                fechaHoraSalida = selectedFecha.Date.AddHours(hora ?? 0);

                if (!idRutaRef.HasValue)
                {
                    var rutaRef = await _context.Rutas.OrderBy(r => r.Id_Ruta).FirstOrDefaultAsync();
                    if (rutaRef == null)
                    {
                        TempData["ErrorMessage"] = "No hay rutas registradas.";
                        return RedirectToAction("BuscarViajes", new { origen, destino, fecha });
                    }
                    idRutaRef = rutaRef.Id_Ruta;
                }
            }

            // Contar cuántas combis ya existen en esa hora para esta ruta
            var startOfHour = fechaHoraSalida.Date.AddHours(fechaHoraSalida.Hour);
            var endOfHour = startOfHour.AddHours(1);
            var countEnHora = await _context.Viajes
                .CountAsync(v => v.Id_Ruta == idRutaRef && v.FechaHoraSalida >= startOfHour && v.FechaHoraSalida < endOfHour);

            var nuevoViaje = new Viaje
            {
                Id_Ruta = idRutaRef,
                Id_Bus = null,           // Sin bus pre-asignado
                PlacaVehiculo = null,    // El trabajador la ingresará cuando quiera
                FechaHoraSalida = fechaHoraSalida,
                NombreConductor = null,
                Precio = precioBase,
                Estado = "Programado"
            };

            _context.Viajes.Add(nuevoViaje);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"¡Nueva Combi {countEnHora + 1:D2} agregada a las {fechaHoraSalida:hh:mm tt}!";

            return RedirectToAction("BuscarViajes", new {
                origen,
                destino,
                fecha = fechaHoraSalida.ToString("yyyy-MM-dd"),
                viajeId = nuevoViaje.Id_Viaje
            });
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ActualizarPrecioViaje(int viajeId, decimal nuevoPrecio, string? origen, string? destino, string? fecha)
        {
            var viaje = await _context.Viajes.FindAsync(viajeId);
            if (viaje != null)
            {
                viaje.Precio = Math.Max(0, nuevoPrecio);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"¡Precio del pasaje actualizado a S/ {viaje.Precio:F2}!";
            }
            return RedirectToAction("BuscarViajes", new { origen, destino, fecha, viajeId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ActualizarConductorViaje(int viajeId, string nombreConductor, string? origen, string? destino, string fecha)
        {
            var viaje = await _context.Viajes.FindAsync(viajeId);
            if (viaje != null && !string.IsNullOrWhiteSpace(nombreConductor))
            {
                viaje.NombreConductor = nombreConductor.Trim();
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"¡Nombre del conductor actualizado a '{viaje.NombreConductor}'!";
            }

            return RedirectToAction("BuscarViajes", new { origen, destino, fecha, viajeId });
        }

        // ==========================================
        // ACTUALIZAR PLACA DEL VEHÍCULO (Opcional, por el trabajador)
        // Formato esperado: 123-ABC
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ActualizarPlacaViaje(int viajeId, string? placaVehiculo, string? origen, string? destino, string fecha)
        {
            var viaje = await _context.Viajes.FindAsync(viajeId);
            if (viaje != null)
            {
                // Normalizar a mayúsculas y quitar espacios
                var placa = placaVehiculo?.Trim().ToUpper();
                viaje.PlacaVehiculo = string.IsNullOrWhiteSpace(placa) ? null : placa;
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = string.IsNullOrWhiteSpace(viaje.PlacaVehiculo)
                    ? "Placa del vehículo eliminada."
                    : $"¡Placa registrada: {viaje.PlacaVehiculo}!";
            }

            return RedirectToAction("BuscarViajes", new { origen, destino, fecha, viajeId });
        }

        // ==========================================
        // 2.2 ELIMINAR SALIDA / VEHÍCULO DE UN HORARIO (-)
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarSalida(int viajeId, string? origen, string? destino, string fecha)
        {
            DateTime selectedFecha;
            if (!DateTime.TryParse(fecha, out selectedFecha))
            {
                selectedFecha = DateTime.Today;
            }

            var viaje = await _context.Viajes
                .Include(v => v.Ventas)
                .FirstOrDefaultAsync(v => v.Id_Viaje == viajeId);

            if (viaje == null)
            {
                TempData["ErrorMessage"] = "No se encontró la salida que se desea eliminar.";
                return RedirectToAction("BuscarViajes", new { origen, destino, fecha = selectedFecha.ToString("yyyy-MM-dd") });
            }

            var tienePasajesActivos = viaje.Ventas.Any(v => v.Estado == "Vendido" || v.Estado == "Reservado");
            if (tienePasajesActivos)
            {
                TempData["ErrorMessage"] = $"No se puede eliminar la salida de las {viaje.FechaHoraSalida:hh:mm tt} porque ya cuenta con pasajes vendidos o reservados.";
                return RedirectToAction("BuscarViajes", new { origen, destino, fecha = selectedFecha.ToString("yyyy-MM-dd"), viajeId });
            }

            var horaTexto = viaje.FechaHoraSalida.ToString("hh:mm tt");
            _context.Viajes.Remove(viaje);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Salida de las {horaTexto} eliminada correctamente.";

            return RedirectToAction("BuscarViajes", new { origen, destino, fecha = selectedFecha.ToString("yyyy-MM-dd") });
        }

        // ==========================================
        // 3. REGISTRAR VENTA (PASAJERO + FACTURA RUC)
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegistrarVenta(
            int viajeId,
            string tipoDocumento,
            string numeroDocumento,
            string nombreCompleto,
            string? telefono,
            string? correo,
            int numeroAsiento,
            string tipoTransaccion,
            decimal precioPagado,
            string? tipoComprobante,
            string? rucEmpresa,
            string? razonSocialEmpresa,
            string? direccionEmpresa,
            string? metodoPago,
            string? observaciones)
        {
            if (string.IsNullOrWhiteSpace(numeroDocumento) || string.IsNullOrWhiteSpace(nombreCompleto) || numeroAsiento <= 0)
            {
                TempData["ErrorMessage"] = "Todos los datos del pasajero y asiento son requeridos.";
                return RedirectToAction("BuscarViajes", new { viajeId });
            }

            var asientoOcupado = await _context.Ventas
                .AnyAsync(v => v.Id_Viaje == viajeId && v.NumeroAsiento == numeroAsiento && v.Estado != "Cancelado");

            if (asientoOcupado)
            {
                TempData["ErrorMessage"] = $"El asiento número {numeroAsiento} ya se encuentra ocupado o reservado.";
                return RedirectToAction("BuscarViajes", new { viajeId });
            }

            var viaje = await _context.Viajes.FindAsync(viajeId);
            if (viaje == null) return NotFound();

            // Buscar o registrar al pasajero
            var pasajero = await _context.Pasajeros
                .FirstOrDefaultAsync(p => p.TipoDocumento == tipoDocumento && p.NumeroDocumento == numeroDocumento);

            if (pasajero == null)
            {
                pasajero = new Pasajero
                {
                    TipoDocumento = tipoDocumento,
                    NumeroDocumento = numeroDocumento.Trim(),
                    NombreCompleto = nombreCompleto.Trim().ToUpper(),
                    Telefono = telefono,
                    Correo = correo
                };
                _context.Pasajeros.Add(pasajero);
                await _context.SaveChangesAsync();
            }
            else
            {
                pasajero.NombreCompleto = nombreCompleto.Trim().ToUpper();
                if (!string.IsNullOrEmpty(telefono)) pasajero.Telefono = telefono;
                if (!string.IsNullOrEmpty(correo)) pasajero.Correo = correo;
                _context.Pasajeros.Update(pasajero);
            }

            // Obtener el ID del usuario autenticado
            var emailUsuario = User.FindFirstValue(ClaimTypes.Email);
            var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Email == emailUsuario);
            if (usuario == null) return Challenge();

            var comprobanteFinal = string.IsNullOrWhiteSpace(tipoComprobante) ? "Boleta" : tipoComprobante;

            var venta = new Venta
            {
                Id_Viaje = viajeId,
                Id_Pasajero = pasajero.Id_Pasajero,
                Id_Usuario = usuario.Id_Usuario,
                NumeroAsiento = numeroAsiento,
                PrecioPagado = precioPagado,
                FechaVenta = DateTime.Now,
                Estado = (tipoTransaccion == "Reserva") ? "Reservado" : "Vendido",
                TipoComprobante = comprobanteFinal,
                RucEmpresa = (comprobanteFinal == "Factura") ? rucEmpresa?.Trim() : null,
                RazonSocialEmpresa = (comprobanteFinal == "Factura") ? razonSocialEmpresa?.Trim().ToUpper() : null,
                DireccionEmpresa = (comprobanteFinal == "Factura") ? direccionEmpresa?.Trim().ToUpper() : null,
                MetodoPago = string.IsNullOrWhiteSpace(metodoPago) ? "Efectivo" : metodoPago,
                Observaciones = observaciones
            };

            _context.Ventas.Add(venta);
            await _context.SaveChangesAsync();

            // Generar y guardar XML SUNAT automáticamente si es venta (no reserva)
            if (venta.Estado == "Vendido")
            {
                // Recargar relaciones para el XML
                var ventaConRelaciones = await _context.Ventas
                    .Include(v => v.ObjetoPasajero)
                    .Include(v => v.ObjetoUsuario)
                    .Include(v => v.ObjetoViaje).ThenInclude(v => v!.ObjetoBus)
                    .Include(v => v.ObjetoViaje).ThenInclude(v => v!.ObjetoRuta)
                    .FirstOrDefaultAsync(v => v.Id_Venta == venta.Id_Venta);
                if (ventaConRelaciones != null)
                    _ = _sunatService.ProcesarComprobanteAsync(ventaConRelaciones);
            }

            if (venta.Estado == "Vendido")
            {
                TempData["SuccessMessage"] = $"¡Pasaje vendido con éxito! Comprobante emitido: {venta.TipoComprobante}.";
                return RedirectToAction("Ticket", new { id = venta.Id_Venta });
            }
            else
            {
                TempData["SuccessMessage"] = "Reserva registrada con éxito.";
                return RedirectToAction("MisVentas");
            }
        }

        // ==========================================
        // 3.1 REGISTRAR VENTA MÚLTIPLE DE ASIENTOS (EMPRESAS / RUC ÚNICO)
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegistrarVentaMultiple(
            int viajeId,
            string tipoTransaccion,
            string? tipoComprobante,
            string? rucEmpresa,
            string? razonSocialEmpresa,
            string? direccionEmpresa,
            string? observaciones,
            List<int> asientos,
            List<string> tiposDocumento,
            List<string> numerosDocumento,
            List<string> nombresCompletos,
            List<decimal> preciosPagados,
            string? origen,
            string? destino,
            string? fecha,
            List<string>? destinosPasajeros = null)
        {
            if (asientos == null || !asientos.Any())
            {
                TempData["ErrorMessage"] = "Debe seleccionar al menos un asiento libre.";
                return RedirectToAction("BuscarViajes", new { origen, destino, fecha, viajeId });
            }

            var viaje = await _context.Viajes.FindAsync(viajeId);
            if (viaje == null && viajeId > 0)
            {
                viaje = await _context.Viajes.FirstOrDefaultAsync();
            }

            if (viaje == null)
            {
                TempData["ErrorMessage"] = "No se encontró la información del viaje.";
                return RedirectToAction("BuscarViajes", new { origen, destino, fecha });
            }

            // Asegurar que el viaje pertenezca a la ruta seleccionada
            if (!string.IsNullOrWhiteSpace(origen) && !string.IsNullOrWhiteSpace(destino))
            {
                var rObj = await _context.Rutas.FirstOrDefaultAsync(r => r.Origen.ToLower() == origen.Trim().ToLower() && r.Destino.ToLower() == destino.Trim().ToLower());
                if (rObj != null && viaje.Id_Ruta != rObj.Id_Ruta)
                {
                    viaje.Id_Ruta = rObj.Id_Ruta;
                    await _context.SaveChangesAsync();
                }
            }

            var emailUsuario = User.FindFirstValue(ClaimTypes.Email);
            var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Email == emailUsuario) 
                          ?? await _context.Usuarios.FirstOrDefaultAsync();

            if (usuario == null)
            {
                return Challenge();
            }

            string comprobanteFinal = string.IsNullOrWhiteSpace(tipoComprobante) ? "Boleta" : tipoComprobante;
            int emitidosCount = 0;
            int ultimaVentaId = 0;

            for (int i = 0; i < asientos.Count; i++)
            {
                int numAsiento = asientos[i];
                string tDoc = (tiposDocumento != null && i < tiposDocumento.Count) ? tiposDocumento[i] : "DNI";
                string nDoc = (numerosDocumento != null && i < numerosDocumento.Count) ? numerosDocumento[i].Trim() : "";
                string nomComp = (nombresCompletos != null && i < nombresCompletos.Count) ? nombresCompletos[i].Trim().ToUpper() : "PASAJERO";
                decimal precio = (preciosPagados != null && i < preciosPagados.Count) ? preciosPagados[i] : viaje.Precio;

                if (string.IsNullOrWhiteSpace(nDoc)) nDoc = $"S/D-{numAsiento}";
                if (string.IsNullOrWhiteSpace(nomComp)) nomComp = $"PASAJERO ASIENTO {numAsiento}";

                // Verificar si el asiento ya está ocupado
                var yaOcupado = await _context.Ventas.AnyAsync(v => v.Id_Viaje == viaje.Id_Viaje && v.NumeroAsiento == numAsiento && v.Estado != "Cancelado");
                if (yaOcupado) continue;

                // Buscar o crear Pasajero
                var pasajero = await _context.Pasajeros.FirstOrDefaultAsync(p => p.TipoDocumento == tDoc && p.NumeroDocumento == nDoc);
                if (pasajero == null)
                {
                    pasajero = new Pasajero
                    {
                        TipoDocumento = tDoc,
                        NumeroDocumento = nDoc,
                        NombreCompleto = nomComp
                    };
                    _context.Pasajeros.Add(pasajero);
                    await _context.SaveChangesAsync();
                }
                else
                {
                    pasajero.NombreCompleto = nomComp;
                    _context.Pasajeros.Update(pasajero);
                }

                string? destPasajero = (destinosPasajeros != null && i < destinosPasajeros.Count && !string.IsNullOrWhiteSpace(destinosPasajeros[i])) ? destinosPasajeros[i].Trim() : destino;
                string obsFinal = observaciones ?? "";
                if (!string.IsNullOrWhiteSpace(destPasajero))
                {
                    obsFinal = $"Bajada: {destPasajero}{(string.IsNullOrWhiteSpace(obsFinal) ? "" : $" | {obsFinal}")}";
                }
                if (obsFinal.Length > 250) obsFinal = obsFinal.Substring(0, 250);

                var venta = new Venta
                {
                    Id_Viaje = viaje.Id_Viaje,
                    Id_Pasajero = pasajero.Id_Pasajero,
                    Id_Usuario = usuario.Id_Usuario,
                    NumeroAsiento = numAsiento,
                    PrecioPagado = precio,
                    FechaVenta = DateTime.Now,
                    Estado = (tipoTransaccion == "Reserva") ? "Reservado" : "Vendido",
                    TipoComprobante = comprobanteFinal,
                    RucEmpresa = (comprobanteFinal == "Factura") ? rucEmpresa?.Trim() : null,
                    RazonSocialEmpresa = (comprobanteFinal == "Factura") ? razonSocialEmpresa?.Trim().ToUpper() : null,
                    DireccionEmpresa = (comprobanteFinal == "Factura") ? direccionEmpresa?.Trim() : null,
                    MetodoPago = "Efectivo",
                    Observaciones = obsFinal
                };

                _context.Ventas.Add(venta);
                await _context.SaveChangesAsync();

                // Generar y guardar XML SUNAT automáticamente (solo ventas, no reservas)
                if (venta.Estado == "Vendido")
                {
                    var ventaRel = await _context.Ventas
                        .Include(v => v.ObjetoPasajero)
                        .Include(v => v.ObjetoUsuario)
                        .Include(v => v.ObjetoViaje).ThenInclude(v => v!.ObjetoBus)
                        .Include(v => v.ObjetoViaje).ThenInclude(v => v!.ObjetoRuta)
                        .FirstOrDefaultAsync(v => v.Id_Venta == venta.Id_Venta);
                    if (ventaRel != null)
                        _ = _sunatService.ProcesarComprobanteAsync(ventaRel);
                }

                ultimaVentaId = venta.Id_Venta;
                emitidosCount++;
            }

            if (tipoTransaccion == "Reserva")
            {
                TempData["SuccessMessage"] = $"¡Reserva registrada con éxito para {emitidosCount} asiento(s)!";
                return RedirectToAction("MisVentas");
            }

            if (emitidosCount == 1 && ultimaVentaId > 0)
            {
                TempData["SuccessMessage"] = $"¡Pasaje vendido con éxito! Comprobante emitido: {comprobanteFinal}.";
                return RedirectToAction("Ticket", new { id = ultimaVentaId });
            }

            TempData["SuccessMessage"] = $"¡Se emitieron {emitidosCount} pasaje(s) exitosamente" + (!string.IsNullOrEmpty(razonSocialEmpresa) ? $" para la empresa '{razonSocialEmpresa}'!" : "!");

            return RedirectToAction("BuscarViajes", new { origen = origen ?? viaje.ObjetoRuta?.Origen, destino = destino ?? viaje.ObjetoRuta?.Destino, fecha, viajeId = viaje.Id_Viaje });
        }

        // ==========================================
        // 3.2 ANULAR VENTA / LIBERAR ASIENTO (DESANIMADOS O CANCELACIONES)
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AnularVenta(int idVenta, string? origen, string? destino, string? fecha, int? viajeId)
        {
            var venta = await _context.Ventas.FindAsync(idVenta);
            if (venta != null)
            {
                venta.Estado = "Cancelado";
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"¡Venta/Reserva del asiento #{venta.NumeroAsiento} fue ANULADA con éxito. El asiento ha sido liberado!";
            }

            int targetViajeId = viajeId ?? venta?.Id_Viaje ?? 0;
            return RedirectToAction("BuscarViajes", new { origen, destino, fecha, viajeId = targetViajeId > 0 ? targetViajeId : (int?)null });
        }

        // ==========================================
        // 4. TICKET (Vista de Impresión / Boleto)
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Ticket(int id)
        {
            var venta = await _context.Ventas
                .Include(v => v.ObjetoPasajero)
                .Include(v => v.ObjetoUsuario)
                .Include(v => v.ObjetoViaje)
                    .ThenInclude(v => v!.ObjetoBus)
                .Include(v => v.ObjetoViaje)
                    .ThenInclude(v => v!.ObjetoRuta)
                .FirstOrDefaultAsync(v => v.Id_Venta == id);

            if (venta == null) return NotFound();

            return View(venta);
        }

        // ==========================================
        // 4.1 MANIFIESTO Y CROQUIS DE ASIENTOS PARA EL CHOFER
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> ManifiestoChofer(int viajeId)
        {
            var viaje = await _context.Viajes
                .Include(v => v.ObjetoBus)
                .Include(v => v.ObjetoRuta)
                .FirstOrDefaultAsync(v => v.Id_Viaje == viajeId);

            if (viaje == null) return NotFound();

            var ventasActivas = await _context.Ventas
                .Include(v => v.ObjetoPasajero)
                .Include(v => v.ObjetoUsuario)
                .Where(v => v.Id_Viaje == viajeId && v.Estado != "Cancelado")
                .OrderBy(v => v.NumeroAsiento)
                .ToListAsync();

            ViewBag.VentasActivas = ventasActivas;
            return View(viaje);
        }

        // ==========================================
        // 5. REGISTRO GENERAL DE VENTAS Y RESERVAS
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> MisVentas()
        {
            var query = _context.Ventas
                .Include(v => v.ObjetoPasajero)
                .Include(v => v.ObjetoUsuario)
                .Include(v => v.ObjetoViaje)
                    .ThenInclude(v => v!.ObjetoBus)
                .Include(v => v.ObjetoViaje)
                    .ThenInclude(v => v!.ObjetoRuta)
                .AsQueryable();

            var ventas = await query.OrderByDescending(v => v.FechaVenta).ToListAsync();
            return View(ventas);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmarReserva(int id)
        {
            var venta = await _context.Ventas.FindAsync(id);
            if (venta == null) return NotFound();

            if (venta.Estado != "Reservado")
            {
                TempData["ErrorMessage"] = "Esta transacción ya no está en estado de Reserva.";
                return RedirectToAction("MisVentas");
            }

            venta.Estado = "Vendido";
            venta.FechaVenta = DateTime.Now;
            await _context.SaveChangesAsync();

            // Generar y subir comprobante electrónico SUNAT
            var ventaConRel = await _context.Ventas
                .Include(v => v.ObjetoPasajero)
                .Include(v => v.ObjetoUsuario)
                .Include(v => v.ObjetoViaje).ThenInclude(v => v!.ObjetoBus)
                .Include(v => v.ObjetoViaje).ThenInclude(v => v!.ObjetoRuta)
                .FirstOrDefaultAsync(v => v.Id_Venta == venta.Id_Venta);
            if (ventaConRel != null)
                _ = _sunatService.ProcesarComprobanteAsync(ventaConRel);

            TempData["SuccessMessage"] = "Reserva confirmada y convertida a Venta.";
            return RedirectToAction("Ticket", new { id = venta.Id_Venta });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelarTransaccion(int id)
        {
            var venta = await _context.Ventas.FindAsync(id);
            if (venta == null) return NotFound();

            venta.Estado = "Cancelado";
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Venta/Reserva cancelada con éxito. El asiento ha sido liberado.";
            return RedirectToAction("MisVentas");
        }

        // ==========================================
        // 6. DESCARGA XML (SUNAT UBL 2.1 FORMAT)
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> DescargarXml(int id)
        {
            var venta = await _context.Ventas
                .Include(v => v.ObjetoPasajero)
                .Include(v => v.ObjetoUsuario)
                .Include(v => v.ObjetoViaje)
                    .ThenInclude(v => v!.ObjetoBus)
                .Include(v => v.ObjetoViaje)
                    .ThenInclude(v => v!.ObjetoRuta)
                .FirstOrDefaultAsync(v => v.Id_Venta == id);

            if (venta == null) return NotFound();

            bool esFactura = (venta.TipoComprobante == "Factura");
            string serie = esFactura ? "F001" : "B001";
            string tipoDocSunat = esFactura ? "01" : "03"; // 01: Factura, 03: Boleta
            string numeroCorrelativo = venta.Id_Venta.ToString("D8");
            string idComprobante = $"{serie}-{numeroCorrelativo}";
            string rucEmisor = "20600308883";
            string razonSocialEmisor = "TOURS CHOTA EXPRESS S.A.C.";

            decimal subtotal = Math.Round(venta.PrecioPagado / 1.18m, 2);
            decimal igv = venta.PrecioPagado - subtotal;

            string docReceptor = esFactura ? (venta.RucEmpresa ?? "20000000001") : venta.ObjetoPasajero!.NumeroDocumento;
            string tipoDocReceptor = esFactura ? "6" : (venta.ObjetoPasajero!.TipoDocumento == "DNI" ? "1" : "4");
            string razonReceptor = esFactura ? (venta.RazonSocialEmpresa ?? "EMPRESA CLIENTE") : venta.ObjetoPasajero!.NombreCompleto;

            XNamespace cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
            XNamespace cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
            XNamespace ext = "urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2";

            var xmlDoc = new XDocument(
                new XDeclaration("1.0", "UTF-8", "yes"),
                new XElement("Invoice",
                    new XAttribute(XNamespace.Xmlns + "cac", cac.NamespaceName),
                    new XAttribute(XNamespace.Xmlns + "cbc", cbc.NamespaceName),
                    new XAttribute(XNamespace.Xmlns + "ext", ext.NamespaceName),
                    new XElement(ext + "UBLExtensions",
                        new XElement(ext + "UBLExtension",
                            new XElement(ext + "ExtensionContent",
                                new XElement("Signature", "SIGNATURE_HASH_TOURS_CHOTA_EXPRESS")
                            )
                        )
                    ),
                    new XElement(cbc + "UBLVersionID", "2.1"),
                    new XElement(cbc + "CustomizationID", "2.0"),
                    new XElement(cbc + "ID", idComprobante),
                    new XElement(cbc + "IssueDate", venta.FechaVenta.ToString("yyyy-MM-dd")),
                    new XElement(cbc + "IssueTime", venta.FechaVenta.ToString("HH:mm:ss")),
                    new XElement(cbc + "InvoiceTypeCode", new XAttribute("listID", "0101"), tipoDocSunat),
                    new XElement(cbc + "DocumentCurrencyCode", "PEN"),

                    // Emisor (Tours Chota Express)
                    new XElement(cac + "AccountingSupplierParty",
                        new XElement(cac + "Party",
                            new XElement(cac + "PartyIdentification",
                                new XElement(cbc + "ID", new XAttribute("schemeID", "6"), rucEmisor)
                            ),
                            new XElement(cac + "PartyLegalEntity",
                                new XElement(cbc + "RegistrationName", razonSocialEmisor)
                            )
                        )
                    ),

                    // Receptor (Cliente / Pasajero / Empresa)
                    new XElement(cac + "AccountingCustomerParty",
                        new XElement(cac + "Party",
                            new XElement(cac + "PartyIdentification",
                                new XElement(cbc + "ID", new XAttribute("schemeID", tipoDocReceptor), docReceptor)
                            ),
                            new XElement(cac + "PartyLegalEntity",
                                new XElement(cbc + "RegistrationName", razonReceptor)
                            )
                        )
                    ),

                    // Detalle de Impuestos
                    new XElement(cac + "TaxTotal",
                        new XElement(cbc + "TaxAmount", new XAttribute("currencyID", "PEN"), igv.ToString("0.00")),
                        new XElement(cac + "TaxSubtotal",
                            new XElement(cbc + "TaxableAmount", new XAttribute("currencyID", "PEN"), subtotal.ToString("0.00")),
                            new XElement(cbc + "TaxAmount", new XAttribute("currencyID", "PEN"), igv.ToString("0.00")),
                            new XElement(cac + "TaxCategory",
                                new XElement(cac + "TaxScheme",
                                    new XElement(cbc + "ID", "1000"),
                                    new XElement(cbc + "Name", "IGV"),
                                    new XElement(cbc + "TaxTypeCode", "VAT")
                                )
                            )
                        )
                    ),

                    // Totales Monetarios
                    new XElement(cac + "LegalMonetaryTotal",
                        new XElement(cbc + "LineExtensionAmount", new XAttribute("currencyID", "PEN"), subtotal.ToString("0.00")),
                        new XElement(cbc + "TaxInclusiveAmount", new XAttribute("currencyID", "PEN"), venta.PrecioPagado.ToString("0.00")),
                        new XElement(cbc + "PayableAmount", new XAttribute("currencyID", "PEN"), venta.PrecioPagado.ToString("0.00"))
                    ),

                    // Línea de detalle del Pasaje
                    new XElement(cac + "InvoiceLine",
                        new XElement(cbc + "ID", "1"),
                        new XElement(cbc + "InvoicedQuantity", new XAttribute("unitCode", "ZZ"), "1"),
                        new XElement(cbc + "LineExtensionAmount", new XAttribute("currencyID", "PEN"), subtotal.ToString("0.00")),
                        new XElement(cac + "Item",
                            new XElement(cbc + "Description", $"SERVICIO DE TRANSPORTE DE PASAJEROS: {(venta.ObjetoViaje?.ObjetoRuta?.Origen ?? "ORIGEN")} A {(venta.ObjetoViaje?.ObjetoRuta?.Destino ?? "DESTINO")} - ASIENTO {venta.NumeroAsiento} - BUS {(venta.ObjetoViaje?.ObjetoBus?.Placa ?? venta.ObjetoViaje?.PlacaVehiculo ?? "S/P")}")
                        ),
                        new XElement(cac + "Price",
                            new XElement(cbc + "PriceAmount", new XAttribute("currencyID", "PEN"), subtotal.ToString("0.00"))
                        )
                    )
                )
            );

            var stream = new MemoryStream();
            xmlDoc.Save(stream);
            stream.Position = 0;

            string fileName = $"{rucEmisor}-{tipoDocSunat}-{idComprobante}.xml";
            return File(stream, "application/xml", fileName);
        }

        // ==========================================
        // 7. EXPORTAR EXCEL / CSV
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> ExportarExcel()
        {
            var query = _context.Ventas
                .Include(v => v.ObjetoPasajero)
                .Include(v => v.ObjetoUsuario)
                .Include(v => v.ObjetoViaje)
                    .ThenInclude(v => v!.ObjetoBus)
                .Include(v => v.ObjetoViaje)
                    .ThenInclude(v => v!.ObjetoRuta)
                .OrderByDescending(v => v.FechaVenta);

            var ventas = await query.ToListAsync();

            var sb = new StringBuilder();
            // Encabezados CSV compatibles con Excel (BOM UTF-8)
            sb.AppendLine("ID_Venta,Fecha_Operacion,Hora,Tipo_Comprobante,Doc_Pasajero,Nombre_Pasajero,RUC_Empresa,Razon_Social,Ruta_Origen,Ruta_Destino,Fecha_Salida,Hora_Salida,Bus_Placa,Asiento,Monto_Pagado,Metodo_Pago,Estado,Vendedor");

            foreach (var v in ventas)
            {
                var fOperacion = v.FechaVenta.ToString("yyyy-MM-dd");
                var hOperacion = v.FechaVenta.ToString("HH:mm:ss");
                var fSalida = v.ObjetoViaje?.FechaHoraSalida.ToString("yyyy-MM-dd") ?? "";
                var hSalida = v.ObjetoViaje?.FechaHoraSalida.ToString("HH:mm") ?? "";
                var pasajeroDoc = $"{v.ObjetoPasajero?.TipoDocumento}: {v.ObjetoPasajero?.NumeroDocumento}";
                var pasajeroNom = $"\"{v.ObjetoPasajero?.NombreCompleto?.Replace("\"", "\"\"")}\"";
                var ruc = v.RucEmpresa ?? "";
                var razon = $"\"{v.RazonSocialEmpresa?.Replace("\"", "\"\"") ?? ""}\"";
                var origen = v.ObjetoViaje?.ObjetoRuta?.Origen ?? "";
                var destino = v.ObjetoViaje?.ObjetoRuta?.Destino ?? "";
                var placa = v.ObjetoViaje?.ObjetoBus?.Placa ?? "";
                var vendedor = $"\"{v.ObjetoUsuario?.Name?.Replace("\"", "\"\"") ?? ""}\"";

                sb.AppendLine($"{v.Id_Venta},{fOperacion},{hOperacion},{v.TipoComprobante},{pasajeroDoc},{pasajeroNom},{ruc},{razon},{origen},{destino},{fSalida},{hSalida},{placa},{v.NumeroAsiento},{v.PrecioPagado.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)},{v.MetodoPago},{v.Estado},{vendedor}");
            }

            var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
            var fileName = $"Ventas_ChotaExpress_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

            return File(bytes, "text/csv", fileName);
        }
    }
}
