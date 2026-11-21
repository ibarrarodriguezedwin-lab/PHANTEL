using CommunityToolkit.Maui.Views;
using PHANTEL.Helpers;
using PHANTEL.Models;
using PHANTEL.Services;

namespace PHANTEL.Views
{
    public partial class NuevaSolicitudPage : ContentPage
    {
        private readonly Usuario _tecnico;
        private readonly CloudDatabaseService _dbService = new();
        private double _distanciaCalculada;
        private decimal _tarifaCalculada;

        private string _fotoBase64Comprimida = string.Empty;
        private string? _rutaVideo = null;
        private string? _rutaAudio = null;
        private CancellationTokenSource? _ctsGeocoding;

        public NuevaSolicitudPage(Usuario tecnico)
        {
            InitializeComponent();
            _tecnico = tecnico;
            lblTecnicoInfo.Text = $"Atención con: {tecnico.Nombre} ({tecnico.Especialidad})";

            if (entryDireccion != null)
                entryDireccion.TextChanged += OnDireccionDestinoChanged;

            CalcularGeolocalizacion();
        }

        private async void CalcularGeolocalizacion()
        {
            await RecalcularTarifaDesdeCpTecnicoAsync(entryDireccion?.Text);
        }

        private async void OnDireccionDestinoChanged(object? sender, TextChangedEventArgs e)
        {
            _ctsGeocoding?.Cancel();
            _ctsGeocoding = new CancellationTokenSource();
            var token = _ctsGeocoding.Token;

            try
            {
                await Task.Delay(700, token);
                await RecalcularTarifaDesdeCpTecnicoAsync(e.NewTextValue);
            }
            catch (TaskCanceledException) { }
        }

        private async Task RecalcularTarifaDesdeCpTecnicoAsync(string? direccionDestino)
        {
            // 1. Origen: Código Postal base del Técnico que realizará el traslado (ej. 14270)
            string cpTecnico = !string.IsNullOrWhiteSpace(_tecnico.CodigoPostal)
                ? _tecnico.CodigoPostal.Trim()
                : (LoginPage.UsuarioActual?.CodigoPostal ?? string.Empty);

            var (latOrigen, lonOrigen) = (Math.Abs(_tecnico.Latitud) > 0.1 && Math.Abs(_tecnico.Longitud) > 0.1)
                ? (_tecnico.Latitud, _tecnico.Longitud)
                : await DatabaseService.ObtenerCoordenadasPorDireccionOCpAsync($"{cpTecnico}, Ciudad de México", cpTecnico);

            // 2. Destino: Domicilio del servicio escrito por el cliente (o su CP base si aún no escribe dirección)
            string cpClienteRespaldo = LoginPage.UsuarioActual?.CodigoPostal ?? string.Empty;

            (double latDestino, double lonDestino) destinoCoords;
            if (!string.IsNullOrWhiteSpace(direccionDestino) && direccionDestino.Trim().Length >= 5)
            {
                destinoCoords = await DatabaseService.ObtenerCoordenadasPorDireccionOCpAsync(
                    direccionDestino.Trim(),
                    cpClienteRespaldo);
            }
            else if (!string.IsNullOrWhiteSpace(cpClienteRespaldo))
            {
                destinoCoords = await DatabaseService.ObtenerCoordenadasPorDireccionOCpAsync(
                    $"{cpClienteRespaldo}, Ciudad de México",
                    cpClienteRespaldo);
            }
            else
            {
                destinoCoords = (latOrigen, lonOrigen);
            }

            _distanciaCalculada = DatabaseService.CalcularDistanciaKm(
                latOrigen,
                lonOrigen,
                destinoCoords.latDestino,
                destinoCoords.lonDestino);

            _tarifaCalculada = DatabaseService.CalcularTarifaTraslado(_distanciaCalculada);

            MainThread.BeginInvokeOnMainThread(() =>
            {
                string origenTexto = string.IsNullOrWhiteSpace(cpTecnico) ? "base del técnico" : $"CP técnico {cpTecnico}";
                lblDistancia.Text = $"Distancia desde {origenTexto}: {_distanciaCalculada:F1} km";
                lblTarifa.Text = $"Tarifa de traslado ($8/km): ${_tarifaCalculada:F2} MXN";
            });
        }

