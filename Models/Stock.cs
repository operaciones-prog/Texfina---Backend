using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TexfinaApi.Models
{
    [Table("STOCK")]
    public class Stock
    {
        [Key]
        [Column("id_stock")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int IdStock { get; set; }

        [Column("id_insumo")]
        public int? IdInsumo { get; set; }

        [MaxLength(100)]
        [Column("presentacion")]
        public string? Presentacion { get; set; }

        [MaxLength(50)]
        [Column("id_unidad")]
        public string? IdUnidad { get; set; }

        [Column("cantidad")]
        public float? Cantidad { get; set; }

        [Column("id_lote")]
        public int? IdLote { get; set; }

        [Column("id_almacen")]
        public int? IdAlmacen { get; set; }

        [Column("fecha_entrada")]
        public DateTime? FechaEntrada { get; set; }

        [Column("fecha_salida")]
        public DateTime? FechaSalida { get; set; }

        // Propiedades de navegación
        [ForeignKey("IdInsumo")]
        public virtual Insumo? Insumo { get; set; }

        [ForeignKey("IdUnidad")]
        public virtual Unidad? Unidad { get; set; }

        [ForeignKey("IdLote")]
        public virtual Lote? Lote { get; set; }

        [ForeignKey("IdAlmacen")]
        public virtual Almacen? Almacen { get; set; }
    }
} 