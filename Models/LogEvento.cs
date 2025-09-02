using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TexfinaApi.Models
{
    [Table("LOG_EVENTO")]
    public class LogEvento
    {
        [Key]
        [Column("id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Column("id_usuario")]
        public int? IdUsuario { get; set; }

        [MaxLength(100)]
        [Column("accion")]
        public string? Accion { get; set; }

        [MaxLength(500)]
        [Column("descripcion")]
        public string? Descripcion { get; set; }

        [MaxLength(45)]
        [Column("ip_origen")]
        public string? IpOrigen { get; set; }

        [MaxLength(100)]
        [Column("modulo")]
        public string? Modulo { get; set; }

        [MaxLength(100)]
        [Column("tabla_afectada")]
        public string? TablaAfectada { get; set; }

        [Column("timestamp")]
        public DateTime? Timestamp { get; set; } = DateTime.UtcNow;

        // Propiedades de navegación
        [ForeignKey("IdUsuario")]
        public virtual Usuario? Usuario { get; set; }
    }
} 