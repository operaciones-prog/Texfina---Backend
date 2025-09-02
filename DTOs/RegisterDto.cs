using System.ComponentModel.DataAnnotations;

namespace TexfinaApi.DTOs
{
    public class RegisterDto
    {
        [Required(ErrorMessage = "El nombre de usuario es requerido")]
        [StringLength(50, ErrorMessage = "El nombre de usuario no puede exceder 50 caracteres")]
        public string Username { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "Formato de email inválido")]
        [StringLength(100, ErrorMessage = "El email no puede exceder 100 caracteres")]
        public string? Email { get; set; }

        [Required(ErrorMessage = "La contraseña es requerida")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "La contraseña debe tener entre 6 y 100 caracteres")]
        public string Password { get; set; } = string.Empty;

        [Compare("Password", ErrorMessage = "Las contraseñas no coinciden")]
        public string ConfirmPassword { get; set; } = string.Empty;

        public string? IdRol { get; set; }
        public int? IdTipoUsuario { get; set; }
    }
} 