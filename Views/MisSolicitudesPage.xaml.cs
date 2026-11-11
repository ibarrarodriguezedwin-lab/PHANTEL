using PHANTEL.Models;
using PHANTEL.Services;

namespace PHANTEL.Views
{
    public partial class MisSolicitudesPage : ContentPage
    {
        private readonly CloudDatabaseService _dbService = new();

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

            // Consultar las solicitudes en la nube filtrando por el ID dinámico del cliente actual
            var solicitudes = await _dbService.ObtenerSolicitudesPorUsuarioAsync(
                usuarioId: LoginPage.UsuarioActual.Id,
                esTecnico: false);

            listSolicitudes.ItemsSource = solicitudes;
        }

        private async void OnActualizarEstadoClicked(object sender, EventArgs e)
        {
            var button = sender as Button;
            if (button?.CommandParameter is SolicitudCita cita)
            {
                string nuevoEstado = cita.Estado == "Pendiente" ? "Aceptada" : "Finalizada";
                await _dbService.ActualizarEstadoSolicitudAsync(cita.Id, nuevoEstado);
                await DisplayAlert("Actualizado", $"La cita ahora está en estado: {nuevoEstado}", "OK");
                CargarSolicitudes();
            }
        }

        private async void OnEliminarCitaClicked(object sender, EventArgs e)
        {
            var button = sender as Button;
            if (button?.CommandParameter is SolicitudCita cita)
            {
                bool confirmar = await DisplayAlert("Confirmar", "¿Deseas cancelar esta solicitud?", "Sí", "No");
                if (confirmar)
                {
                    await _dbService.ActualizarEstadoSolicitudAsync(cita.Id, "Cancelada");
                    CargarSolicitudes();
                }
            }
        }
    }
}