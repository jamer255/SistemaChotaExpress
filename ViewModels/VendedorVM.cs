using System.ComponentModel.DataAnnotations;

namespace SistemaChotaExpress.ViewModels
{
    public class VendedorVM
    {
        [Required(ErrorMessage = "Su nombre completo es requerido.")]
        public string Name { get; set; }

        [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
        [EmailAddress(ErrorMessage = "Formato de correo inválido.")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Defina una contraseña de acceso.")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Debe tener al menos 6 caracteres.")]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        [Required(ErrorMessage = "Es obligatorio repetir la contraseña.")]
        [Compare("Password", ErrorMessage = "Las contraseñas no coinciden.")]
        [DataType(DataType.Password)]
        public string RepetPassword { get; set; }

        [Required(ErrorMessage = "El teléfono celular es obligatorio.")]
        [RegularExpression(@"^[0-9]{9}$", ErrorMessage = "Ingrese un número celular válido (9 dígitos).")]
        public string Telefono { get; set; }

        public int Id_Rol { get; set; } = 2; // 1 = Gerente, 2 = Vendedor
    }
}
