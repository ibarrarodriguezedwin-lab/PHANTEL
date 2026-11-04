using PHANTEL.Models;
using PHANTEL.Services;

namespace PHANTEL.Views
{
    public partial class PanelTecnicoPage : ContentPage
    {
        private readonly DatabaseService _dbService = new();
        private readonly Usuario? _tecnicoActual = LoginPage.UsuarioActual;

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
            if (_tecnicoActual != null)
            {
                lblBienvenida.Text = $"👨‍🔧 {_tecnicoActual.Nombre}";
                lblEspecialidad.Text = $"Especialidad: {_tecnicoActual.Especialidad} | ⭐ {_tecnicoActual.CalificacionPromedio:F1}";

                var citas = await _dbService.ObtenerSolicitudesPorUsuarioAsync(_tecnicoActual.Id, esTecnico: true);
                listCitasTecnico.ItemsSource = citas;
            }
        }

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
                    // Prioriza la dirección escrita por el cliente o usa las coordenadas GPS por defecto
                    string destino = !string.IsNullOrWhiteSpace(cita.DireccionCliente) && cita.DireccionCliente != "Ubicación por coordenadas GPS"
                        ? Uri.EscapeDataString(cita.DireccionCliente)
                        : "19.3452,-99.1384";

                    // URL universal para trazar ruta de manejo en Google Maps
                    string urlGoogleMaps = $"https://www.google.com/maps/dir/?api=1&destination={destino}&travelmode=driving";

                    // Abre en el navegador por defecto (Chrome/Edge/Safari)
                    await Launcher.OpenAsync(new Uri(urlGoogleMaps));
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Error de Navegación", $"No se pudo abrir Google Maps: {ex.Message}", "OK");
                }
            }
        }

        private async void OnCerrarSesionClicked(object sender, EventArgs e)
        {
            Application.Current!.Windows[0].Page = new LoginPage();
        }
    }
}