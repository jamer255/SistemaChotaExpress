using System.ComponentModel.DataAnnotations;

namespace SistemaChotaExpress.Models
{
    public class Ruta
    {
        [Key]
        public int Id_Ruta { get; set; }

        [Required(ErrorMessage = "El origen es obligatorio")]
        [StringLength(100)]
        public string Origen { get; set; }

        [Required(ErrorMessage = "El destino es obligatorio")]
        [StringLength(100)]
        public string Destino { get; set; }

        [Required(ErrorMessage = "La duración es obligatoria")]
        [Range(0.5, 48.0, ErrorMessage = "La duración debe ser entre 0.5 y 48 horas")]
        public double DuracionHoras { get; set; }
    }
}
