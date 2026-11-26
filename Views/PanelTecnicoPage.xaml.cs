using Firebase.Database.Query;
using PHANTEL.Models;
using PHANTEL.Services;

namespace PHANTEL.Views
{
    public partial class PanelTecnicoPage : ContentPage
    {
        private readonly CloudDatabaseService _dbService = new();
        private const string WHATSAPP_ADMIN = "525582393553";

        public PanelTecnicoPage()
        {
            InitializeComponent();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            CargarDatosTecnico();
        }

        private async void CargarDatosTecnico()
        {
            var tecnicoActual = LoginPage.UsuarioActual;

            if (tecnicoActual != null)
            {
                var usuarioFresco = await _dbService.ObtenerUsuarioPorEmailAsync(tecnicoActual.Email);
                if (usuarioFresco != null)
                {
                    LoginPage.UsuarioActual = usuarioFresco;
                    tecnicoActual = usuarioFresco;

                    if (tecnicoActual.CuentaSuspendida)
                    {
                        Application.Current!.Windows[0].Page = new NavigationPage(new CuentaSuspendidaPage());
                        return;
                    }
                }

                lblBienvenida.Text = tecnicoActual.Nombre;

                string cpTexto = string.IsNullOrWhiteSpace(tecnicoActual.CodigoPostal) ? "Sin CP" : $"CP {tecnicoActual.CodigoPostal}";
                lblEspecialidad.Text = $"{tecnicoActual.Especialidad}, {tecnicoActual.TextoCalificacion}, {cpTexto}";

                ActualizarBotonVerificacionKyc();
                ActualizarEstadoDisponibilidad();

                if (!string.IsNullOrEmpty(tecnicoActual.FotoPerfil))
                {
                    try
                    {
                        string cleanBase64 = tecnicoActual.FotoPerfil.Contains(",")
                            ? tecnicoActual.FotoPerfil.Split(',')[1]
                            : tecnicoActual.FotoPerfil;

                        byte[] imageBytes = Convert.FromBase64String(cleanBase64.Trim());
                        btnPerfilTecnico.Source = ImageSource.FromStream(() => new MemoryStream(imageBytes));
                    }
                    catch
                    {
                        btnPerfilTecnico.Source = "dotnet_bot.png";
                    }
                }

                var citas = await _dbService.ObtenerSolicitudesPorUsuarioAsync(tecnicoActual.Id, esTecnico: true);
                listCitasTecnico.ItemsSource = citas;
            }
            else
            {
                await DisplayAlertAsync("Error de Sesión", "No se encontró un técnico activo. Inicia sesión de nuevo.", "OK");
                Application.Current!.Windows[0].Page = new NavigationPage(new LoginPage());
            }
        }

        private void ActualizarBotonVerificacionKyc()
        {
            var u = LoginPage.UsuarioActual;
            if (u == null || btnVerificacionKyc == null) return;

            switch (u.EstadoVerificacion)
            {
                case "Verificado":
                    btnVerificacionKyc.Text = "Identidad verificada";
                    btnVerificacionKyc.TextColor = Color.FromArgb("#34C759");
                    break;
                case "En Revisión":
                    btnVerificacionKyc.Text = "Verificación en revisión";
                    btnVerificacionKyc.TextColor = Color.FromArgb("#4DA3E0");
                    break;
                case "Rechazado":
                    btnVerificacionKyc.Text = "Verificación rechazada, sube tu INE de nuevo";
                    btnVerificacionKyc.TextColor = Color.FromArgb("#E5534B");
                    break;
                default:
                    btnVerificacionKyc.Text = "Verifica tu identidad para obtener el sello";
                    btnVerificacionKyc.TextColor = Color.FromArgb("#E0A030");
                    break;
            }
        }

        private async void OnAbrirVerificacionKycClicked(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new VerificacionTecnicoPage());
        }

        private async void OnPerfilTecnicoClicked(object sender, EventArgs e)
        {
            if (LoginPage.UsuarioActual == null) return;
            await Navigation.PushAsync(new ConfiguracionPage());
        }

        private void ActualizarEstadoDisponibilidad()
        {
            var u = LoginPage.UsuarioActual;
            if (u == null || lblDisponibilidad == null) return;

            lblDisponibilidad.Text = u.Disponible
                ? "Disponible para recibir trabajos"
                : "En pausa, los clientes no te ven";
            lblDisponibilidad.TextColor = u.Disponible
                ? Color.FromArgb("#34C759")
                : Color.FromArgb("#E0A030");
        }

        private async void OnDisponibilidadTapped(object? sender, TappedEventArgs e)
        {
            await Navigation.PushAsync(new ConfiguracionPage());
        }

