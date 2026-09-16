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

        /// <summary>Número de licencia de conducir del piloto.</summary>
        [StringLength(20)]
        [Display(Name = "N° Licencia")]
        public string? NumeroLicencia { get; set; }

        [Required(ErrorMessage = "El modelo es obligatorio")]
        [StringLength(50)]
        public string Modelo { get; set; } = "Toyota HiAce";

        /// <summary>Marca del vehículo (Toyota, Hyundai, Kia, etc.).</summary>
        [StringLength(50)]
        [Display(Name = "Marca")]
        public string? Marca { get; set; } = "Toyota";

        [Required(ErrorMessage = "La capacidad es obligatoria")]
        [Range(5, 100, ErrorMessage = "La capacidad debe estar entre 5 y 100 asientos")]
        public int Capacidad { get; set; } = 15;
    }
}
