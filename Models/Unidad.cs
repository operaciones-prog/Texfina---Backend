using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TexfinaApi.Models
{
    [Table("UNIDAD")]
    public class Unidad
    {
        [Key]
        [MaxLength(50)]
        [Column("id_unidad")]
        public string IdUnidad { get; set; } = string.Empty;

        [MaxLength(100)]
        [Column("nombre")]
        public string? Nombre { get; set; }

        // Propiedades de navegación
        public virtual ICollection<Insumo> Insumos { get; set; } = new List<Insumo>();
        public virtual ICollection<Stock> Stocks { get; set; } = new List<Stock>();
        public virtual ICollection<Ingreso> Ingresos { get; set; } = new List<Ingreso>();
    }
} 