namespace TexfinaApi.DTOs
{
    public class AuthResponseDto
    {
        public bool Success { get; set; }
        public string? Token { get; set; }
        public string? RefreshToken { get; set; }
        public DateTime? Expires { get; set; }
        public UserInfoDto? User { get; set; }
        public string? Message { get; set; }
    }

    public class UserInfoDto
    {
        public int IdUsuario { get; set; }
        public string Username { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Rol { get; set; }
        public string? TipoUsuario { get; set; }
        public bool Activo { get; set; }
        public DateTime? LastLogin { get; set; }
        public List<string> Permisos { get; set; } = new List<string>();
    }
} 