using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TexfinaApi.Models
{
    [Table("USUARIO")]
    public class Usuario
    {
        [Key]
        [Column("id_usuario")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int IdUsuario { get; set; }

        [Required]
        [MaxLength(50)]
        [Column("username")]
        public string Username { get; set; } = string.Empty;

        [MaxLength(100)]
        [Column("email")]
        public string? Email { get; set; }

        [MaxLength(255)]
        [Column("password_hash")]
        public string? PasswordHash { get; set; }

        [MaxLength(50)]
        [Column("id_rol")]
        public string? IdRol { get; set; }

        [Column("id_tipo_usuario")]
        public int? IdTipoUsuario { get; set; }

        [Column("activo")]
        public bool? Activo { get; set; } = true;

        [Column("created_at")]
        public DateTime? CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("last_login")]
        public DateTime? LastLogin { get; set; }

        // Propiedades de navegación
        [ForeignKey("IdRol")]
        public virtual Rol? Rol { get; set; }

        [ForeignKey("IdTipoUsuario")]
        public virtual TipoUsuario? TipoUsuario { get; set; }

        public virtual ICollection<Sesion> Sesiones { get; set; } = new List<Sesion>();
        public virtual ICollection<LogEvento> LogEventos { get; set; } = new List<LogEvento>();
    }
} 