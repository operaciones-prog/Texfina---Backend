using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TexfinaApi.Models
{
    [Table("INSUMO")]
    public class Insumo
    {
        [Key]
        [Column("id_insumo")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int IdInsumo { get; set; }

        [MaxLength(50)]
        [Column("id_fox")]
        public string? IdFox { get; set; }

        [MaxLength(200)]
        [Column("nombre")]
        public string? Nombre { get; set; }

        [MaxLength(50)]
        [Column("id_clase")]
        public string? IdClase { get; set; }

        [Column("peso_unitario")]
        public float? PesoUnitario { get; set; }

        [MaxLength(50)]
        [Column("id_unidad")]
        public string? IdUnidad { get; set; }

        [MaxLength(100)]
        [Column("presentacion")]
        public string? Presentacion { get; set; }

        [Column("precio_unitario")]
        public float? PrecioUnitario { get; set; }

        [Column("created_at")]
        public DateTime? CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        // Propiedades de navegación
        [ForeignKey("IdClase")]
        public virtual Clase? Clase { get; set; }

        [ForeignKey("IdUnidad")]
        public virtual Unidad? Unidad { get; set; }

        public virtual ICollection<InsumoProveedor> InsumoProveedores { get; set; } = new List<InsumoProveedor>();
        public virtual ICollection<Lote> Lotes { get; set; } = new List<Lote>();
        public virtual ICollection<Stock> Stocks { get; set; } = new List<Stock>();
        public virtual ICollection<Ingreso> Ingresos { get; set; } = new List<Ingreso>();
        public virtual ICollection<Consumo> Consumos { get; set; } = new List<Consumo>();
        public virtual ICollection<RecetaDetalle> RecetaDetalles { get; set; } = new List<RecetaDetalle>();
    }
} 