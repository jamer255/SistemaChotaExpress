using System.ComponentModel.DataAnnotations;

namespace SistemaChotaExpress.Models
{
    public class Pasajero
    {
        [Key]
        public int Id_Pasajero { get; set; }

        [Required]
        [StringLength(10)]
        public string TipoDocumento { get; set; } = "DNI"; // DNI, CE, RUC

        [Required(ErrorMessage = "El número de documento es obligatorio")]
        [StringLength(20)]
        public string NumeroDocumento { get; set; }

        [Required(ErrorMessage = "El nombre completo o razón social es obligatorio")]
        [StringLength(150)]
        public string NombreCompleto { get; set; }

        [StringLength(20)]
        public string? Telefono { get; set; }

        [StringLength(100)]
        [EmailAddress(ErrorMessage = "Formato de correo inválido")]
        public string? Correo { get; set; }
    }
}
