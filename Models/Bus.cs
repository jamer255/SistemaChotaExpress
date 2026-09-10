using System.ComponentModel.DataAnnotations;

namespace SistemaChotaExpress.Models
{
    public class Bus
    {
        [Key]
        public int Id_Bus { get; set; }

        [StringLength(100)]
        public string? Placa { get; set; } = "S/P";

        [Required(ErrorMessage = "El nombre del vehículo es obligatorio")]
        [StringLength(100)]
        public string NombreVehiculo { get; set; } = "Combi Express";

        [StringLength(100)]
        public string? NombreConductor { get; set; } = "Conductor Asignado";

        [Required(ErrorMessage = "El modelo es obligatorio")]
        [StringLength(50)]
        public string Modelo { get; set; } = "Toyota HiAce";

        [Required(ErrorMessage = "La capacidad es obligatoria")]
        [Range(5, 100, ErrorMessage = "La capacidad debe estar entre 5 y 100 asientos")]
        public int Capacidad { get; set; } = 15;
    }
}