        private async void OnTomarFotoClicked(object sender, EventArgs e)
        {
            try
            {
                var photo = await MediaPicker.Default.CapturePhotoAsync();
                if (photo != null)
                {
                    await GuardarYMostrarFotoAsync(photo);
                }
            }
            catch (Exception ex)
            {
                await DisplayAlertAsync("Cámara no disponible", $"No se pudo abrir la cámara ({ex.Message}). Puedes usar el botón '🖼️ Galería'.", "OK");
            }
        }

        private async void OnSeleccionarFotoGaleriaClicked(object sender, EventArgs e)
        {
            try
            {
                var results = await MediaPicker.Default.PickPhotosAsync(new MediaPickerOptions
                {
                    Title = "Selecciona una foto de la falla"
                });

                var photo = results?.FirstOrDefault();
                if (photo != null)
                {
                    await GuardarYMostrarFotoAsync(photo);
                }
            }
            catch (Exception ex)
            {
                await DisplayAlertAsync("Error", $"No se pudo seleccionar la imagen: {ex.Message}", "OK");
            }
        }

        private async Task GuardarYMostrarFotoAsync(FileResult photo)
        {
            string base64 = await ImageHelper.ComprimirImagenABase64Async(photo, maxAncho: 700f, calidad: 0.65f);
            if (string.IsNullOrEmpty(base64))
            {
                await DisplayAlertAsync("Error", "No se pudo procesar la foto seleccionada.", "OK");
                return;
            }

            _fotoBase64Comprimida = base64;
            imgPrevisualizacion.Source = ImageSource.FromStream(() => new MemoryStream(Convert.FromBase64String(base64)));
            imgPrevisualizacion.IsVisible = true;
        }

        private async void OnGrabarVideoClicked(object sender, EventArgs e)
        {
            try
            {
                var video = await MediaPicker.Default.CaptureVideoAsync();
                if (video != null)
                {
                    var newPath = Path.Combine(FileSystem.CacheDirectory, video.FileName);
                    using var stream = await video.OpenReadAsync();
                    using var newStream = File.OpenWrite(newPath);
                    await stream.CopyToAsync(newStream);

                    _rutaVideo = newPath;
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
            await DisplayAlertAsync("Audio", "Audio de muestra adjuntado.", "OK");
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

            if (LoginPage.UsuarioActual == null)
            {
                await DisplayAlertAsync("Error", "No hay sesión de usuario activa.", "OK");
                return;
            }

            await RecalcularTarifaDesdeCpTecnicoAsync(entryDireccion.Text);

            DateTime fechaBase = pickerFecha.Date ?? DateTime.Today;
            TimeSpan horaSeleccionada = pickerHora.Time ?? new TimeSpan(10, 0, 0);
            DateTime fechaProgramada = fechaBase.Date.Add(horaSeleccionada);

            string direccionFinal = string.IsNullOrWhiteSpace(entryDireccion.Text)
                ? (string.IsNullOrEmpty(LoginPage.UsuarioActual.Direccion) ? $"Zona CP {LoginPage.UsuarioActual.CodigoPostal}" : LoginPage.UsuarioActual.Direccion)
                : entryDireccion.Text.Trim();

            var nuevaCita = new SolicitudCita
            {
                ClienteId = LoginPage.UsuarioActual.Id,
                TecnicoId = _tecnico.Id,
                TecnicoEmail = _tecnico.Email.Trim().ToLower(),
                DescripcionFalla = editorDescripcion.Text.Trim(),
                DireccionCliente = direccionFinal,
                FechaHoraProgramada = fechaProgramada,
                DistanciaKm = _distanciaCalculada,
                TarifaTraslado = _tarifaCalculada,
                Estado = "Pendiente",
                FechaCreacion = DateTime.Now,
                EvidenciaRutaImagen = _fotoBase64Comprimida,
                EvidenciaRutaVideo = string.Empty,
                EvidenciaRutaAudio = _rutaAudio ?? string.Empty
            };

            bool guardado = await _dbService.CrearSolicitudAsync(nuevaCita);

            if (guardado)
            {
                await DisplayAlertAsync("Éxito", $"Solicitud agendada con éxito para el {fechaProgramada:dd/MM/yyyy HH:mm} hrs.", "Aceptar");
                await Navigation.PopAsync();
            }
            else
            {
                await DisplayAlertAsync("Error", "No se pudo guardar la solicitud en la nube.", "OK");
            }
        }
    }
}