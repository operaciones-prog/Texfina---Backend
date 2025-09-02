using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TexfinaApi.Models
{
    [Table("INSUMO_PROVEEDOR")]
    public class InsumoProveedor
    {
        [Key]
        [Column("id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Column("id_insumo")]
        public int? IdInsumo { get; set; }

        [Column("id_proveedor")]
        public int? IdProveedor { get; set; }

        [Column("precio_unitario")]
        public float? PrecioUnitario { get; set; }

        // Propiedades de navegación
        [ForeignKey("IdInsumo")]
        public virtual Insumo? Insumo { get; set; }

        [ForeignKey("IdProveedor")]
        public virtual Proveedor? Proveedor { get; set; }

        public virtual ICollection<Ingreso> Ingresos { get; set; } = new List<Ingreso>();
    }
} 