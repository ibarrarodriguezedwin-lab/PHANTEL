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
                    await DisplayAlert("Atención", "El técnico debe aceptar la solicitud antes de poder finalizar el trabajo.", "OK");
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
                    await DisplayAlert("¡Gracias!", $"Has calificado el servicio del técnico con {popup.PuntuacionResultado} ★.", "OK");
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
                bool confirmar = await DisplayAlert("⚠️ Eliminar Cita", "¿Deseas eliminar esta solicitud de tus citas?", "Sí, eliminar", "Cancelar");
                if (confirmar)
                {
                    await _dbService.EliminarSolicitudAsync(cita.Id);
                    await DisplayAlert("Eliminada", "La cita ha sido eliminada.", "OK");
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
                    await DisplayAlert("Atención", "El técnico debe aceptar la cita primero para iniciar la conversación por chat.", "OK");
                    return;
                }

                await Navigation.PushAsync(new ChatPage(cita));
            }
        }
    }
}