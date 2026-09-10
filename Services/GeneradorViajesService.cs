using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SistemaChotaExpress.Data;
using SistemaChotaExpress.Models;

namespace SistemaChotaExpress.Services
{
    public static class GeneradorViajesService
    {
        /// <summary>
        /// Asegura que existan salidas horarias (de 00:00 a 23:00) para una fecha y una RUTA específica.
        /// Respeta cualquier salida que el Gerente o el trabajador ya hayan programado o personalizado.
        /// </summary>
        public static async Task AsegurarViajesParaFechaYRutaAsync(AppDbContext context, DateTime fecha, int idRuta)
        {
            var startOfDay = fecha.Date;
            var endOfDay = startOfDay.AddDays(1);

            // Horas que ya tienen al menos 1 viaje registrado para ESTA RUTA en ese día
            var horasExistentes = await context.Viajes
                .Where(v => v.Id_Ruta == idRuta && v.FechaHoraSalida >= startOfDay && v.FechaHoraSalida < endOfDay)
                .Select(v => v.FechaHoraSalida.Hour)
                .Distinct()
                .ToListAsync();

            var horasSet = new HashSet<int>(horasExistentes);
            var nuevosViajes = new List<Viaje>();

            for (int hora = 0; hora < 24; hora++)
            {
                if (!horasSet.Contains(hora))
                {
                    nuevosViajes.Add(new Viaje
                    {
                        Id_Ruta = idRuta,
                        Id_Bus = null,
                        PlacaVehiculo = null,
                        FechaHoraSalida = startOfDay.AddHours(hora),
                        Precio = 0.00m,
                        NombreConductor = null,
                        Estado = "Programado"
                    });
                    horasSet.Add(hora);
                }
            }

            if (nuevosViajes.Any())
            {
                await context.Viajes.AddRangeAsync(nuevosViajes);
                await context.SaveChangesAsync();
            }

            await LimpiarDuplicadosPorRutaAsync(context, fecha.Date, idRuta);
        }

        /// <summary>
        /// Asegura salidas para un rango de fechas. Mantenido para compatibilidad.
        /// </summary>
        public static async Task AsegurarViajesParaRangoAsync(AppDbContext context, DateTime fechaInicio, DateTime fechaFin)
        {
            for (var current = fechaInicio.Date; current <= fechaFin.Date; current = current.AddDays(1))
            {
                await AsegurarViajesParaFechaAsync(context, current);
            }
        }

        /// <summary>
        /// Asegura salidas para una fecha dada. Mantenido para compatibilidad.
        /// </summary>
        public static async Task AsegurarViajesParaFechaAsync(AppDbContext context, DateTime fecha)
        {
            // Para cada ruta registrada en el sistema, garantizar salidas
            var rutasIds = await context.Rutas.Select(r => r.Id_Ruta).ToListAsync();
            foreach (var idRuta in rutasIds)
            {
                await AsegurarViajesParaFechaYRutaAsync(context, fecha, idRuta);
            }
        }

        /// <summary>
        /// Limpia salidas duplicadas vacías (sin ventas) pertenecientes a la MISMA ruta y hora.
        /// Nunca elimina viajes de otras rutas diferentes.
        /// </summary>
        public static async Task LimpiarDuplicadosPorRutaAsync(AppDbContext context, DateTime fecha, int idRuta)
        {
            var startOfDay = fecha.Date;
            var endOfDay = startOfDay.AddDays(1);

            var viajesSinVentas = await context.Viajes
                .Include(v => v.Ventas)
                .Where(v => v.Id_Ruta == idRuta &&
                            v.Estado == "Programado" &&
                            v.FechaHoraSalida >= startOfDay &&
                            v.FechaHoraSalida < endOfDay &&
                            !v.Ventas.Any(vt => vt.Estado == "Vendido" || vt.Estado == "Reservado"))
                .ToListAsync();

            var grupos = viajesSinVentas
                .GroupBy(v => new { Fecha = v.FechaHoraSalida.Date, Hora = v.FechaHoraSalida.Hour, Ruta = v.Id_Ruta });

            var aEliminar = new List<Viaje>();
            foreach (var g in grupos)
            {
                if (g.Count() > 1)
                {
                    // Conservar el que tenga placa, conductor o bus asignado; si no, el más antiguo
                    var conservar = g.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v.PlacaVehiculo) || !string.IsNullOrWhiteSpace(v.NombreConductor) || v.Id_Bus != null)
                                    ?? g.OrderBy(v => v.Id_Viaje).First();
                    aEliminar.AddRange(g.Where(v => v.Id_Viaje != conservar.Id_Viaje));
                }
            }

            if (aEliminar.Any())
            {
                context.Viajes.RemoveRange(aEliminar);
                await context.SaveChangesAsync();
            }
        }

        /// <summary>
        /// Limpieza general de duplicados asegurando agrupar por Ruta.
        /// </summary>
        public static async Task LimpiarDuplicadosInicialesAsync(AppDbContext context)
        {
            var viajesSinVentas = await context.Viajes
                .Include(v => v.Ventas)
                .Where(v => v.Estado == "Programado" && !v.Ventas.Any(vt => vt.Estado == "Vendido" || vt.Estado == "Reservado"))
                .ToListAsync();

            // Agrupar por Fecha, Hora Y RUTA — para no mezclar salidas de distintas rutas
            var grupos = viajesSinVentas
                .GroupBy(v => new { Fecha = v.FechaHoraSalida.Date, Hora = v.FechaHoraSalida.Hour, Ruta = v.Id_Ruta });

            var aEliminar = new List<Viaje>();
            foreach (var g in grupos)
            {
                if (g.Count() > 1)
                {
                    var conservar = g.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v.PlacaVehiculo) || !string.IsNullOrWhiteSpace(v.NombreConductor) || v.Id_Bus != null)
                                    ?? g.OrderBy(v => v.Id_Viaje).First();
                    aEliminar.AddRange(g.Where(v => v.Id_Viaje != conservar.Id_Viaje));
                }
            }

            if (aEliminar.Any())
            {
                context.Viajes.RemoveRange(aEliminar);
                await context.SaveChangesAsync();
            }
        }
    }
}
