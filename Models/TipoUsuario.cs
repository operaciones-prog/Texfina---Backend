using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TexfinaApi.Models
{
    [Table("TIPO_USUARIO")]
    public class TipoUsuario
    {
        [Key]
        [Column("id_tipo_usuario")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int IdTipoUsuario { get; set; }

        [MaxLength(100)]
        [Column("descripcion")]
        public string? Descripcion { get; set; }

        [Column("requiere_cierre_automatico")]
        public bool? RequiereCierreAutomatico { get; set; }

        // Propiedades de navegación
        public virtual ICollection<Usuario> Usuarios { get; set; } = new List<Usuario>();
    }
} 