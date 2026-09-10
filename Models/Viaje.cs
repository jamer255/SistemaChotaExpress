using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SistemaChotaExpress.Models
{
    public class Viaje
    {
        [Key]
        public int Id_Viaje { get; set; }

        // FK al Bus: ahora es OPCIONAL — los vehículos no están pre-asignados al horario
        public int? Id_Bus { get; set; }
        [ForeignKey("Id_Bus")]
        public virtual Bus? ObjetoBus { get; set; }

        // Placa del vehículo que cubre este turno (la registra el trabajador, es opcional)
        [StringLength(20)]
        public string? PlacaVehiculo { get; set; }

        public int? Id_Ruta { get; set; }
        [ForeignKey("Id_Ruta")]
        public virtual Ruta? ObjetoRuta { get; set; }

        [StringLength(100)]
        public string? NombreConductor { get; set; }

        [Required(ErrorMessage = "La fecha y hora de salida son obligatorias")]
        public DateTime FechaHoraSalida { get; set; }

        [Required(ErrorMessage = "El precio es obligatorio")]
        [Range(0, 1000.0, ErrorMessage = "El precio debe ser un valor positivo")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Precio { get; set; } = 0.00m;

        [Required]
        [StringLength(20)]
        public string Estado { get; set; } = "Programado"; // Programado, Completado, Cancelado

        public virtual ICollection<Venta> Ventas { get; set; } = new List<Venta>();
    }
}
