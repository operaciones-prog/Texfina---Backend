using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TexfinaApi.Models
{
    [Table("CONSUMO")]
    public class Consumo
    {
        [Key]
        [Column("id_consumo")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int IdConsumo { get; set; }

        [Column("id_insumo")]
        public int? IdInsumo { get; set; }

        [MaxLength(100)]
        [Column("area")]
        public string? Area { get; set; }

        [Column("fecha")]
        public DateOnly? Fecha { get; set; }

        [Column("cantidad")]
        public float? Cantidad { get; set; }

        [Column("id_lote")]
        public int? IdLote { get; set; }

        [MaxLength(50)]
        [Column("estado")]
        public string? Estado { get; set; }

        // Propiedades de navegación
        [ForeignKey("IdInsumo")]
        public virtual Insumo? Insumo { get; set; }

        [ForeignKey("IdLote")]
        public virtual Lote? Lote { get; set; }
    }
} 