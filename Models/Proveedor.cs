using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TexfinaApi.Models
{
    [Table("PROVEEDOR")]
    public class Proveedor
    {
        [Key]
        [Column("id_proveedor")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int IdProveedor { get; set; }

        [MaxLength(200)]
        [Column("empresa")]
        public string? Empresa { get; set; }

        [MaxLength(20)]
        [Column("ruc")]
        public string? Ruc { get; set; }

        [MaxLength(100)]
        [Column("contacto")]
        public string? Contacto { get; set; }

        [MaxLength(255)]
        [Column("direccion")]
        public string? Direccion { get; set; }

        [Column("created_at")]
        public DateTime? CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        // Propiedades de navegación
        public virtual ICollection<InsumoProveedor> InsumoProveedores { get; set; } = new List<InsumoProveedor>();
    }
} 