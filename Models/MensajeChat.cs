namespace PHANTEL.Models
{
    public class MensajeChat
    {
        public string Id { get; set; } = string.Empty;
        public int SolicitudId { get; set; }
        public int EmisorId { get; set; }
        public string EmisorNombre { get; set; } = string.Empty;
        public string Texto { get; set; } = string.Empty;
        public DateTime FechaHora { get; set; } = DateTime.Now;
        public bool EsTecnico { get; set; }

        // Multimedia en el chat
        public string ImagenBase64 { get; set; } = string.Empty;
        public string VideoBase64 { get; set; } = string.Empty;
        public string NombreArchivoVideo { get; set; } = string.Empty;

        // Confirmaciones de entrega y lectura
        public bool Entregado { get; set; } = true;
        public bool Leido { get; set; } = false;
    }
}