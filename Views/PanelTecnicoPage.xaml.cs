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
                lblBienvenida.Text = $"👨‍🔧 {tecnicoActual.Nombre}";
                lblEspecialidad.Text = $"Especialidad: {tecnicoActual.Especialidad} | ⭐ {tecnicoActual.CalificacionPromedio:F1}";

                // Cargar imagen de perfil si la tiene
                if (!string.IsNullOrEmpty(tecnicoActual.FotoPerfil))
                {
                    try
                    {
                        byte[] imageBytes = Convert.FromBase64String(tecnicoActual.FotoPerfil);
                        btnPerfilTecnico.Source = ImageSource.FromStream(() => new MemoryStream(imageBytes));
                    }
                    catch
                    {
                        btnPerfilTecnico.Source = "dotnet_bot.png";
                    }
                }

                // FILTRO ESTRICTO: Obtenemos todas las solicitudes de la nube y filtramos por el correo del técnico actual
                var todasLasSolicitudes = await _dbService.ObtenerSolicitudesPorUsuarioAsync(tecnicoActual.Id, esTecnico: true);

                string emailTecnico = tecnicoActual.Email.Trim().ToLower();
                var citasFiltradas = todasLasSolicitudes
                    .Where(s => (!string.IsNullOrEmpty(s.TecnicoEmail) && s.TecnicoEmail.Trim().ToLower() == emailTecnico) ||
                                (string.IsNullOrEmpty(s.TecnicoEmail) && s.TecnicoId == tecnicoActual.Id))
                    .ToList();

                listCitasTecnico.ItemsSource = citasFiltradas;
            }
            else
            {
                await DisplayAlertAsync("Error de Sesión", "No se encontró un técnico activo.", "OK");
                Application.Current!.Windows[0].Page = new NavigationPage(new LoginPage());
            }
        }

        private async void OnAceptarFinalizarClicked(object sender, EventArgs e)
        {
            var button = sender as Button;
            if (button?.CommandParameter is SolicitudCita cita)
            {
                string nuevoEstado = cita.Estado == "Pendiente" ? "Aceptada" : "Finalizada";
                await _dbService.ActualizarEstadoSolicitudAsync(cita.Id, nuevoEstado);
                await DisplayAlertAsync("Estado Actualizado", $"La cita ha sido marcada como: {nuevoEstado}", "OK");
                CargarDatosTecnico();
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
                if (cita.Estado != "Aceptada")
                {
                    await DisplayAlertAsync("Atención", "Debes aceptar la cita primero para iniciar la retroalimentación por chat.", "OK");
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

        private void OnVerPerfilClicked(object sender, EventArgs e)
        {
            DisplayAlertAsync("Mi Perfil", "Módulo de perfil de técnico.", "OK");
        }

        private void OnCerrarSesionClicked(object sender, EventArgs e)
        {
            LoginPage.UsuarioActual = null;
            Application.Current!.Windows[0].Page = new NavigationPage(new LoginPage());
        }

        private Task DisplayAlertAsync(string title, string message, string cancel)
        {
            return DisplayAlert(title, message, cancel);
        }

        private Task<bool> DisplayAlertAsync(string title, string message, string accept, string cancel)
        {
            return DisplayAlert(title, message, accept, cancel);
        }
    }
}