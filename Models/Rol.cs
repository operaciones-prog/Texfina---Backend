using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TexfinaApi.Models
{
    [Table("ROL")]
    public class Rol
    {
        [Key]
        [MaxLength(50)]
        [Column("id_rol")]
        public string IdRol { get; set; } = string.Empty;

        [MaxLength(100)]
        [Column("nombre")]
        public string? Nombre { get; set; }

        [MaxLength(200)]
        [Column("descripcion")]
        public string? Descripcion { get; set; }

        // Propiedades de navegación
        public virtual ICollection<Usuario> Usuarios { get; set; } = new List<Usuario>();
        public virtual ICollection<RolPermiso> RolPermisos { get; set; } = new List<RolPermiso>();
    }
} 