using System;
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
        // LISTA DE ENCOMIENDAS
        // =============================================
        [HttpGet]
        public async Task<IActionResult> Index(string? buscar, string? estado)
        {
            var query = _context.Encomiendas
                .Include(e => e.ObjetoRuta)
                .Include(e => e.UsuarioRegistro)
                .AsQueryable();

            // Trabajadores solo ven las suyas; Gerente ve todas
            if (!User.IsInRole("Gerente"))
            {
                var emailActual = User.Identity?.Name;
                var usuarioActual = await _context.Usuarios.FirstOrDefaultAsync(u => u.Email == emailActual);
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
                    (e.DniDestinatario != null && e.DniDestinatario.Contains(buscar)));
            }

            if (!string.IsNullOrWhiteSpace(estado) && estado != "Todos")
                query = query.Where(e => e.Estado == estado);

            var lista = await query.OrderByDescending(e => e.FechaRegistro).ToListAsync();

            ViewBag.Buscar = buscar;
            ViewBag.EstadoFiltro = estado ?? "Todos";
            return View(lista);
        }

        // =============================================
        // FORMULARIO DE REGISTRO
        // =============================================
        [HttpGet]
        public async Task<IActionResult> Registrar()
        {
            ViewBag.Rutas = new SelectList(await _context.Rutas.OrderBy(r => r.Origen).ToListAsync(),
                "Id_Ruta", "NombreRuta");
            return View(new Encomienda());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Registrar(Encomienda modelo)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Rutas = new SelectList(await _context.Rutas.OrderBy(r => r.Origen).ToListAsync(),
                    "Id_Ruta", "NombreRuta");
                return View(modelo);
            }

            // Generar codigo de seguimiento unico
            var anio = DateTime.Now.Year;
            var ultimoCodigo = await _context.Encomiendas
                .Where(e => e.FechaRegistro.Year == anio)
                .CountAsync();
            modelo.CodigoSeguimiento = $"ECE-{anio}-{(ultimoCodigo + 1):D4}";
            modelo.FechaRegistro = DateTime.Now;
            modelo.Estado = "Registrado";

            // Asignar usuario registrador
            var emailActual = User.Identity?.Name;
            var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Email == emailActual);
            if (usuario != null)
                modelo.Id_Usuario = usuario.Id_Usuario;

            _context.Encomiendas.Add(modelo);
            await _context.SaveChangesAsync();

            TempData["Exito"] = $"Encomienda registrada con codigo: {modelo.CodigoSeguimiento}";
            return RedirectToAction("Detalle", new { id = modelo.Id_Encomienda });
        }

        // =============================================
        // DETALLE / COMPROBANTE
        // =============================================
        [HttpGet]
        public async Task<IActionResult> Detalle(int id)
        {
            var enc = await _context.Encomiendas
                .Include(e => e.ObjetoRuta)
                .Include(e => e.UsuarioRegistro)
                .FirstOrDefaultAsync(e => e.Id_Encomienda == id);

            if (enc == null) return NotFound();
            return View(enc);
        }

        // =============================================
        // MARCAR COMO ENTREGADA
        // =============================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarcarEntregada(int id)
        {
            var enc = await _context.Encomiendas.FindAsync(id);
            if (enc == null) return NotFound();

            enc.Estado = "Entregado";
            enc.FechaEntrega = DateTime.Now;
            await _context.SaveChangesAsync();

            TempData["Exito"] = $"Encomienda {enc.CodigoSeguimiento} marcada como entregada.";
            return RedirectToAction("Detalle", new { id });
        }
    }
}
