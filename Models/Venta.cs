using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SistemaChotaExpress.Models
{
    public class Venta
    {
        [Key]
        public int Id_Venta { get; set; }

        [Required]
        public int Id_Viaje { get; set; }
        [ForeignKey("Id_Viaje")]
        public virtual Viaje? ObjetoViaje { get; set; }

        [Required]
        public int Id_Pasajero { get; set; }
        [ForeignKey("Id_Pasajero")]
        public virtual Pasajero? ObjetoPasajero { get; set; }

        [Required]
        public int Id_Usuario { get; set; } // El Vendedor que registra la venta
        [ForeignKey("Id_Usuario")]
        public virtual Usuario? ObjetoUsuario { get; set; }

        [Required]
        [Range(1, 100, ErrorMessage = "Asiento no válido")]
        public int NumeroAsiento { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal PrecioPagado { get; set; }

        [Required]
        public DateTime FechaVenta { get; set; } = DateTime.Now;

        [Required]
        [StringLength(20)]
        public string Estado { get; set; } = "Vendido"; // Vendido, Reservado, Cancelado

        [Required]
        [StringLength(30)]
        public string TipoComprobante { get; set; } = "Boleta"; // Boleta, Factura, Ticket

        [StringLength(20)]
        public string? RucEmpresa { get; set; }

        [StringLength(200)]
        public string? RazonSocialEmpresa { get; set; }

        [StringLength(250)]
        public string? DireccionEmpresa { get; set; }

        [StringLength(50)]
        public string? MetodoPago { get; set; } = "Efectivo";

        [StringLength(250)]
        public string? Observaciones { get; set; }
    }
}
