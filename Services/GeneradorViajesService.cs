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

        /// <summary>
        /// Lista oficial de los 14 lugares autorizados para Tours Chota Express.
        /// </summary>
        public static readonly List<string> LugaresOficiales = new()
        {
            "Bambamarca",
            "Cajamarca",
            "Chiclayo",
            "Chota",
            "Cobro",
            "Coimolache",
            "Cutervo",
            "Empalme",
            "Hualgayoc",
            "Huambos",
            "Llama",
            "Porvenir",
            "Samangay",
            "San Antonio"
        };

        /// <summary>
        /// Asegura que todas las rutas oficiales para los 14 lugares estén registradas en la base de datos.
        /// </summary>
        public static async Task AsegurarRutasOficialesAsync(AppDbContext context)
        {
            var pares = new (string Origen, string Destino, double Horas)[]
            {
                ("Chota", "Chiclayo", 6.0),
                ("Chiclayo", "Chota", 6.0),
                ("Chota", "Cajamarca", 4.0),
                ("Cajamarca", "Chota", 4.0),
                ("Chota", "Cutervo", 2.0),
                ("Cutervo", "Chota", 2.0),
                ("Chota", "Bambamarca", 1.5),
                ("Bambamarca", "Chota", 1.5),
                ("Chota", "Huambos", 2.0),
                ("Huambos", "Chota", 2.0),
                ("Chota", "Llama", 2.5),
                ("Llama", "Chota", 2.5),
                ("Chota", "Hualgayoc", 2.0),
                ("Hualgayoc", "Chota", 2.0),
                ("Chota", "Cobro", 2.0),
                ("Cobro", "Chota", 2.0),
                ("Chota", "Samangay", 2.0),
                ("Samangay", "Chota", 2.0),
                ("Chota", "Porvenir", 1.5),
                ("Porvenir", "Chota", 1.5),
                ("Chota", "San Antonio", 1.5),
                ("San Antonio", "Chota", 1.5),
                ("Chota", "Coimolache", 2.5),
                ("Coimolache", "Chota", 2.5),
                ("Chota", "Empalme", 2.0),
                ("Empalme", "Chota", 2.0),
                ("Cajamarca", "Chiclayo", 7.0),
                ("Chiclayo", "Cajamarca", 7.0),
                ("Cajamarca", "Bambamarca", 3.0),
                ("Bambamarca", "Cajamarca", 3.0),
                ("Cajamarca", "Hualgayoc", 2.5),
                ("Hualgayoc", "Cajamarca", 2.5),
                ("Bambamarca", "Hualgayoc", 1.0),
                ("Hualgayoc", "Bambamarca", 1.0),
                ("Chiclayo", "Huambos", 4.0),
                ("Huambos", "Chiclayo", 4.0),
                ("Chiclayo", "Llama", 3.5),
                ("Llama", "Chiclayo", 3.5),
                ("Chiclayo", "Cutervo", 6.0),
                ("Cutervo", "Chiclayo", 6.0),
                ("Empalme", "Bambamarca", 1.0),
                ("Bambamarca", "Empalme", 1.0),
                ("Empalme", "Cajamarca", 2.5),
                ("Cajamarca", "Empalme", 2.5),
                ("Coimolache", "Hualgayoc", 1.0),
                ("Hualgayoc", "Coimolache", 1.0)
            };

            try
            {
                var rutasExistentes = await context.Rutas.ToListAsync();
                var nuevas = new List<Ruta>();

                foreach (var p in pares)
                {
                    if (!rutasExistentes.Any(r => r.Origen.Equals(p.Origen, StringComparison.OrdinalIgnoreCase) &&
                                                 r.Destino.Equals(p.Destino, StringComparison.OrdinalIgnoreCase)))
                    {
                        nuevas.Add(new Ruta { Origen = p.Origen, Destino = p.Destino, DuracionHoras = p.Horas });
                    }
                }

                if (nuevas.Any())
                {
                    await context.Rutas.AddRangeAsync(nuevas);
                    await context.SaveChangesAsync();
                }
            }
            catch { }
        }
    }
}
