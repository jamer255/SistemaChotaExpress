using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SistemaChotaExpress.Models
{
    public class Usuario
    {
        [Key]
        public int Id_Usuario { get; set; }

        [Required, StringLength(100)]
        public string Name { get; set; }

        [Required, EmailAddress, StringLength(100)]
        public string Email { get; set; }

        [Required, StringLength(255)]
        public string Password { get; set; }

        [Required, StringLength(15)]
        public string Telefono { get; set; }

        [Required]
        public int Id_Rol { get; set; }

        [ForeignKey("Id_Rol")]
        public virtual Roles? ObjetoRol { get; set; }
    }
}
