using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TexfinaApi.Models
{
    [Table("LOTE")]
    public class Lote
    {
        [Key]
        [Column("id_lote")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int IdLote { get; set; }

        [Column("id_insumo")]
        public int? IdInsumo { get; set; }

        [MaxLength(50)]
        [Column("lote")]
        public string? Numero { get; set; }

        [MaxLength(100)]
        [Column("ubicacion")]
        public string? Ubicacion { get; set; }

        [Column("stock_inicial")]
        public float? StockInicial { get; set; }

        [Column("stock_actual")]
        public float? StockActual { get; set; }

        [Column("fecha_expiracion")]
        public DateOnly? FechaExpiracion { get; set; }

        [Column("precio_total")]
        public float? PrecioTotal { get; set; }

        [MaxLength(50)]
        [Column("estado_lote")]
        public string? EstadoLote { get; set; }

        // PROPIEDADES DE NAVEGACIÓN RESTAURADAS
        [ForeignKey("IdInsumo")]
        public virtual Insumo? Insumo { get; set; }

        public virtual ICollection<Stock> Stocks { get; set; } = new List<Stock>();
        public virtual ICollection<Ingreso> Ingresos { get; set; } = new List<Ingreso>();
        public virtual ICollection<Consumo> Consumos { get; set; } = new List<Consumo>();
    }
} 