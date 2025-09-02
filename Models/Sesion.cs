using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TexfinaApi.Models
{
    [Table("SESION")]
    public class Sesion
    {
        [Key]
        [Column("id_sesion")]
        public Guid IdSesion { get; set; } = Guid.NewGuid();

        [Column("id_usuario")]
        public int? IdUsuario { get; set; }

        [Column("inicio")]
        public DateTime? Inicio { get; set; } = DateTime.UtcNow;

        [Column("fin")]
        public DateTime? Fin { get; set; }

        [Column("cerrada_automaticamente")]
        public bool? CerradaAutomaticamente { get; set; }

        // Propiedades de navegación
        [ForeignKey("IdUsuario")]
        public virtual Usuario? Usuario { get; set; }
    }
} 