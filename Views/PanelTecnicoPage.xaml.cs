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
            // Obtener el técnico actual en tiempo real desde la propiedad global
            var tecnicoActual = LoginPage.UsuarioActual;

            if (tecnicoActual != null)
            {
                lblBienvenida.Text = $"👨‍🔧 {tecnicoActual.Nombre}";
                lblEspecialidad.Text = $"Especialidad: {tecnicoActual.Especialidad} | ⭐ {tecnicoActual.CalificacionPromedio:F1}";

                // Cargar Foto de Perfil en el ImageButton si existe en Base64
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

                // Cargar solicitudes asociadas a este técnico en vivo desde la nube
                var citas = await _dbService.ObtenerSolicitudesPorUsuarioAsync(tecnicoActual.Id, esTecnico: true);
                listCitasTecnico.ItemsSource = citas;
            }
            else
            {
                await DisplayAlert("Error de Sesión", "No se encontró un técnico activo. Inicia sesión de nuevo.", "OK");
                Application.Current!.Windows[0].Page = new NavigationPage(new LoginPage());
            }
        }

        // --- MENÚ DESPLEGABLE Y GESTIÓN DE PERFIL DEL TÉCNICO ---

        private async void OnPerfilTecnicoClicked(object sender, EventArgs e)
        {
            var u = LoginPage.UsuarioActual;
            if (u == null) return;

            string opcion = await DisplayActionSheet(
                $"Técnico: {u.Nombre}",
                "Cancelar",
                null,
                "📷 Editar Foto de Perfil",
                "📋 Historial de Citas",
                "🚪 Cerrar Sesión");

            switch (opcion)
            {
                case "📷 Editar Foto de Perfil":
                    await CambiarFotoPerfilTecnicoAsync();
                    break;

                case "📋 Historial de Citas":
                    var citas = await _dbService.ObtenerSolicitudesPorUsuarioAsync(u.Id, esTecnico: true);
                    var historial = citas.Where(c => c.Estado == "Finalizada" || c.Estado == "Rechazada").ToList();

                    if (historial.Count == 0)
                    {
                        await DisplayAlert("Historial de Citas", "Aún no tienes citas finalizadas o rechazadas en tu historial.", "OK");
                    }
                    else
                    {
                        string resumen = string.Join("\n\n", historial.Select(c =>
                            $"• Cita #{c.Id} [{c.Estado}]\n  Falla: {c.DescripcionFalla}\n  Traslado: ${c.TarifaTraslado:F2}"));
                        await DisplayAlert("Historial de Citas", resumen, "Cerrar");
                    }
                    break;

                case "🚪 Cerrar Sesión":
                    LoginPage.UsuarioActual = null;
                    Application.Current!.Windows[0].Page = new NavigationPage(new LoginPage());
                    break;
            }
        }

        private async Task CambiarFotoPerfilTecnicoAsync()
        {
            try
            {
                var photo = await MediaPicker.Default.PickPhotoAsync(new MediaPickerOptions
                {
                    Title = "Selecciona tu nueva foto de perfil"
                });

                if (photo != null && LoginPage.UsuarioActual != null)
                {
                    using var stream = await photo.OpenReadAsync();
                    using var ms = new MemoryStream();
                    await stream.CopyToAsync(ms);
                    string base64 = Convert.ToBase64String(ms.ToArray());

                    bool guardado = await _dbService.ActualizarFotoPerfilAsync(LoginPage.UsuarioActual.Id, base64);
                    if (guardado)
                    {
                        LoginPage.UsuarioActual.FotoPerfil = base64;
                        btnPerfilTecnico.Source = ImageSource.FromStream(() => new MemoryStream(Convert.FromBase64String(base64)));
                        await DisplayAlert("Éxito", "Tu foto de perfil se actualizó correctamente.", "OK");
                    }
                    else
                    {
                        await DisplayAlert("Error", "No se pudo guardar la foto en la nube.", "OK");
                    }
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"No se pudo cambiar la foto: {ex.Message}", "OK");
            }
        }

        // --- ACCIONES SOBRE CITA ---

        private async void OnAceptarFinalizarClicked(object sender, EventArgs e)
        {
            var button = sender as Button;
            if (button?.CommandParameter is SolicitudCita cita)
            {
                string nuevoEstado = cita.Estado == "Pendiente" ? "Aceptada" : "Finalizada";
                await _dbService.ActualizarEstadoSolicitudAsync(cita.Id, nuevoEstado);
                await DisplayAlert("Estado Actualizado", $"La cita ha sido marcada como: {nuevoEstado}", "OK");
                CargarDatosTecnico();
            }
        }

        private async void OnRechazarCitaClicked(object sender, EventArgs e)
        {
            var button = sender as Button;
            if (button?.CommandParameter is SolicitudCita cita)
            {
                bool confirmar = await DisplayAlert("Confirmar", "¿Deseas rechazar esta solicitud?", "Sí", "No");
                if (confirmar)
                {
                    await _dbService.ActualizarEstadoSolicitudAsync(cita.Id, "Rechazada");
                    CargarDatosTecnico();
                }
            }
        }

        // --- NAVEGACIÓN A CHAT DE RETROALIMENTACIÓN DIRECTA ---
        private async void OnAbrirChatClicked(object sender, EventArgs e)
        {
            var button = sender as Button;
            if (button?.CommandParameter is SolicitudCita cita)
            {
                if (cita.Estado != "Aceptada")
                {
                    await DisplayAlert("Atención", "Debes aceptar la cita primero para iniciar la retroalimentación por chat.", "OK");
                    return;
                }

                await Navigation.PushAsync(new ChatPage(cita));
            }
        }

        // --- NAVEGACIÓN EN TIEMPO REAL CON GOOGLE MAPS (WEB / UNIVERSAL) ---
        private async void OnAbrirGoogleMapsWebClicked(object sender, EventArgs e)
        {
            var button = sender as Button;
            if (button?.CommandParameter is SolicitudCita cita)
            {
                if (cita.Estado != "Aceptada")
                {
                    await DisplayAlert("Atención", "Debes aceptar la cita primero para iniciar la ruta de navegación.", "OK");
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
                    await DisplayAlert("Error de Navegación", $"No se pudo abrir Google Maps: {ex.Message}", "OK");
                }
            }
        }
    }
}