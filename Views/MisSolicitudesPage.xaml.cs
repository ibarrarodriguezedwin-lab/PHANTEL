using PHANTEL.Models;
using PHANTEL.Services;

namespace PHANTEL.Views
{
    public partial class MisSolicitudesPage : ContentPage
    {
        private readonly DatabaseService _dbService = new();

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
            var solicitudes = await _dbService.ObtenerSolicitudesPorUsuarioAsync(usuarioId: 1, esTecnico: false);
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
                bool confirmar = await DisplayAlert("Confirmar", "¿Deseas cancelar y eliminar esta solicitud?", "Sí", "No");
                if (confirmar)
                {
                    await _dbService.EliminarSolicitudAsync(cita.Id);
                    CargarSolicitudes();
                }
            }
        }
    }
}