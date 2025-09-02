using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TexfinaApi.Models
{
    [Table("ROL_PERMISO")]
    public class RolPermiso
    {
        [Key]
        [Column("id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [MaxLength(50)]
        [Column("id_rol")]
        public string? IdRol { get; set; }

        [Column("id_permiso")]
        public int? IdPermiso { get; set; }

        // Propiedades de navegación
        [ForeignKey("IdRol")]
        public virtual Rol? Rol { get; set; }

        [ForeignKey("IdPermiso")]
        public virtual Permiso? Permiso { get; set; }
    }
} 