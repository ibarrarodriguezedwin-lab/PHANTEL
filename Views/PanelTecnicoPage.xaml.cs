using PHANTEL.Models;
using PHANTEL.Services;

namespace PHANTEL.Views
{
    public partial class PanelTecnicoPage : ContentPage
    {
        private readonly CloudDatabaseService _dbService = new();

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
                // Refrescar estado del técnico desde Firebase para detectar si ya fue verificado o suspendido
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
                lblEspecialidad.Text = $"{tecnicoActual.Especialidad}, calificación {tecnicoActual.CalificacionPromedio:F1}, {cpTexto}";

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

        // --- MENÚ DESPLEGABLE Y GESTIÓN DE PERFIL DEL TÉCNICO ---

        private async void OnPerfilTecnicoClicked(object sender, EventArgs e)
        {
            if (LoginPage.UsuarioActual == null) return;
            await Navigation.PushAsync(new ConfiguracionPage());
        }

        private async Task ActualizarCodigoPostalTecnicoAsync()
        {
            var u = LoginPage.UsuarioActual;
            if (u == null) return;

            string nuevoCp = await DisplayPromptAsync(
                "Actualizar Código Postal",
                "Ingresa tu Código Postal de residencia (5 dígitos):",
                accept: "Guardar",
                cancel: "Cancelar",
                initialValue: u.CodigoPostal,
                maxLength: 5,
                keyboard: Keyboard.Numeric);

            if (string.IsNullOrWhiteSpace(nuevoCp)) return;

            nuevoCp = nuevoCp.Trim();
            if (nuevoCp.Length != 5 || !nuevoCp.All(char.IsDigit))
            {
                await DisplayAlertAsync("CP Inválido", "Ingresa exactamente 5 dígitos numéricos.", "OK");
                return;
            }

            var (lat, lon) = await DatabaseService.ObtenerCoordenadasPorDireccionOCpAsync(
                $"{nuevoCp}, Ciudad de México",
                nuevoCp);

            bool actualizado = await _dbService.ActualizarCodigoPostalUsuarioAsync(
                u.Id,
                u.Email,
                nuevoCp,
                lat,
                lon);

            if (actualizado)
            {
                u.CodigoPostal = nuevoCp;
                u.Latitud = lat;
                u.Longitud = lon;
                lblEspecialidad.Text = $"Especialidad: {u.Especialidad} | ⭐ {u.CalificacionPromedio:F1} | 📍 CP {nuevoCp}";
                await DisplayAlertAsync("Actualizado", $"Tu Código Postal base ahora es {nuevoCp}.", "OK");
            }
            else
            {
                await DisplayAlertAsync("Error", "No se pudo actualizar el Código Postal en la nube.", "OK");
            }
        }

        private async Task CambiarFotoPerfilTecnicoAsync()
        {
            try
            {
                var results = await MediaPicker.Default.PickPhotosAsync(new MediaPickerOptions
                {
                    Title = "Selecciona tu nueva foto de perfil"
                });

                var photo = results?.FirstOrDefault();
                if (photo != null && LoginPage.UsuarioActual != null)
                {
                    string base64 = await PHANTEL.Helpers.ImageHelper.ComprimirImagenABase64Async(photo, maxAncho: 400f, calidad: 0.65f);
                    if (string.IsNullOrEmpty(base64))
                    {
                        await DisplayAlertAsync("Error", "No se pudo procesar la imagen seleccionada.", "OK");
                        return;
                    }

                    bool guardado = await _dbService.ActualizarFotoPerfilAsync(LoginPage.UsuarioActual.Id, base64);
                    if (guardado)
                    {
                        LoginPage.UsuarioActual.FotoPerfil = base64;
                        btnPerfilTecnico.Source = ImageSource.FromStream(() => new MemoryStream(Convert.FromBase64String(base64)));
                        await DisplayAlertAsync("Éxito", "Tu foto de perfil se actualizó correctamente.", "OK");
                    }
                    else
                    {
                        await DisplayAlertAsync("Error", "No se pudo guardar la foto en la nube.", "OK");
                    }
                }
            }
            catch (Exception ex)
            {
                await DisplayAlertAsync("Error", $"No se pudo cambiar la foto: {ex.Message}", "OK");
            }
        }

