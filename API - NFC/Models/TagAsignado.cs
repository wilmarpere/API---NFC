using System;
using System.ComponentModel.DataAnnotations;

namespace API___NFC.Models
{
    public class TagAsignado
    {
        [Key]
        public int IdTag { get; set; }

        [Required, MaxLength(100)]
        public string CodigoTag { get; set; } = string.Empty;

        [Required]
        public int IdPersona { get; set; }

        [Required, MaxLength(20)]
        public string TipoPersona { get; set; } = "Usuario"; // o "Aprendiz"

        public DateTime FechaAsignacion { get; set; } = DateTime.Now;
    }
}
