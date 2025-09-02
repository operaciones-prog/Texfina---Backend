using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TexfinaApi.Models
{
    [Table("PERMISO")]
    public class Permiso
    {
        [Key]
        [Column("id_permiso")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int IdPermiso { get; set; }

        [MaxLength(100)]
        [Column("nombre")]
        public string? Nombre { get; set; }

        [MaxLength(255)]
        [Column("descripcion")]
        public string? Descripcion { get; set; }

        // Propiedades de navegación
        public virtual ICollection<RolPermiso> RolPermisos { get; set; } = new List<RolPermiso>();
    }
} 