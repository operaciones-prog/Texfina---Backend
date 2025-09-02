using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TexfinaApi.Models
{
    [Table("RECETA")]
    public class Receta
    {
        [Key]
        [Column("id_receta")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int IdReceta { get; set; }

        [MaxLength(200)]
        [Column("nombre")]
        public string? Nombre { get; set; }

        // Propiedades de navegación
        public virtual ICollection<RecetaDetalle> RecetaDetalles { get; set; } = new List<RecetaDetalle>();
    }
} 