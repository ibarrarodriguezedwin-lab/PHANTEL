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
        public string CodigoPostal { get; set; } = string.Empty;
        public string Experiencia { get; set; } = string.Empty;
        public decimal TarifaPorHora { get; set; }

        public double Latitud { get; set; }
        public double Longitud { get; set; }
        public double CalificacionPromedio { get; set; } = 0.0;
        public int TotalCalificaciones { get; set; } = 0;
        public double AjusteReputacion { get; set; } = 0.0;
        public string FotoPerfil { get; set; } = string.Empty;

        // --- PREFERENCIAS Y DISPONIBILIDAD ---
        public bool Disponible { get; set; } = true;
        public string Idioma { get; set; } = "es";

        // --- MODALIDAD DE ATENCIÓN (Técnico) ---
        public string Modalidad { get; set; } = "Domicilio";
        public string DireccionLocal { get; set; } = string.Empty;
        public string HorarioLocal { get; set; } = string.Empty;

        // --- CONTROL FINANCIERO, COMISIONES, AVISOS Y SUSPENSIÓN ---
        public string ClabeInterbancaria { get; set; } = string.Empty;
        public decimal SaldoEnCustodia { get; set; } = 0m;
        public decimal SaldoLiberado { get; set; } = 0m;
        public int ReportesAcumulados { get; set; } = 0;
        public bool CuentaSuspendida { get; set; } = false;
        public string MotivoSuspension { get; set; } = string.Empty;

        // --- AVISO DE CONDUCTA (VISIBLE EN PANTALLA PRINCIPAL DEL CLIENTE) ---
        public bool TieneAvisoConducta { get; set; } = false;
        public string MensajeAvisoConducta { get; set; } = string.Empty;
        public DateTime? FechaAvisoConducta { get; set; }

        // --- ACEPTACIÓN DE TÉRMINOS DE COMISIÓN ---
        public bool AceptoTerminosComision { get; set; } = false;
        public DateTime? FechaAceptacionTerminos { get; set; }

        // --- VERIFICACIÓN DE IDENTIDAD (KYC) ---
        public string EstadoVerificacion { get; set; } = "No Verificado";
        public string IneFrenteBase64 { get; set; } = string.Empty;
        public string IneReversoBase64 { get; set; } = string.Empty;
        public string MotivoRechazoVerificacion { get; set; } = string.Empty;

        // --- PROPIEDADES CALCULADAS ---
        [Ignore]
        [Newtonsoft.Json.JsonIgnore]
        public bool AtiendeDomicilio => Modalidad == "Domicilio" || Modalidad == "Ambos";

        [Ignore]
        [Newtonsoft.Json.JsonIgnore]
        public bool AtiendeLocal => Modalidad == "Local" || Modalidad == "Ambos";

        [Ignore]
        [Newtonsoft.Json.JsonIgnore]
        public string TextoModalidad => Modalidad switch
        {
            "Local" => "En local fijo",
            "Ambos" => "Local y domicilio",
            _ => "A domicilio"
        };

        [Ignore]
        [Newtonsoft.Json.JsonIgnore]
        public bool MostrarEstrellas => TotalCalificaciones >= 1;

        [Ignore]
        [Newtonsoft.Json.JsonIgnore]
        public bool MostrarNuevo => TotalCalificaciones < 3;

        [Ignore]
        [Newtonsoft.Json.JsonIgnore]
        public string TextoEstrellas => $"{CalificacionPromedio:F1} ★ ({TotalCalificaciones})";

        [Ignore]
        [Newtonsoft.Json.JsonIgnore]
        public string TextoNuevo => "Nuevo";

        [Ignore]
        [Newtonsoft.Json.JsonIgnore]
        public string TextoCalificacion => TotalCalificaciones == 0
            ? "Sin reseñas aún"
            : $"{CalificacionPromedio:F1} ★ ({TotalCalificaciones} {(TotalCalificaciones == 1 ? "reseña" : "reseñas")})";

        [Ignore]
        [Newtonsoft.Json.JsonIgnore]
        public double PuntajeRecomendacion
        {
            get
            {
                const double pesoPrevio = 5.0;
                const double promedioBase = 4.0;
                double v = TotalCalificaciones;
                double r = v > 0 ? CalificacionPromedio : promedioBase;
                return (v / (v + pesoPrevio)) * r + (pesoPrevio / (v + pesoPrevio)) * promedioBase;
            }
        }

        [Ignore]
        [Newtonsoft.Json.JsonIgnore]
        public bool EstaVerificado => EstadoVerificacion == "Verificado";

        [Ignore]
        [Newtonsoft.Json.JsonIgnore]
        public string InsigniaVerificacionTexto => EstadoVerificacion == "Verificado"
            ? "Técnico Verificado PHANTEL"
            : "Perfil sin verificar";
    }

    public class SolicitudCita
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public int ClienteId { get; set; }
        public int TecnicoId { get; set; }
        public string TecnicoEmail { get; set; } = string.Empty;
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

        public string Modalidad { get; set; } = "Domicilio";

        public bool CalificadaPorCliente { get; set; } = false;
        public bool CalificadaPorTecnico { get; set; } = false;

        public string ConceptoPresupuesto { get; set; } = string.Empty;
        public decimal MontoPresupuesto { get; set; } = 0m;
        public decimal PorcentajeComisionAplicado { get; set; } = 0.03m;
        public decimal MontoComisionPhantel { get; set; } = 0m;
        public decimal MontoNetoTecnico { get; set; } = 0m;

        public string EstadoPago { get; set; } = "Sin Cotizar";
        public string ReferenciaTransferencia { get; set; } = string.Empty;
        public string ComprobantePagoBase64 { get; set; } = string.Empty;
        public decimal PenalizacionAplicada { get; set; } = 0m;

        // --- PROPIEDADES PARA DISPUTAS Y REPORTE PRIVADO ---
        public bool EnDisputa { get; set; } = false;
        public bool ResueltaPorAdmin { get; set; } = false;
        public string MotivoReporte { get; set; } = string.Empty;
        public string DictamenAdmin { get; set; } = string.Empty;

        // Reporte privado enviado por el Técnico sobre el Cliente (solo visible Admin <-> Técnico)
        public bool ReportePrivadoTecnico { get; set; } = false;
        public string MotivoReporteTecnico { get; set; } = string.Empty;

        [Ignore]
        [Newtonsoft.Json.JsonIgnore]
        public bool EsEnLocal => Modalidad == "Local";

        [Ignore]
        [Newtonsoft.Json.JsonIgnore]
        public bool EsDomicilio => Modalidad != "Local";

        [Ignore]
        [Newtonsoft.Json.JsonIgnore]
        public bool TieneImagen => !string.IsNullOrWhiteSpace(EvidenciaRutaImagen);

        [Ignore]
        [Newtonsoft.Json.JsonIgnore]
        public bool TieneVideo => !string.IsNullOrWhiteSpace(EvidenciaRutaVideo);

        [Ignore]
        [Newtonsoft.Json.JsonIgnore]
        public string TextoUbicacion => EsEnLocal ? "Atención en tu local" : DireccionCliente;

        [Ignore]
        [Newtonsoft.Json.JsonIgnore]
        public decimal MontoTotalConTraslado => TarifaTraslado + MontoPresupuesto;

        // --- VISIBILIDAD DE BOTONES DEL CLIENTE ---
        [Ignore]
        [Newtonsoft.Json.JsonIgnore]
        public bool MostrarBotonCalificar => Estado == "Finalizada" && !CalificadaPorCliente && !ResueltaPorAdmin && !EnDisputa;

        [Ignore]
        [Newtonsoft.Json.JsonIgnore]
        public bool MostrarBotonReportar => (Estado == "Finalizada" || EstadoPago == "En Custodia (Retenido)") && !ResueltaPorAdmin && !EnDisputa;

        // --- VISIBILIDAD DE BOTONES DEL TÉCNICO ---
        // 1. Pendiente: solo Aceptar o Rechazar
        [Ignore]
        [Newtonsoft.Json.JsonIgnore]
        public bool TecnicoMostrarAceptar => Estado == "Pendiente";

        // 2. Aceptada: Finalizar trabajo, Chat con cliente y Ver ruta (si es a domicilio). Sin Eliminar.
        [Ignore]
        [Newtonsoft.Json.JsonIgnore]
        public bool TecnicoMostrarFinalizar => Estado == "Aceptada";

        [Ignore]
        [Newtonsoft.Json.JsonIgnore]
        public bool TecnicoMostrarChat => Estado == "Aceptada";

        [Ignore]
        [Newtonsoft.Json.JsonIgnore]
        public bool TecnicoMostrarRuta => Estado == "Aceptada" && EsDomicilio;

        // 3. Finalizada (o Rechazada): Eliminar y Reportar problema con el cliente (privado a Admin)
        [Ignore]
        [Newtonsoft.Json.JsonIgnore]
        public bool TecnicoMostrarEliminar => Estado == "Finalizada" || Estado == "Rechazada";

        [Ignore]
        [Newtonsoft.Json.JsonIgnore]
        public bool TecnicoMostrarReportarCliente => Estado == "Finalizada" && !ReportePrivadoTecnico;

        [Ignore]
        [Newtonsoft.Json.JsonIgnore]
        public bool TecnicoMostrarBadgeReportado => Estado == "Finalizada" && ReportePrivadoTecnico;
    }

    public class Calificacion
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public int SolicitudId { get; set; }
        public int TecnicoId { get; set; }
        public int Puntuacion { get; set; }
        public string Comentario { get; set; } = string.Empty;

        public string TecnicoEmail { get; set; } = string.Empty;
        public string NombreCliente { get; set; } = string.Empty;
        public DateTime? Fecha { get; set; }

        [Ignore]
        [Newtonsoft.Json.JsonIgnore]
        public string TextoEstrellas
        {
            get
            {
                int n = Math.Clamp(Puntuacion, 0, 5);
                return new string('★', n) + new string('☆', 5 - n);
            }
        }

        [Ignore]
        [Newtonsoft.Json.JsonIgnore]
        public string TextoAutor => string.IsNullOrWhiteSpace(NombreCliente) ? "Cliente" : NombreCliente;

        [Ignore]
        [Newtonsoft.Json.JsonIgnore]
        public string TextoFecha => Fecha.HasValue ? Fecha.Value.ToString("dd/MM/yyyy") : string.Empty;

        [Ignore]
        [Newtonsoft.Json.JsonIgnore]
        public string TextoComentario
        {
            get
            {
                string t = Comentario ?? string.Empty;
                int i = t.IndexOf("]:", StringComparison.Ordinal);
                if (t.StartsWith("[") && i >= 0) t = t.Substring(i + 2);
                t = t.Trim();
                return string.IsNullOrEmpty(t) ? "Sin comentario escrito." : t;
            }
        }
    }

    public class ReporteClienteItem
    {
        [Newtonsoft.Json.JsonIgnore]
        public string Key { get; set; } = string.Empty;
        public int SolicitudId { get; set; }
        public int ClienteId { get; set; }
        public int TecnicoId { get; set; }
        public string TecnicoNombre { get; set; } = string.Empty;
        public string TecnicoEmail { get; set; } = string.Empty;
        public string Motivo { get; set; } = string.Empty;
        public string Detalle { get; set; } = string.Empty;
        public DateTime Fecha { get; set; }
        public string EstadoRevision { get; set; } = "Pendiente";
    }
}