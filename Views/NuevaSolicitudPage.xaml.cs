using CommunityToolkit.Maui.Views;
using PHANTEL.Models;
using PHANTEL.Services;

namespace PHANTEL.Views
{
    public partial class NuevaSolicitudPage : ContentPage
    {
        private readonly Usuario _tecnico;
        private readonly DatabaseService _dbService = new();
        private double _distanciaCalculada;
        private decimal _tarifaCalculada;

        // Inicializar en null para evitar que los controles intenten leer rutas vacías
        private string? _rutaFoto = null;
        private string? _rutaVideo = null;
        private string? _rutaAudio = null;

        public NuevaSolicitudPage(Usuario tecnico)
        {
            InitializeComponent();
            _tecnico = tecnico;
            lblTecnicoInfo.Text = $"Atención con: {tecnico.Nombre} ({tecnico.Especialidad})";
            CalcularGeolocalizacion();
        }

        private async void CalcularGeolocalizacion()
        {
            try
            {
                var request = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10));
                var location = await Geolocation.Default.GetLocationAsync(request);

                double latCliente = location?.Latitude ?? 19.3452;
                double lonCliente = location?.Longitude ?? -99.1384;

                _distanciaCalculada = DatabaseService.CalcularDistanciaKm(latCliente, lonCliente, _tecnico.Latitud, _tecnico.Longitud);
                _tarifaCalculada = DatabaseService.CalcularTarifaTraslado(_distanciaCalculada);

                lblDistancia.Text = $"Distancia estimada al técnico: {_distanciaCalculada} km";
                lblTarifa.Text = $"Tarifa de traslado estimada: ${_tarifaCalculada:F2} MXN";
            }
            catch
            {
                double latCliente = 19.3452;
                double lonCliente = -99.1384;

                _distanciaCalculada = DatabaseService.CalcularDistanciaKm(latCliente, lonCliente, _tecnico.Latitud, _tecnico.Longitud);
                _tarifaCalculada = DatabaseService.CalcularTarifaTraslado(_distanciaCalculada);

                lblDistancia.Text = $"Distancia estimada: {_distanciaCalculada} km";
                lblTarifa.Text = $"Tarifa de traslado: ${_tarifaCalculada:F2} MXN";
            }
        }

        private async void OnTomarFotoClicked(object sender, EventArgs e)
        {
            try
            {
                var photo = await MediaPicker.CapturePhotoAsync();
                if (photo != null)
                {
                    var newPath = Path.Combine(FileSystem.CacheDirectory, photo.FileName);
                    using var stream = await photo.OpenReadAsync();
                    using var newStream = File.OpenWrite(newPath);
                    await stream.CopyToAsync(newStream);

                    _rutaFoto = newPath;
                    imgPrevisualizacion.Source = ImageSource.FromFile(_rutaFoto);
                    imgPrevisualizacion.IsVisible = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al tomar foto: {ex.Message}");
            }
        }

        private async void OnGrabarVideoClicked(object sender, EventArgs e)
        {
            try
            {
                var video = await MediaPicker.CaptureVideoAsync();
                if (video != null)
                {
                    var newPath = Path.Combine(FileSystem.CacheDirectory, video.FileName);
                    using var stream = await video.OpenReadAsync();
                    using var newStream = File.OpenWrite(newPath);
                    await stream.CopyToAsync(newStream);

                    _rutaVideo = newPath;

                    // Asignar el reproductor únicamente cuando el archivo ya esté guardado en disco
                    mediaVideoPrevisualizacion.Source = MediaSource.FromFile(_rutaVideo);
                    mediaVideoPrevisualizacion.IsVisible = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al grabar video: {ex.Message}");
            }
        }

        private async void OnGrabarAudioClicked(object sender, EventArgs e)
        {
            await DisplayAlert("Audio", "Presiona para iniciar/detener la grabación de voz.", "OK");
            _rutaAudio = "audio_evidencia_sample.mp3";
        }

        private async void OnAgendarCitaClicked(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(editorDescripcion.Text))
            {
                lblErrorDescripcion.IsVisible = true;
                return;
            }
            lblErrorDescripcion.IsVisible = false;

            // Manejo seguro de nulos para DateTime? y TimeSpan?
            DateTime fechaBase = pickerFecha.Date ?? DateTime.Today;
            TimeSpan horaSeleccionada = pickerHora.Time ?? new TimeSpan(10, 0, 0);
            DateTime fechaProgramada = fechaBase.Date.Add(horaSeleccionada);

            string direccionFinal = string.IsNullOrWhiteSpace(entryDireccion.Text)
                ? "Ubicación por coordenadas GPS"
                : entryDireccion.Text.Trim();

            int idClienteActual = LoginPage.UsuarioActual?.Id ?? 1;

            var nuevaCita = new SolicitudCita
            {
                ClienteId = idClienteActual,
                TecnicoId = _tecnico.Id,
                DescripcionFalla = editorDescripcion.Text,
                DireccionCliente = direccionFinal,
                FechaHoraProgramada = fechaProgramada,
                DistanciaKm = _distanciaCalculada,
                TarifaTraslado = _tarifaCalculada,
                Estado = "Pendiente",
                EvidenciaRutaImagen = _rutaFoto ?? string.Empty,
                EvidenciaRutaVideo = _rutaVideo ?? string.Empty,
                EvidenciaRutaAudio = _rutaAudio ?? string.Empty
            };

            await _dbService.CrearSolicitudAsync(nuevaCita);
            await DisplayAlert("Éxito", $"Tu solicitud fue agendada para el {fechaProgramada:dd/MM/yyyy} a las {fechaProgramada:HH:mm} hrs.", "Aceptar");
            await Navigation.PopAsync();
        }
    }
}