        // --- 1. ACEPTAR CITA ---
        private void OnAceptarCitaTapped(object? sender, TappedEventArgs e)
        {
            if (e.Parameter is SolicitudCita cita)
                ProcesarAceptarCita(cita);
        }

        private void OnAceptarCitaClicked(object sender, EventArgs e)
        {
            if ((sender as Button)?.CommandParameter is SolicitudCita cita)
                ProcesarAceptarCita(cita);
        }

        private async void ProcesarAceptarCita(SolicitudCita cita)
        {
            await _dbService.ActualizarEstadoSolicitudAsync(cita.Id, "Aceptada");
            await DisplayAlertAsync("Cita Aceptada", "Ya puedes chatear con el cliente, ver la ruta y finalizar el servicio al terminar.", "OK");
            CargarDatosTecnico();
        }

        // --- 2. RECHAZAR CITA ---
        private void OnRechazarCitaTapped(object? sender, TappedEventArgs e)
        {
            if (e.Parameter is SolicitudCita cita)
                ProcesarRechazarCita(cita);
        }

        private void OnRechazarCitaClicked(object sender, EventArgs e)
        {
            if ((sender as Button)?.CommandParameter is SolicitudCita cita)
                ProcesarRechazarCita(cita);
        }

        private async void ProcesarRechazarCita(SolicitudCita cita)
        {
            bool confirmar = await DisplayAlertAsync("Rechazar Solicitud", "¿Deseas rechazar esta solicitud?", "Sí, rechazar", "Cancelar");
            if (confirmar)
            {
                await _dbService.ActualizarEstadoSolicitudAsync(cita.Id, "Rechazada");
                CargarDatosTecnico();
            }
        }

        // --- 3. FINALIZAR TRABAJO ---
        private void OnFinalizarTrabajoTapped(object? sender, TappedEventArgs e)
        {
            if (e.Parameter is SolicitudCita cita)
                ProcesarFinalizarTrabajo(cita);
        }

        private void OnFinalizarTrabajoClicked(object sender, EventArgs e)
        {
            if ((sender as Button)?.CommandParameter is SolicitudCita cita)
                ProcesarFinalizarTrabajo(cita);
        }

        private async void ProcesarFinalizarTrabajo(SolicitudCita cita)
        {
            if (cita.Estado != "Aceptada")
                return;

            bool confirmar = await DisplayAlertAsync("Finalizar Trabajo", "¿Confirmas que el servicio técnico ha concluido?", "Sí, finalizar", "Cancelar");
            if (confirmar)
            {
                cita.Estado = "Finalizada";
                await _dbService.ActualizarEstadoSolicitudAsync(cita.Id, "Finalizada");
                await DisplayAlertAsync("Trabajo Finalizado", "El servicio quedó finalizado. Ahora puedes eliminar la tarjeta o enviar un reporte privado si hubo algún problema con el cliente.", "OK");
                CargarDatosTecnico();
            }
        }

        // --- 4. REPORTAR PROBLEMA CON EL CLIENTE (PRIVADO ENTRE TÉCNICO Y ADMIN) ---
        private async void OnReportarClientePrivadoTapped(object? sender, TappedEventArgs e)
        {
            if (e.Parameter is not SolicitudCita cita || LoginPage.UsuarioActual == null) return;

            string motivo = await DisplayActionSheetAsync(
                "Reporte Privado a Administración (Conducta del Cliente)",
                "Cancelar",
                null,
                "Falta de respeto o agresión verbal",
                "Cliente se niega a liberar el pago tras trabajo concluido",
                "Dirección falsa o cliente ausente sin aviso",
                "Intento de fraude o solicitud fuera de la plataforma");

            if (string.IsNullOrEmpty(motivo) || motivo == "Cancelar")
                return;

            string detalle = await DisplayPromptAsync(
                "Detalle Confidencial para Admin",
                "Explica brevemente lo ocurrido. Este reporte es 100% confidencial y solo lo verá Administración PHANTEL:",
                accept: "Enviar Reporte Privado",
                cancel: "Cancelar",
                placeholder: "Describe los hechos...");

            if (string.IsNullOrWhiteSpace(detalle))
                return;

            bool guardado = await _dbService.EnviarReportePrivadoClienteAsync(
                solicitudId: cita.Id,
                clienteId: cita.ClienteId,
                tecnicoId: LoginPage.UsuarioActual.Id,
                tecnicoNombre: LoginPage.UsuarioActual.Nombre,
                tecnicoEmail: LoginPage.UsuarioActual.Email,
                motivo: motivo,
                detalle: detalle.Trim());

            if (guardado)
            {
                cita.ReportePrivadoTecnico = true;
                cita.MotivoReporteTecnico = $"[REPORTE TÉCNICO -> CLIENTE ID #{cita.ClienteId}] {motivo}: {detalle.Trim()} ({DateTime.Now:dd/MM/yyyy HH:mm})";
                CargarDatosTecnico();

                bool abrirCanalPrivado = await DisplayAlertAsync(
                    "Reporte Confidencial Registrado",
                    "Tu reporte fue enviado únicamente a Administración PHANTEL. El cliente no tiene acceso a este mensaje.\n\n¿Deseas abrir el canal privado con Administración por WhatsApp para adjuntar evidencia?",
                    "Abrir Canal Privado",
                    "Cerrar");

                if (abrirCanalPrivado)
                {
                    string textoWhats =
                        $"Hola Administración PHANTEL (Canal Privado Técnicos). Reporto conducta irregular de un cliente:\n\n" +
                        $"• Folio Cita: #{cita.Id}\n" +
                        $"• ID Cliente: #{cita.ClienteId}\n" +
                        $"• Técnico: {LoginPage.UsuarioActual.Nombre} ({LoginPage.UsuarioActual.Email})\n" +
                        $"• Motivo: {motivo}\n" +
                        $"• Detalle: {detalle.Trim()}";

                    string url = $"https://wa.me/{WHATSAPP_ADMIN}?text={Uri.EscapeDataString(textoWhats)}";
                    try { await Launcher.Default.OpenAsync(new Uri(url)); } catch { }
                }
            }
            else
            {
                await DisplayAlertAsync("Error", "No se pudo registrar el reporte privado en la nube. Verifica tu conexión.", "OK");
            }
        }
        // --- 5. ELIMINAR CITA (SOLO CUANDO YA FINALIZÓ O FUE RECHAZADA) ---
        private void OnEliminarCitaTapped(object? sender, TappedEventArgs e)
        {
            if (e.Parameter is SolicitudCita cita)
                ProcesarEliminarCita(cita);
        }

