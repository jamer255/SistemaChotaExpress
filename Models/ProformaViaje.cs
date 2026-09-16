using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace SistemaChotaExpress.Models
{
    public class ProformaViaje
    {
        [Key]
        public int Id_Proforma { get; set; }

        [ValidateNever]
        [StringLength(25)]
        [Display(Name = "Código Proforma")]
        public string CodigoProforma { get; set; } = string.Empty;

        // --- CLIENTE ---
        [Required(ErrorMessage = "El nombre del cliente o empresa es obligatorio")]
        [StringLength(120)]
        [Display(Name = "Cliente / Razón Social")]
        public string NombreCliente { get; set; } = string.Empty;

        [StringLength(10)]
        [Display(Name = "Tipo de Documento")]
        public string TipoDocumento { get; set; } = "DNI"; // DNI o RUC

        [StringLength(20)]
        [Display(Name = "N° Documento")]
        public string? NumeroDocumento { get; set; }

        [StringLength(20)]
        [Display(Name = "Teléfono / WhatsApp")]
        public string? TelefonoCliente { get; set; }

        // --- DETALLES DEL VIAJE ---
        [Display(Name = "Ruta")]
        public int? Id_Ruta { get; set; }
        [ForeignKey("Id_Ruta")]
        [ValidateNever]
        public virtual Ruta? ObjetoRuta { get; set; }

        [Required(ErrorMessage = "La fecha de viaje es obligatoria")]
        [DataType(DataType.Date)]
        [Display(Name = "Fecha de Viaje")]
        public DateTime FechaViaje { get; set; } = DateTime.Today;

        [StringLength(50)]
        [Display(Name = "Horario / Turno")]
        public string? HoraSalida { get; set; }

        [Required(ErrorMessage = "El tipo de servicio es obligatorio")]
        [StringLength(60)]
        [Display(Name = "Tipo de Servicio")]
        public string TipoServicio { get; set; } = "Viaje Expreso (Combi Completa)"; 
        // Opciones: "Viaje Expreso (Combi Completa)", "Pasajes Individuales"

        [Range(1, 100, ErrorMessage = "La cantidad debe ser al menos 1")]
        [Display(Name = "Cantidad de Pasajeros / Asientos")]
        public int CantidadPasajeros { get; set; } = 15;

        [Column(TypeName = "decimal(10,2)")]
        [Display(Name = "Precio Unitario (S/)")]
        public decimal? PrecioUnitario { get; set; }

        [Required(ErrorMessage = "El precio total es obligatorio")]
        [Range(0.01, 99999.99, ErrorMessage = "El monto debe ser mayor a 0")]
        [Column(TypeName = "decimal(10,2)")]
        [Display(Name = "Precio Total (S/)")]
        public decimal PrecioTotal { get; set; }

        [Range(1, 90, ErrorMessage = "La validez debe ser entre 1 y 90 días")]
        [Display(Name = "Días de Validez")]
        public int ValidezDias { get; set; } = 5;

        [StringLength(500)]
        [Display(Name = "Condiciones / Observaciones")]
        public string? Observaciones { get; set; }

        // --- ESTADO & AUDITORÍA ---
        [ValidateNever]
        [StringLength(20)]
        public string Estado { get; set; } = "Emitida"; // Emitida, Aprobada, Vencida

        public DateTime FechaEmision { get; set; } = DateTime.Now;

        public int? Id_Usuario { get; set; }
        [ForeignKey("Id_Usuario")]
        [ValidateNever]
        public virtual Usuario? UsuarioRegistro { get; set; }
    }
}
