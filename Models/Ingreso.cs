using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TexfinaApi.Models
{
    [Table("INGRESO")]
    public class Ingreso
    {
        [Key]
        [Column("id_ingreso")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int IdIngreso { get; set; }

        [Column("id_insumo")]
        public int? IdInsumo { get; set; }

        [Column("id_insumo_proveedor")]
        public int? IdInsumoProveedor { get; set; }

        [Column("fecha")]
        public DateOnly? Fecha { get; set; }

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

        [Column("precio_total_formula")]
        public float? PrecioTotalFormula { get; set; }

        [Column("precio_unitario_historico")]
        public float? PrecioUnitarioHistorico { get; set; }

        [MaxLength(50)]
        [Column("numero_remision")]
        public string? NumeroRemision { get; set; }

        [MaxLength(50)]
        [Column("orden_compra")]
        public string? OrdenCompra { get; set; }

        [MaxLength(50)]
        [Column("estado")]
        public string? Estado { get; set; }

        // Propiedades de navegación
        [ForeignKey("IdInsumo")]
        public virtual Insumo? Insumo { get; set; }

        [ForeignKey("IdInsumoProveedor")]
        public virtual InsumoProveedor? InsumoProveedor { get; set; }

        [ForeignKey("IdUnidad")]
        public virtual Unidad? Unidad { get; set; }

        [ForeignKey("IdLote")]
        public virtual Lote? Lote { get; set; }
    }
} 