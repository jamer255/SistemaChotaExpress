using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SistemaChotaExpress.Data;
using SistemaChotaExpress.Models;

namespace SistemaChotaExpress.Controllers
{
    [Authorize]
    public class EncomiendaController : Controller
    {
        private readonly AppDbContext _context;

        public EncomiendaController(AppDbContext context)
        {
            _context = context;
        }

        // =============================================
        // 1. REGISTRO DE ENCOMIENDAS (LISTADO)
        // =============================================
        [HttpGet]
        public async Task<IActionResult> Index(string? buscar, string? estado)
        {
            try
            {
                var query = _context.Encomiendas
                    .Include(e => e.ObjetoRuta)
                    .Include(e => e.UsuarioRegistro)
                    .AsQueryable();

                // Trabajadores solo ven las suyas; Gerente ve todas
                if (!User.IsInRole("Gerente"))
                {
                    var emailActual = User.FindFirstValue(ClaimTypes.Email);
                    var nombreActual = User.Identity?.Name;
                    var usuarioActual = await _context.Usuarios
                        .FirstOrDefaultAsync(u => u.Email == emailActual || u.Name == nombreActual);
                    if (usuarioActual != null)
                        query = query.Where(e => e.Id_Usuario == usuarioActual.Id_Usuario);
                }

                if (!string.IsNullOrWhiteSpace(buscar))
                {
                    buscar = buscar.Trim().ToLower();
                    query = query.Where(e =>
                        e.CodigoSeguimiento.ToLower().Contains(buscar) ||
                        e.NombreRemitente.ToLower().Contains(buscar) ||
                        e.NombreDestinatario.ToLower().Contains(buscar) ||
                        (e.DniDestinatario != null && e.DniDestinatario.Contains(buscar)) ||
                        (e.DniRemitente != null && e.DniRemitente.Contains(buscar)));
                }

                if (!string.IsNullOrWhiteSpace(estado) && estado != "Todos")
                    query = query.Where(e => e.Estado == estado);

                var lista = await query.OrderByDescending(e => e.FechaRegistro).ToListAsync();

                ViewBag.Buscar = buscar;
                ViewBag.EstadoFiltro = estado ?? "Todos";
                return View(lista);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "No se pudieron cargar las encomiendas: " + ex.Message;
                ViewBag.Buscar = buscar;
                ViewBag.EstadoFiltro = estado ?? "Todos";
                return View(new List<Encomienda>());
            }
        }

