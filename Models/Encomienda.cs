using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SistemaChotaExpress.Models
{
    public class Encomienda
    {
        [Key]
        public int Id_Encomienda { get; set; }

        [Required]
        [StringLength(20)]
        public string CodigoSeguimiento { get; set; } = string.Empty;

        // --- REMITENTE ---
        [Required(ErrorMessage = "El nombre del remitente es obligatorio")]
        [StringLength(100)]
        [Display(Name = "Nombre del Remitente")]
        public string NombreRemitente { get; set; } = string.Empty;

        [StringLength(15)]
        [Display(Name = "DNI/RUC del Remitente")]
        public string? DniRemitente { get; set; }

        [StringLength(15)]
        [Display(Name = "Telefono del Remitente")]
        public string? TelefonoRemitente { get; set; }

        // --- DESTINATARIO ---
        [Required(ErrorMessage = "El nombre del destinatario es obligatorio")]
        [StringLength(100)]
        [Display(Name = "Nombre del Destinatario")]
        public string NombreDestinatario { get; set; } = string.Empty;

        [StringLength(15)]
        [Display(Name = "DNI/RUC del Destinatario")]
        public string? DniDestinatario { get; set; }

        [StringLength(15)]
        [Display(Name = "Telefono del Destinatario")]
        public string? TelefonoDestinatario { get; set; }

        // --- PAQUETE ---
        [Required(ErrorMessage = "La descripcion del contenido es obligatoria")]
        [StringLength(200)]
        [Display(Name = "Descripcion del contenido")]
        public string Descripcion { get; set; } = string.Empty;

        [Range(0.1, 999.9)]
        [Column(TypeName = "decimal(8,2)")]
        [Display(Name = "Peso (kg)")]
        public decimal? PesoKg { get; set; }

        [Required(ErrorMessage = "El precio es obligatorio")]
        [Range(0, 9999.99)]
        [Column(TypeName = "decimal(10,2)")]
        [Display(Name = "Precio del envio (S/)")]
        public decimal PrecioEnvio { get; set; }

        // --- RUTA ---
        public int? Id_Ruta { get; set; }
        [ForeignKey("Id_Ruta")]
        public virtual Ruta? ObjetoRuta { get; set; }

        // --- ESTADO ---
        [Required]
        [StringLength(20)]
        public string Estado { get; set; } = "Registrado";

        [StringLength(300)]
        [Display(Name = "Observaciones")]
        public string? Observaciones { get; set; }

        public DateTime FechaRegistro { get; set; } = DateTime.Now;
        public DateTime? FechaEntrega { get; set; }

        public int? Id_Usuario { get; set; }
        [ForeignKey("Id_Usuario")]
        public virtual Usuario? UsuarioRegistro { get; set; }
    }
}