        // --- ESTADO DE DISPONIBILIDAD (se cambia en Configuración) ---

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

        // --- ACCIONES INDEPENDIENTES SOBRE CITA ---

        // 1. ACEPTAR CITA
        private async void OnAceptarCitaClicked(object sender, EventArgs e)
        {
            var button = sender as Button;
            if (button?.CommandParameter is SolicitudCita cita)
            {
                await _dbService.ActualizarEstadoSolicitudAsync(cita.Id, "Aceptada");
                await DisplayAlertAsync("Estado Actualizado", "La cita ha sido marcada como Aceptada.", "OK");
                CargarDatosTecnico();
            }
        }

        // 2. FINALIZAR TRABAJO (SIN CALIFICAR AL CLIENTE)
        private async void OnFinalizarTrabajoClicked(object sender, EventArgs e)
        {
            var button = sender as Button;
            if (button?.CommandParameter is SolicitudCita cita)
            {
                if (cita.Estado == "Finalizada")
                {
                    await DisplayAlertAsync("Aviso", "Este servicio ya fue marcado como finalizado.", "OK");
                    return;
                }

                if (cita.Estado != "Aceptada")
                {
                    await DisplayAlertAsync("Atención", "Debes aceptar la cita antes de finalizar el trabajo.", "OK");
                    return;
                }

                bool confirmar = await DisplayAlertAsync("Finalizar Trabajo", "¿Confirmas que el servicio técnico ha concluido?", "Sí, finalizar", "Cancelar");
                if (confirmar)
                {
                    cita.Estado = "Finalizada";
                    await _dbService.ActualizarEstadoSolicitudAsync(cita.Id, "Finalizada");
                    await DisplayAlertAsync("Trabajo Finalizado", "El servicio ha sido marcado como finalizado.", "OK");
                    CargarDatosTecnico();
                }
            }
        }

        // 3. ELIMINAR CITA (Botón totalmente independiente)
        private async void OnEliminarCitaClicked(object sender, EventArgs e)
        {
            var button = sender as Button;
            if (button?.CommandParameter is SolicitudCita cita)
            {
                bool confirmar = await DisplayAlertAsync("⚠️ Eliminar Cita", "¿Estás seguro de eliminar permanentemente esta solicitud?", "Sí, eliminar", "Cancelar");
                if (confirmar)
                {
                    await _dbService.EliminarSolicitudAsync(cita.Id);
                    await DisplayAlertAsync("Eliminada", "La cita ha sido eliminada del sistema.", "OK");
                    CargarDatosTecnico();
                }
            }
        }

        private async void OnRechazarCitaClicked(object sender, EventArgs e)
        {
            var button = sender as Button;
            if (button?.CommandParameter is SolicitudCita cita)
            {
                bool confirmar = await DisplayAlertAsync("Confirmar", "¿Deseas rechazar esta solicitud?", "Sí", "No");
                if (confirmar)
                {
                    await _dbService.ActualizarEstadoSolicitudAsync(cita.Id, "Rechazada");
                    CargarDatosTecnico();
                }
            }
        }

        private async void OnAbrirChatClicked(object sender, EventArgs e)
        {
            var button = sender as Button;
            if (button?.CommandParameter is SolicitudCita cita)
            {
                if (cita.Estado != "Aceptada" && cita.Estado != "Finalizada")
                {
                    await DisplayAlertAsync("Atención", "Debes aceptar la cita primero para iniciar el chat.", "OK");
                    return;
                }

                await Navigation.PushAsync(new ChatPage(cita));
            }
        }

        private async void OnAbrirGoogleMapsWebClicked(object sender, EventArgs e)
        {
            var button = sender as Button;
            if (button?.CommandParameter is SolicitudCita cita)
            {
                if (cita.Estado != "Aceptada")
                {
                    await DisplayAlertAsync("Atención", "Debes aceptar la cita primero para iniciar la ruta de navegación.", "OK");
                    return;
                }

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
}