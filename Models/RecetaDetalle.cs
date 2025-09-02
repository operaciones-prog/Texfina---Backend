using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TexfinaApi.Models
{
    [Table("RECETA_DETALLE")]
    public class RecetaDetalle
    {
        [Key]
        [Column("id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Column("id_receta")]
        public int? IdReceta { get; set; }

        [Column("id_insumo")]
        public int? IdInsumo { get; set; }

        [Column("proporcion")]
        public float? Proporcion { get; set; }

        [Column("orden")]
        public int? Orden { get; set; }

        [MaxLength(50)]
        [Column("tipo_medida")]
        public string? TipoMedida { get; set; }

        // Propiedades de navegación
        [ForeignKey("IdReceta")]
        public virtual Receta? Receta { get; set; }

        [ForeignKey("IdInsumo")]
        public virtual Insumo? Insumo { get; set; }
    }
} 