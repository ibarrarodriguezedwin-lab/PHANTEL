using PHANTEL.Models;
using PHANTEL.Services;

namespace PHANTEL.Views
{
    public partial class MisSolicitudesPage : ContentPage
    {
        private readonly CloudDatabaseService _dbService = new();
        private bool _popupAbierto = false;

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
        }

        // 1. FINALIZAR TRABAJO, CALIFICAR AL TÉCNICO Y LIBERAR PAGO EN AUTOMÁTICO
        private async void OnFinalizarTrabajoClienteClicked(object sender, EventArgs e)
        {
            var button = sender as Button;
            if (button?.CommandParameter is SolicitudCita cita)
            {
                if (cita.ResueltaPorAdmin || cita.EnDisputa)
                {
                    await DisplayAlertAsync("Caso cerrado / en revisión", "Esta cita ya fue gestionada por Administración PHANTEL y no admite calificación manual.", "OK");
                    return;
                }

                if (cita.Estado != "Aceptada" && cita.Estado != "Finalizada")
                {
                    await DisplayAlertAsync("Atención", "El técnico debe aceptar la solicitud antes de poder finalizar el trabajo.", "OK");
                    return;
                }

                if (cita.CalificadaPorCliente)
                {
                    await DisplayAlertAsync("Aviso", "Este servicio ya fue finalizado y calificado.", "OK");
                    return;
                }

                await AbrirModalCalificarTecnicoAsync(cita);
            }
        }

        private async Task AbrirModalCalificarTecnicoAsync(SolicitudCita cita)
        {
            if (_popupAbierto) return;
            _popupAbierto = true;

            var popup = new CalificacionPopup("Cliente");
            await Navigation.PushModalAsync(popup);

            popup.Disappearing += async (s, args) =>
            {
                _popupAbierto = false;
                if (popup.FueConfirmado)
                {
                    cita.Estado = "Finalizada";
                    cita.CalificadaPorCliente = true;

                    await _dbService.ActualizarEstadoSolicitudAsync(cita.Id, "Finalizada");
                    await _dbService.MarcarCitaCalificadaAsync(cita.Id, esTecnico: false);

                    var nuevaCalificacion = new Calificacion
                    {
                        SolicitudId = cita.Id,
                        TecnicoId = cita.TecnicoId,
                        Puntuacion = popup.PuntuacionResultado,
                        Comentario = $"[Cliente evaluó al Técnico]: {popup.ComentarioResultado}"
                    };

                    await _dbService.GuardarCalificacionAsync(nuevaCalificacion);

                    // Si había pago en custodia y no existe disputa ni resolución de admin, liberar dinero automáticamente al técnico
                    bool pagoLiberado = false;
                    if (!cita.EnDisputa && !cita.ResueltaPorAdmin && cita.EstadoPago == "En Custodia (Retenido)")
                    {
                        pagoLiberado = await _dbService.LiberarPagoATecnicoAsync(cita.Id);
                        if (pagoLiberado)
                        {
                            cita.EstadoPago = "Liberado al Técnico";
                            await _dbService.EnviarMensajeChatAsync(new MensajeChat
                            {
                                SolicitudId = cita.Id,
                                EmisorId = LoginPage.UsuarioActual?.Id ?? 0,
                                EmisorNombre = "🛡️ SISTEMA PHANTEL",
                                Texto = $"✅ SERVICIO CALIFICADO ({popup.PuntuacionResultado} ★) Y PAGO LIBERADO: Se liberaron automáticamente ${cita.MontoTotalConTraslado:F2} MXN al técnico.",
                                FechaHora = DateTime.Now,
                                EsTecnico = false
                            });
                        }
                    }

                    string msgConfirmacion = pagoLiberado
                        ? $"Has calificado al técnico con {popup.PuntuacionResultado} ★ y se liberaron automáticamente ${cita.MontoTotalConTraslado:F2} MXN a su cuenta."
                        : $"Has calificado el servicio del técnico con {popup.PuntuacionResultado} ★.";

                    await DisplayAlertAsync("¡Gracias!", msgConfirmacion, "OK");
                }
                CargarSolicitudes();
            };
        }

        // 2. ELIMINAR CITA
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

            if (cita.ResueltaPorAdmin || cita.EnDisputa)
            {
                await DisplayAlertAsync("Aviso", "Esta cita ya cuenta con un reporte abierto o dictamen final de Administración.", "OK");
                return;
            }

            string telefonoWhatsApp = "525582393553";
            string telefonoLlamada = "5636511034";

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
                CargarSolicitudes();

                string mensaje = $"Hola equipo de Administración PHANTEL. Quiero reportar un problema con un servicio para revisión y posible reembolso:\n\n" +
                                 $"• Folio de Cita: #{cita.Id}\n" +
                                 $"• Cliente: {nombreCliente}\n" +
                                 $"• Técnico asignado: {cita.TecnicoEmail}\n" +
                                 $"• Falla reportada: {cita.DescripcionFalla}\n" +
                                 $"• Monto total: ${cita.MontoTotalConTraslado:F2} MXN\n\n" +
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
                CargarSolicitudes();

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