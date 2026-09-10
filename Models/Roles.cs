using System.ComponentModel.DataAnnotations;

namespace SistemaChotaExpress.Models
{
    public class Roles
    {
        [Key]
        public int Id_Rol { get; set; }

        [Required]
        [StringLength(50)]
        public string NombreRol { get; set; } // "Gerente" o "Vendedor"
    }
}
