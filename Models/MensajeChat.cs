namespace PHANTEL.Models
{
    public class MensajeChat
    {
        public string Id { get; set; } = string.Empty;
        public int SolicitudId { get; set; }     // ID de la cita a la que pertenece el chat
        public int EmisorId { get; set; }        // ID del que envía el mensaje
        public string EmisorNombre { get; set; } = string.Empty;
        public string Texto { get; set; } = string.Empty;
        public DateTime FechaHora { get; set; } = DateTime.Now;
        public bool EsTecnico { get; set; }      // True si lo envió el técnico, False si fue el cliente
    }
}