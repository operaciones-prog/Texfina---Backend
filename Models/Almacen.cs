using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TexfinaApi.Models
{
    [Table("ALMACEN")]
    public class Almacen
    {
        [Key]
        [Column("id_almacen")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int IdAlmacen { get; set; }

        [MaxLength(100)]
        [Column("nombre")]
        public string? Nombre { get; set; }

        [MaxLength(200)]
        [Column("ubicacion")]
        public string? Ubicacion { get; set; }

        // Propiedades de navegación
        public virtual ICollection<Stock> Stocks { get; set; } = new List<Stock>();
    }
} 