        // =============================================
        // 2. REGISTRAR ENCOMIENDA (FORMULARIO)
        // =============================================
        [HttpGet]
        public async Task<IActionResult> Registrar()
        {
            try
            {
                var rutas = await _context.Rutas
                    .Where(r => r.Origen.ToLower() != r.Destino.ToLower())
                    .OrderBy(r => r.Origen)
                    .ToListAsync();
                ViewBag.Rutas = new SelectList(rutas, "Id_Ruta", "NombreRuta");
            }
            catch
            {
                ViewBag.Rutas = new SelectList(new List<Ruta>(), "Id_Ruta", "NombreRuta");
            }

            return View(new Encomienda { Estado = "Registrado" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Registrar(Encomienda modelo)
        {
            try
            {
                ModelState.Remove("CodigoSeguimiento");
                ModelState.Remove("Estado");
                ModelState.Remove("ObjetoRuta");
                ModelState.Remove("UsuarioRegistro");

                if (!ModelState.IsValid)
                {
                    var rutas = await _context.Rutas
                        .Where(r => r.Origen.ToLower() != r.Destino.ToLower())
                        .OrderBy(r => r.Origen).ToListAsync();
                    ViewBag.Rutas = new SelectList(rutas, "Id_Ruta", "NombreRuta", modelo.Id_Ruta);
                    return View(modelo);
                }

                // Generar código de seguimiento correlativo único
                var anio = DateTime.Now.Year;
                var countTotal = await _context.Encomiendas.CountAsync();
                modelo.CodigoSeguimiento = $"ECE-{anio}-{(countTotal + 1):D4}";
                modelo.FechaRegistro = DateTime.Now;
                modelo.Estado = "Registrado";

                // Asignar trabajador que registra
                var emailActual = User.FindFirstValue(ClaimTypes.Email);
                var nombreActual = User.Identity?.Name;
                var usuario = await _context.Usuarios
                    .FirstOrDefaultAsync(u => u.Email == emailActual || u.Name == nombreActual);
                if (usuario != null)
                    modelo.Id_Usuario = usuario.Id_Usuario;

                _context.Encomiendas.Add(modelo);
                await _context.SaveChangesAsync();

                TempData["Exito"] = $"¡Encomienda registrada con éxito! Código: {modelo.CodigoSeguimiento}";
                return RedirectToAction("Detalle", new { id = modelo.Id_Encomienda });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error al registrar la encomienda: " + ex.Message);
                try
                {
                    var rutas = await _context.Rutas
                        .Where(r => r.Origen.ToLower() != r.Destino.ToLower())
                        .OrderBy(r => r.Origen).ToListAsync();
                    ViewBag.Rutas = new SelectList(rutas, "Id_Ruta", "NombreRuta", modelo.Id_Ruta);
                }
                catch
                {
                    ViewBag.Rutas = new SelectList(new List<Ruta>(), "Id_Ruta", "NombreRuta");
                }
                return View(modelo);
            }
        }

        // =============================================
        // 3. DETALLE / COMPROBANTE DE ENCOMIENDA
        // =============================================
        [HttpGet]
        public async Task<IActionResult> Detalle(int id)
        {
            try
            {
                var enc = await _context.Encomiendas
                    .Include(e => e.ObjetoRuta)
                    .Include(e => e.UsuarioRegistro)
                    .FirstOrDefaultAsync(e => e.Id_Encomienda == id);

                if (enc == null) return NotFound();

                // Vendedor solo puede ver las suyas
                if (!User.IsInRole("Gerente"))
                {
                    var emailActual = User.FindFirstValue(ClaimTypes.Email);
                    var nombreActual = User.Identity?.Name;
                    var usuarioActual = await _context.Usuarios
                        .FirstOrDefaultAsync(u => u.Email == emailActual || u.Name == nombreActual);
                    if (usuarioActual != null && enc.Id_Usuario.HasValue && enc.Id_Usuario != usuarioActual.Id_Usuario)
                        return Forbid();
                }

                return View(enc);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al cargar el detalle: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // =============================================
        // 4. EDITAR ENCOMIENDA
        // =============================================
        [HttpGet]
        public async Task<IActionResult> Editar(int id)
        {
            try
            {
                var enc = await _context.Encomiendas.FindAsync(id);
                if (enc == null) return NotFound();

                // Solo se puede editar si está en estado Registrado
                if (enc.Estado != "Registrado")
                {
                    TempData["Error"] = "Solo se pueden editar encomiendas en estado 'Registrado'.";
                    return RedirectToAction("Detalle", new { id });
                }

                // Vendedor solo puede editar las suyas
                if (!User.IsInRole("Gerente"))
                {
                    var emailActual = User.FindFirstValue(ClaimTypes.Email);
                    var nombreActual = User.Identity?.Name;
                    var usuarioActual = await _context.Usuarios
                        .FirstOrDefaultAsync(u => u.Email == emailActual || u.Name == nombreActual);
                    if (usuarioActual != null && enc.Id_Usuario.HasValue && enc.Id_Usuario != usuarioActual.Id_Usuario)
                        return Forbid();
                }

                var rutas = await _context.Rutas
                    .Where(r => r.Origen.ToLower() != r.Destino.ToLower())
                    .OrderBy(r => r.Origen).ToListAsync();
                ViewBag.Rutas = new SelectList(rutas, "Id_Ruta", "NombreRuta", enc.Id_Ruta);
                return View(enc);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al cargar la encomienda: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Editar(int id, Encomienda modelo)
        {
            if (id != modelo.Id_Encomienda)
                return BadRequest();

            try
            {
                var enc = await _context.Encomiendas.FindAsync(id);
                if (enc == null) return NotFound();

                if (enc.Estado != "Registrado")
                {
                    TempData["Error"] = "Solo se pueden editar encomiendas en estado 'Registrado'.";
                    return RedirectToAction("Detalle", new { id });
                }

                ModelState.Remove("CodigoSeguimiento");
                ModelState.Remove("Estado");
                ModelState.Remove("ObjetoRuta");
                ModelState.Remove("UsuarioRegistro");

                if (!ModelState.IsValid)
                {
                    var rutas = await _context.Rutas
                        .Where(r => r.Origen.ToLower() != r.Destino.ToLower())
                        .OrderBy(r => r.Origen).ToListAsync();
                    ViewBag.Rutas = new SelectList(rutas, "Id_Ruta", "NombreRuta", modelo.Id_Ruta);
                    return View(modelo);
                }

                // Actualizar solo los campos editables
                enc.NombreRemitente      = modelo.NombreRemitente;
                enc.DniRemitente         = modelo.DniRemitente;
                enc.TelefonoRemitente    = modelo.TelefonoRemitente;
                enc.NombreDestinatario   = modelo.NombreDestinatario;
                enc.DniDestinatario      = modelo.DniDestinatario;
                enc.TelefonoDestinatario = modelo.TelefonoDestinatario;
                enc.Descripcion          = modelo.Descripcion;
                enc.PesoKg               = modelo.PesoKg;
                enc.PrecioEnvio          = modelo.PrecioEnvio;
                enc.Id_Ruta              = modelo.Id_Ruta;
                enc.Observaciones        = modelo.Observaciones;

                await _context.SaveChangesAsync();

                TempData["Exito"] = "Encomienda actualizada correctamente.";
                return RedirectToAction("Detalle", new { id });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error al actualizar la encomienda: " + ex.Message);
                var rutas = await _context.Rutas
                    .Where(r => r.Origen.ToLower() != r.Destino.ToLower())
                    .OrderBy(r => r.Origen).ToListAsync();
                ViewBag.Rutas = new SelectList(rutas, "Id_Ruta", "NombreRuta", modelo.Id_Ruta);
                return View(modelo);
            }
        }

        // =============================================
        // 5. CAMBIAR ESTADO
        // =============================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CambiarEstado(int id, string nuevoEstado)
        {
            try
            {
                var enc = await _context.Encomiendas.FindAsync(id);
                if (enc == null) return NotFound();

                var estadosPermitidos = new[] { "Registrado", "En transito", "Entregado", "No reclamado", "Anulado" };
                if (!estadosPermitidos.Contains(nuevoEstado))
                {
                    TempData["Error"] = "Estado no válido.";
                    return RedirectToAction("Detalle", new { id });
                }

                // Vendedor solo puede cambiar estado de sus propias encomiendas
                if (!User.IsInRole("Gerente"))
                {
                    var emailActual = User.FindFirstValue(ClaimTypes.Email);
                    var nombreActual = User.Identity?.Name;
                    var usuarioActual = await _context.Usuarios
                        .FirstOrDefaultAsync(u => u.Email == emailActual || u.Name == nombreActual);
                    if (usuarioActual != null && enc.Id_Usuario.HasValue && enc.Id_Usuario != usuarioActual.Id_Usuario)
                        return Forbid();
                }

                if (nuevoEstado == "Entregado" && enc.FechaEntrega == null)
                    enc.FechaEntrega = DateTime.Now;

                enc.Estado = nuevoEstado;
                await _context.SaveChangesAsync();

                TempData["Exito"] = $"Estado de {enc.CodigoSeguimiento} actualizado a '{nuevoEstado}'.";
                return RedirectToAction("Detalle", new { id });
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al cambiar estado: " + ex.Message;
                return RedirectToAction("Detalle", new { id });
            }
        }

        // Acción de compatibilidad para el botón "Marcar Entregada"
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarcarEntregada(int id)
        {
            return await CambiarEstado(id, "Entregado");
        }

        // =============================================
        // 6. BUSCAR POR CODIGO (RASTREO PUBLICO)
        // =============================================
        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> Rastrear(string? codigo)
        {
            Encomienda? enc = null;
            try
            {
                if (!string.IsNullOrWhiteSpace(codigo))
                {
                    enc = await _context.Encomiendas
                        .Include(e => e.ObjetoRuta)
                        .FirstOrDefaultAsync(e => e.CodigoSeguimiento.ToUpper() == codigo.Trim().ToUpper());

                    if (enc == null)
                        TempData["ErrorRastreo"] = $"No se encontró ninguna encomienda con el código '{codigo}'.";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorRastreo"] = "Ocurrió un error al buscar: " + ex.Message;
            }

            ViewBag.CodigoBuscado = codigo;
            return View(enc);
        }
    }
}
