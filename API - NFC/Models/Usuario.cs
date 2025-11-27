using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace API___NFC.Models
{
    public class Usuario
    {
        [Key]
        public int IdUsuario { get; set; }

        [Required, MaxLength(100)]
        public string Nombre { get; set; }

        [Required, MaxLength(100)]
        public string Apellido { get; set; }

        [Required, MaxLength(5)]
        public string TipoDocumento { get; set; }

        [Required, MaxLength(20)]
        public string NumeroDocumento { get; set; }

        [Required, MaxLength(150)]
        public string Correo { get; set; }

       
        [Required, MaxLength(255)]
        public string Contraseña { get; set; }

        [Required, MaxLength(20)]
        public string Rol { get; set; }

        [ MaxLength(100)]
        public string? CodigoBarras { get; set; } = null;

        [MaxLength(100)]
        public string? Cargo { get; set; } 

        [MaxLength(20)]
        public string? Telefono { get; set; } 

        [MaxLength(255)]
        public string? FotoUrl { get; set; } 
        public bool? Estado { get; set; }
        [JsonIgnore]
        public string? TokenRecuperacion { get; set; }    // Guarda el token temporal para recuperar contraseña
        [JsonIgnore]
        public DateTime? FechaTokenExpira { get; set; }   // Guarda la fecha de expiración del token


        public DateTime? FechaCreacion { get; set; }

        public DateTime? FechaActualizacion { get; set; }
    }
}