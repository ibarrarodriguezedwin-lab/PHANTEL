using PHANTEL.Models;
using PHANTEL.Services;

namespace PHANTEL.Views
{
    public partial class MisSolicitudesPage : ContentPage
    {
        private readonly CloudDatabaseService _dbService = new();
        private static readonly HashSet<int> _citasCalificadasPorCliente = new();

        public MisSolicitudesPage()
        {
            InitializeComponent();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            CargarSolicitudes();
        }

        private async void CargarSolicitudes()
        {
            if (LoginPage.UsuarioActual == null)
                return;

            var solicitudes = await _dbService.ObtenerSolicitudesPorUsuarioAsync(
                usuarioId: LoginPage.UsuarioActual.Id,
                esTecnico: false);

            listSolicitudes.ItemsSource = solicitudes;

            // DETECCIÓN AUTOMÁTICA: Si el técnico finalizó el trabajo, pedirle al cliente calificar al técnico
            var citaRecienFinalizada = solicitudes.FirstOrDefault(c => c.Estado == "Finalizada" && !_citasCalificadasPorCliente.Contains(c.Id));
            if (citaRecienFinalizada != null)
            {
                _citasCalificadasPorCliente.Add(citaRecienFinalizada.Id);
                await AbrirModalCalificarTecnicoAsync(citaRecienFinalizada);
            }
        }

        // 1. FINALIZAR TRABAJO Y CALIFICAR AL TÉCNICO
        private async void OnFinalizarTrabajoClienteClicked(object sender, EventArgs e)
        {
            var button = sender as Button;
            if (button?.CommandParameter is SolicitudCita cita)
            {
                if (cita.Estado != "Aceptada" && cita.Estado != "Finalizada")
                {
                    await DisplayAlertAsync("Atención", "El técnico debe aceptar la solicitud antes de poder finalizar el trabajo.", "OK");
                    return;
                }

                // Marca como Finalizada en Firebase (esto también activará la calificación del técnico)
                await _dbService.ActualizarEstadoSolicitudAsync(cita.Id, "Finalizada");
                _citasCalificadasPorCliente.Add(cita.Id);

                await AbrirModalCalificarTecnicoAsync(cita);
            }
        }

        private async Task AbrirModalCalificarTecnicoAsync(SolicitudCita cita)
        {
            var popup = new CalificacionPopup("Cliente");
            await Navigation.PushModalAsync(popup);

            popup.Disappearing += async (s, args) =>
            {
                if (popup.FueConfirmado)
                {
                    var nuevaCalificacion = new Calificacion
                    {
                        SolicitudId = cita.Id,
                        TecnicoId = cita.TecnicoId,
                        Puntuacion = popup.PuntuacionResultado,
                        Comentario = $"[Cliente evaluó al Técnico]: {popup.ComentarioResultado}"
                    };

                    await _dbService.GuardarCalificacionAsync(nuevaCalificacion);
                    await DisplayAlertAsync("¡Gracias!", $"Has calificado el servicio del técnico con {popup.PuntuacionResultado} ★.", "OK");
                }
                CargarSolicitudes();
            };
        }

        // 2. ELIMINAR CITA (Botón totalmente independiente)
        private async void OnEliminarCitaClicked(object sender, EventArgs e)
        {
            var button = sender as Button;
            if (button?.CommandParameter is SolicitudCita cita)
            {
                bool confirmar = await DisplayAlertAsync("⚠️ Eliminar Cita", "¿Deseas eliminar esta solicitud de tus citas?", "Sí, eliminar", "Cancelar");
                if (confirmar)
                {
                    await _dbService.EliminarSolicitudAsync(cita.Id);
                    await DisplayAlertAsync("Eliminada", "La cita ha sido eliminada.", "OK");
                    CargarSolicitudes();
                }
            }
        }

        // 3. CHAT DIRECTO CON EL TÉCNICO
        private async void OnAbrirChatClicked(object sender, EventArgs e)
        {
            var button = sender as Button;
            if (button?.CommandParameter is SolicitudCita cita)
            {
                if (cita.Estado != "Aceptada" && cita.Estado != "Finalizada")
                {
                    await DisplayAlertAsync("Atención", "El técnico debe aceptar la cita primero para iniciar la conversación por chat.", "OK");
                    return;
                }

                await Navigation.PushAsync(new ChatPage(cita));
            }
        }

        // 4. REPORTAR PROBLEMA / REEMBOLSO (WHATSAPP O LLAMADA A ADMINISTRACIÓN)
        private async void OnReportarProblemaClicked(object sender, EventArgs e)
        {
            var button = sender as Button;
            if (button?.CommandParameter is not SolicitudCita cita) return;

            // Números de soporte de los administradores PHANTEL (Formato internacional con 52 para México)
            string telefonoWhatsApp = "525582393553"; // <-- Reemplaza con tu número real de administrador
            string telefonoLlamada = "+525636511034"; // <-- Reemplaza con tu número real de administrador

            string opcion = await DisplayActionSheetAsync(
                $"Soporte y Reembolsos - Cita #{cita.Id}",
                "Cancelar",
                null,
                "💬 Mensaje Directo por WhatsApp",
                "📞 Llamar a un Administrador");

            if (string.IsNullOrEmpty(opcion) || opcion == "Cancelar")
                return;

            var cliente = LoginPage.UsuarioActual;
            string nombreCliente = cliente?.Nombre ?? "Cliente";

            if (opcion == "💬 Mensaje Directo por WhatsApp")
            {
                await _dbService.MarcarCitaEnDisputaAsync(cita.Id, "WhatsApp");

                string mensaje = $"Hola equipo de Administración PHANTEL. Quiero reportar un problema con un servicio finalizado para revisión y posible reembolso:\n\n" +
                                 $"• Folio de Cita: #{cita.Id}\n" +
                                 $"• Cliente: {nombreCliente}\n" +
                                 $"• Técnico asignado: {cita.TecnicoEmail}\n" +
                                 $"• Falla reportada: {cita.DescripcionFalla}\n" +
                                 $"• Monto pagado: ${cita.TarifaTraslado:F2} MXN\n\n" +
                                 $"Detalle de mi problema: ";

                string urlWhatsApp = $"https://wa.me/{telefonoWhatsApp}?text={Uri.EscapeDataString(mensaje)}";

                try
                {
                    await Launcher.Default.OpenAsync(new Uri(urlWhatsApp));
                }
                catch (Exception ex)
                {
                    await DisplayAlertAsync("Error", $"No se pudo abrir WhatsApp en este dispositivo: {ex.Message}", "OK");
                }
            }
            else if (opcion == "📞 Llamar a un Administrador")
            {
                await _dbService.MarcarCitaEnDisputaAsync(cita.Id, "Llamada Telefónica");

                try
                {
                    if (PhoneDialer.Default.IsSupported)
                    {
                        PhoneDialer.Default.Open(telefonoLlamada);
                    }
                    else
                    {
                        await Launcher.Default.OpenAsync(new Uri($"tel:{telefonoLlamada}"));
                    }
                }
                catch (Exception ex)
                {
                    await DisplayAlertAsync("Contacto Directo", $"Marca directamente al número de soporte: {telefonoLlamada}\n({ex.Message})", "OK");
                }
            }
        }
    }
}