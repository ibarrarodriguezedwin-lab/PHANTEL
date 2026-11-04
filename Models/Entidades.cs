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
        public string Especialidad { get; set; } = string.Empty; // Plomería, Electricidad, Soporte de Cómputo, Cerrajería
        public double Latitud { get; set; }
        public double Longitud { get; set; }
        public double CalificacionPromedio { get; set; } = 5.0;
        // En Models/Entidades.cs
        public string FotoPerfil { get; set; } = string.Empty; // Dejar vacío por defecto si no hay archivo en Resources/Images
    }

    public class SolicitudCita
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public int ClienteId { get; set; }
        public int TecnicoId { get; set; }
        public string DescripcionFalla { get; set; } = string.Empty;
        public string DireccionCliente { get; set; } = string.Empty; // <-- Dirección/Referencia del domicilio
        public DateTime FechaHoraProgramada { get; set; } = DateTime.Now; // <-- Fecha y Hora seleccionada
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
        public int Puntuacion { get; set; } // 1 a 5
        public string Comentario { get; set; } = string.Empty;
    }
}