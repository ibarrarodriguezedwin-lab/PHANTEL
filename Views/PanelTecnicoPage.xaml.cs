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

        // --- MENÚ DESPLEGABLE Y GESTIÓN DE PERFIL ---

       
        private async void OnVerPerfilClicked(object sender, EventArgs e)
        {
            var usuario = LoginPage.UsuarioActual;
            if (usuario != null)
            {
                await DisplayAlert("Mi Cuenta PHANTEL",
                    $"Nombre: {usuario.Nombre}\n" +
                    $"Email: {usuario.Email}\n" +
                    $"Teléfono: {usuario.Telefono}\n" +
                    $"Rol: {usuario.TipoPerfil}\n" +
                    $"Especialidad: {usuario.Especialidad}", "Aceptar");
            }
        }

        private void OnCerrarSesionClicked(object sender, EventArgs e)
        {
            // Limpiar la sesión global antes de redirigir al Login
            LoginPage.UsuarioActual = null;
            Application.Current!.Windows[0].Page = new NavigationPage(new LoginPage());
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