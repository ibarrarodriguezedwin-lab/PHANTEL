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
        public string TipoPerfil { get; set; } = string.Empty; // "Cliente", "Técnico" o "Admin"
        public string Especialidad { get; set; } = string.Empty;

        public string Telefono { get; set; } = string.Empty;
        public string Direccion { get; set; } = string.Empty;
        public string Experiencia { get; set; } = string.Empty;
        public decimal TarifaPorHora { get; set; }

        public double Latitud { get; set; }
        public double Longitud { get; set; }
        public double CalificacionPromedio { get; set; } = 5.0;
        public string FotoPerfil { get; set; } = string.Empty;

        // --- CONTROL FINANCIERO Y REPUTACIÓN (CUSTODIA / PENALIZACIONES) ---
        public string ClabeInterbancaria { get; set; } = string.Empty;
        public decimal SaldoEnCustodia { get; set; } = 0m;
        public decimal SaldoLiberado { get; set; } = 0m;
        public int ReportesAcumulados { get; set; } = 0;
        public bool CuentaSuspendida { get; set; } = false;
        public string MotivoSuspension { get; set; } = string.Empty;


        // --- VERIFICACIÓN DE IDENTIDAD (KYC / TÉCNICO VERIFICADO) ---
        public string EstadoVerificacion { get; set; } = "No Verificado";
        // Valores: "No Verificado", "En Revisión", "Verificado", "Rechazado"
        public string IneFrenteBase64 { get; set; } = string.Empty;
        public string IneReversoBase64 { get; set; } = string.Empty;
        public string MotivoRechazoVerificacion { get; set; } = string.Empty;

        [Ignore]
        [Newtonsoft.Json.JsonIgnore]
        public bool EstaVerificado => EstadoVerificacion == "Verificado";

        [Ignore]
        [Newtonsoft.Json.JsonIgnore]
        public string InsigniaVerificacionTexto => EstadoVerificacion == "Verificado"
            ? "✔️ Técnico Verificado PHANTEL"
            : "⚠️ Perfil sin verificar";
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

        // --- CONTROL DE CALIFICACIONES ---
        public bool CalificadaPorCliente { get; set; } = false;
        public bool CalificadaPorTecnico { get; set; } = false;

        // --- PRESUPUESTO Y PAGO PROTEGIDO (ESTILO MERCADO LIBRE) ---
        public string ConceptoPresupuesto { get; set; } = string.Empty;
        public decimal MontoPresupuesto { get; set; } = 0m;
        public string EstadoPago { get; set; } = "Sin Cotizar";
        // Valores: "Sin Cotizar", "Cotizado", "En Custodia (Retenido)", "Liberado al Técnico", "Retenido por Disputa", "Fondos Liquidados (Cerrado)", "Reembolsado al Cliente (Cerrado)"
        public string ReferenciaTransferencia { get; set; } = string.Empty;
        public string ComprobantePagoBase64 { get; set; } = string.Empty;
        public decimal PenalizacionAplicada { get; set; } = 0m;

        // --- PROPIEDADES PARA DISPUTAS Y REEMBOLSOS ---
        public bool EnDisputa { get; set; } = false;
        public bool ResueltaPorAdmin { get; set; } = false;
        public string MotivoReporte { get; set; } = string.Empty;
        public string DictamenAdmin { get; set; } = string.Empty;

        [Ignore]
        [Newtonsoft.Json.JsonIgnore]
        public decimal MontoTotalConTraslado => TarifaTraslado + MontoPresupuesto;

        [Ignore]
        [Newtonsoft.Json.JsonIgnore]
        public bool MostrarBotonCalificar => Estado == "Finalizada" && !CalificadaPorCliente && !ResueltaPorAdmin && !EnDisputa;

        [Ignore]
        [Newtonsoft.Json.JsonIgnore]
        public bool MostrarBotonReportar => (Estado == "Finalizada" || EstadoPago == "En Custodia (Retenido)") && !ResueltaPorAdmin && !EnDisputa;
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