        private void OnEliminarCitaClicked(object sender, EventArgs e)
        {
            if ((sender as Button)?.CommandParameter is SolicitudCita cita)
                ProcesarEliminarCita(cita);
        }

        private async void ProcesarEliminarCita(SolicitudCita cita)
        {
            if (cita.Estado == "Aceptada")
            {
                await DisplayAlertAsync("Acción no permitida", "No puedes eliminar una cita en curso. Debes finalizar el trabajo primero.", "OK");
                return;
            }

            bool confirmar = await DisplayAlertAsync("Eliminar Cita", "¿Deseas quitar esta solicitud finalizada de tu historial?", "Sí, eliminar", "Cancelar");
            if (confirmar)
            {
                await _dbService.EliminarSolicitudAsync(cita.Id);
                await DisplayAlertAsync("Eliminada", "La cita ha sido eliminada de tu panel.", "OK");
                CargarDatosTecnico();
            }
        }

        // --- 6. CHAT CON EL CLIENTE ---
        private void OnAbrirChatTapped(object? sender, TappedEventArgs e)
        {
            if (e.Parameter is SolicitudCita cita)
                ProcesarAbrirChat(cita);
        }

        private void OnAbrirChatClicked(object sender, EventArgs e)
        {
            if ((sender as Button)?.CommandParameter is SolicitudCita cita)
                ProcesarAbrirChat(cita);
        }

        private async void ProcesarAbrirChat(SolicitudCita cita)
        {
            if (cita.Estado != "Aceptada")
                return;

            await Navigation.PushAsync(new ChatPage(cita));
        }

        // --- 7. VER RUTA EN MAPS ---
        private void OnAbrirGoogleMapsWebTapped(object? sender, TappedEventArgs e)
        {
            if (e.Parameter is SolicitudCita cita)
                ProcesarAbrirGoogleMaps(cita);
        }

        private void OnAbrirGoogleMapsWebClicked(object sender, EventArgs e)
        {
            if ((sender as Button)?.CommandParameter is SolicitudCita cita)
                ProcesarAbrirGoogleMaps(cita);
        }

        private async void ProcesarAbrirGoogleMaps(SolicitudCita cita)
        {
            if (cita.Estado != "Aceptada")
                return;

            try
            {
                string destino = !string.IsNullOrWhiteSpace(cita.DireccionCliente) && cita.DireccionCliente != "Ubicación por coordenadas GPS"
                    ? Uri.EscapeDataString(cita.DireccionCliente)
                    : "19.3452,-99.1384";

                string urlGoogleMaps = $"https://www.google.com/maps/dir/?api=1&destination={destino}&travelmode=driving";
                await Launcher.OpenAsync(new Uri(urlGoogleMaps));
            }
            catch (Exception ex)
            {
                await DisplayAlertAsync("Error de Navegación", $"No se pudo abrir Google Maps: {ex.Message}", "OK");
            }
        }
    }
}