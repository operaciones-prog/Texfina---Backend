using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TexfinaApi.Models
{
    [Table("CLASE")]
    public class Clase
    {
        [Key]
        [MaxLength(50)]
        [Column("id_clase")]
        public string IdClase { get; set; } = string.Empty;

        [MaxLength(100)]
        [Column("familia")]
        public string? Familia { get; set; }

        [MaxLength(100)]
        [Column("sub_familia")]
        public string? SubFamilia { get; set; }

        // Propiedades de navegación
        public virtual ICollection<Insumo> Insumos { get; set; } = new List<Insumo>();
    }
} 