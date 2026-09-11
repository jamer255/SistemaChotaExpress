using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SistemaChotaExpress.Models
{
    public class Ruta
    {
        [Key]
        public int Id_Ruta { get; set; }

        [Required(ErrorMessage = "El origen es obligatorio")]
        [StringLength(100)]
        public string Origen { get; set; } = string.Empty;

        [Required(ErrorMessage = "El destino es obligatorio")]
        [StringLength(100)]
        public string Destino { get; set; } = string.Empty;

        [Required(ErrorMessage = "La duración es obligatoria")]
        [Range(0.5, 48.0, ErrorMessage = "La duración debe ser entre 0.5 y 48 horas")]
        public double DuracionHoras { get; set; }

        /// <summary>Nombre de la ruta para mostrar en selects y reportes.</summary>
        [NotMapped]
        public string NombreRuta => $"{Origen} → {Destino} ({DuracionHoras} hrs)";
    }
}
