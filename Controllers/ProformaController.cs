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
using SistemaChotaExpress.Services;

namespace SistemaChotaExpress.Controllers
{
    [Authorize]
    public class ProformaController : Controller
    {
        private readonly AppDbContext _context;

        public ProformaController(AppDbContext context)
        {
            _context = context;
        }

        // =============================================
        // 1. LISTADO DE PROFORMAS
        // =============================================
        [HttpGet]
        public async Task<IActionResult> Index(string? buscar, string? estado)
        {
            try
            {
                var query = _context.ProformasViajes
                    .Include(p => p.ObjetoRuta)
                    .Include(p => p.UsuarioRegistro)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(buscar))
                {
                    buscar = buscar.Trim().ToLower();
                    query = query.Where(p =>
                        p.CodigoProforma.ToLower().Contains(buscar) ||
                        p.NombreCliente.ToLower().Contains(buscar) ||
                        (p.NumeroDocumento != null && p.NumeroDocumento.Contains(buscar)) ||
                        (p.TelefonoCliente != null && p.TelefonoCliente.Contains(buscar)));
                }

                if (!string.IsNullOrWhiteSpace(estado) && estado != "Todos")
                {
                    query = query.Where(p => p.Estado == estado);
                }

                var lista = await query.OrderByDescending(p => p.FechaEmision).ToListAsync();

                ViewBag.Buscar = buscar;
                ViewBag.EstadoFiltro = estado ?? "Todos";
                return View(lista);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al listar las proformas: " + ex.Message;
                return View(new List<ProformaViaje>());
            }
        }

        // =============================================
        // 2. CREAR PROFORMA (GET)
        // =============================================
        [HttpGet]
        public async Task<IActionResult> Crear()
        {
            ViewBag.Lugares = GeneradorViajesService.LugaresOficiales.OrderBy(l => l).ToList();

            var totalCount = await _context.ProformasViajes.CountAsync();
            ViewBag.SiguienteCodigo = $"PROF-{(totalCount + 1):D4}";

            var modelo = new ProformaViaje
            {
                FechaViaje = DateTime.Today.AddDays(1),
                TipoServicio = "Viaje Expreso (Combi Completa)",
                CantidadPasajeros = 15,
                ValidezDias = 5,
                PrecioTotal = 0,
                Observaciones = "Incluye combustible, chofer profesional calificado, seguro SOAT vigente y monitoreo satelital SUTRAN."
            };

            return View(modelo);
        }

        // =============================================
        // 3. CREAR PROFORMA (POST)
        // =============================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear(ProformaViaje modelo, string? origenRuta, string? destinoRuta)
        {
            try
            {
                // Asignar o crear ruta
                if (!string.IsNullOrWhiteSpace(origenRuta) && !string.IsNullOrWhiteSpace(destinoRuta))
                {
                    var rutaMatch = await _context.Rutas.FirstOrDefaultAsync(r =>
                        r.Origen.ToLower() == origenRuta.Trim().ToLower() &&
                        r.Destino.ToLower() == destinoRuta.Trim().ToLower());

                    if (rutaMatch == null)
                    {
                        rutaMatch = new Ruta
                        {
                            Origen = origenRuta.Trim(),
                            Destino = destinoRuta.Trim(),
                            DuracionHoras = 2.0
                        };
                        _context.Rutas.Add(rutaMatch);
                        await _context.SaveChangesAsync();
                    }
                    modelo.Id_Ruta = rutaMatch.Id_Ruta;
                }

                ModelState.Remove("CodigoProforma");
                ModelState.Remove("Estado");
                ModelState.Remove("ObjetoRuta");
                ModelState.Remove("UsuarioRegistro");

                if (!ModelState.IsValid)
                {
                    ViewBag.Lugares = GeneradorViajesService.LugaresOficiales.OrderBy(l => l).ToList();
                    var totalCount = await _context.ProformasViajes.CountAsync();
                    ViewBag.SiguienteCodigo = $"PROF-{(totalCount + 1):D4}";
                    return View(modelo);
                }

                // Generar código correlativo
                var total = await _context.ProformasViajes.CountAsync();
                modelo.CodigoProforma = $"PROF-{(total + 1):D4}";
                modelo.FechaEmision = DateTime.Now;
                modelo.Estado = "Emitida";

                // Asignar usuario logueado
                var emailActual = User.FindFirstValue(ClaimTypes.Email);
                var nombreActual = User.Identity?.Name;
                var usuario = await _context.Usuarios
                    .FirstOrDefaultAsync(u => u.Email == emailActual || u.Name == nombreActual);
                if (usuario != null)
                {
                    modelo.Id_Usuario = usuario.Id_Usuario;
                }

                _context.ProformasViajes.Add(modelo);
                await _context.SaveChangesAsync();

                TempData["Exito"] = $"¡Proforma {modelo.CodigoProforma} registrada con éxito!";
                return RedirectToAction("Imprimir", new { id = modelo.Id_Proforma });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error al registrar la proforma: " + ex.Message);
                ViewBag.Lugares = GeneradorViajesService.LugaresOficiales.OrderBy(l => l).ToList();
                return View(modelo);
            }
        }

        // =============================================
        // 4. IMPRIMIR PROFORMA (HOJA A4 PROFESIONAL)
        // =============================================
        [HttpGet]
        public async Task<IActionResult> Imprimir(int id)
        {
            var proforma = await _context.ProformasViajes
                .Include(p => p.ObjetoRuta)
                .Include(p => p.UsuarioRegistro)
                .FirstOrDefaultAsync(p => p.Id_Proforma == id);

            if (proforma == null)
            {
                TempData["Error"] = "Proforma no encontrada.";
                return RedirectToAction("Index");
            }

            return View(proforma);
        }

        // =============================================
        // 5. CAMBIAR ESTADO
        // =============================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CambiarEstado(int id, string nuevoEstado)
        {
            var proforma = await _context.ProformasViajes.FindAsync(id);
            if (proforma != null)
            {
                proforma.Estado = nuevoEstado;
                await _context.SaveChangesAsync();
                TempData["Exito"] = $"Estado de la proforma actualizado a '{nuevoEstado}'.";
            }
            return RedirectToAction("Index");
        }
    }
}
