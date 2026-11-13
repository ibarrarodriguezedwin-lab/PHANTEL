using SQLite;

namespace PHANTEL.Models
{
    public class Usuario
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string TipoPerfil { get; set; } = string.Empty; // "Cliente" o "Técnico"
        public string Especialidad { get; set; } = string.Empty;

        public string Telefono { get; set; } = string.Empty;
        public string Direccion { get; set; } = string.Empty;
        public string Experiencia { get; set; } = string.Empty;
        public decimal TarifaPorHora { get; set; }

        public double Latitud { get; set; }
        public double Longitud { get; set; }
        public double CalificacionPromedio { get; set; } = 5.0;
        public string FotoPerfil { get; set; } = string.Empty;
    }

    public class SolicitudCita
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public int ClienteId { get; set; }
        public int TecnicoId { get; set; }
        public string TecnicoEmail { get; set; } = string.Empty; // <--- Identificador único del técnico en la nube
        public string DescripcionFalla { get; set; } = string.Empty;
        public string DireccionCliente { get; set; } = string.Empty;
        public DateTime FechaHoraProgramada { get; set; } = DateTime.Now;
        public double DistanciaKm { get; set; }
        public decimal TarifaTraslado { get; set; }
        public string Estado { get; set; } = "Pendiente";
        public DateTime FechaCreacion { get; set; } = DateTime.Now;
        public string EvidenciaRutaImagen { get; set; } = string.Empty;
        public string EvidenciaRutaVideo { get; set; } = string.Empty;
        public string EvidenciaRutaAudio { get; set; } = string.Empty;
    }

    public class Calificacion
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public int SolicitudId { get; set; }
        public int TecnicoId { get; set; }
        public int Puntuacion { get; set; }
        public string Comentario { get; set; } = string.Empty;
    }
}