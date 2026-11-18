using PHANTEL.Services;

namespace PHANTEL.Views
{
    public partial class VerificacionTecnicoPage : ContentPage
    {
        private readonly CloudDatabaseService _dbService = new();
        private string _frenteBase64 = string.Empty;
        private string _reversoBase64 = string.Empty;

        public VerificacionTecnicoPage()
        {
            InitializeComponent();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await CargarEstadoActualAsync();
        }

        private async Task CargarEstadoActualAsync()
        {
            var u = LoginPage.UsuarioActual;
            if (u == null) return;

            var fresco = await _dbService.ObtenerUsuarioPorEmailAsync(u.Email);
            if (fresco != null)
            {
                LoginPage.UsuarioActual = fresco;
                u = fresco;
            }

            string estado = string.IsNullOrWhiteSpace(u.EstadoVerificacion) ? "No Verificado" : u.EstadoVerificacion;
            lblEstadoVerificacion.Text = $"Estado actual: {estado}";

            switch (estado)
            {
                case "Verificado":
                    cardEstadoKyc.Stroke = Color.FromArgb("#28A745");
                    lblEstadoVerificacion.TextColor = Color.FromArgb("#28A745");
                    lblDetalleEstado.Text = "✔️ ¡Felicidades! Tu identidad está verificada. Ya cuentas con la insignia oficial ante todos los clientes.";
                    btnEnviarVerificacion.IsEnabled = false;
                    btnEnviarVerificacion.Text = "✔️ Perfil Verificado";
                    break;

                case "En Revisión":
                    cardEstadoKyc.Stroke = Color.FromArgb("#FFC107");
                    lblEstadoVerificacion.TextColor = Color.FromArgb("#FFC107");
                    lblDetalleEstado.Text = "⏳ Tus documentos están siendo auditados por Administración PHANTEL. Te notificaremos en cuanto queden aprobados.";
                    break;

                case "Rechazado":
                    cardEstadoKyc.Stroke = Color.FromArgb("#DC3545");
                    lblEstadoVerificacion.TextColor = Color.FromArgb("#FF6B6B");
                    lblDetalleEstado.Text = $"❌ Documentación rechazada: {u.MotivoRechazoVerificacion}. Sube nuevamente fotos claras de tu INE.";
                    break;
            }
        }

        private async Task<string?> CapturarImagenBase64Async()
        {
            try
            {
                string accion = await DisplayActionSheetAsync("Seleccionar imagen", "Cancelar", null, "📸 Tomar Foto con Cámara", "🖼️ Elegir de Galería");
                FileResult? archivo = null;

                if (accion == "📸 Tomar Foto con Cámara" && MediaPicker.Default.IsCaptureSupported)
                    archivo = await MediaPicker.Default.CapturePhotoAsync();
                else if (accion == "🖼️ Elegir de Galería")
                {
                    var fotos = await MediaPicker.Default.PickPhotosAsync();
                    archivo = fotos?.FirstOrDefault();
                }

                if (archivo == null) return null;
                return await PHANTEL.Helpers.ImageHelper.ComprimirImagenABase64Async(archivo, maxAncho: 800f, calidad: 0.70f);
            }
            catch (Exception ex)
            {
                await DisplayAlertAsync("Error", $"No se pudo cargar la imagen: {ex.Message}", "OK");
                return null;
            }
        }

        private async void OnSeleccionarFrenteClicked(object sender, EventArgs e)
        {
            var base64 = await CapturarImagenBase64Async();
            if (string.IsNullOrWhiteSpace(base64)) return;

            _frenteBase64 = base64;
            imgIneFrente.Source = ImageSource.FromStream(() => new MemoryStream(Convert.FromBase64String(base64)));
            imgIneFrente.IsVisible = true;
        }

        private async void OnSeleccionarReversoClicked(object sender, EventArgs e)
        {
            var base64 = await CapturarImagenBase64Async();
            if (string.IsNullOrWhiteSpace(base64)) return;

            _reversoBase64 = base64;
            imgIneReverso.Source = ImageSource.FromStream(() => new MemoryStream(Convert.FromBase64String(base64)));
            imgIneReverso.IsVisible = true;
        }

        private async void OnEnviarVerificacionClicked(object sender, EventArgs e)
        {
            var u = LoginPage.UsuarioActual;
            if (u == null) return;

            if (string.IsNullOrWhiteSpace(_frenteBase64) || string.IsNullOrWhiteSpace(_reversoBase64))
            {
                await DisplayAlertAsync("Faltan documentos", "Debes adjuntar tanto el frente como el reverso de tu INE antes de enviar.", "OK");
                return;
            }

            btnEnviarVerificacion.IsEnabled = false;
            btnEnviarVerificacion.Text = "⏳ Subiendo documentos...";

            bool ok = await _dbService.EnviarDocumentosVerificacionAsync(u.Id, u.Email, _frenteBase64, _reversoBase64);
            if (ok)
            {
                u.EstadoVerificacion = "En Revisión";
                await DisplayAlertAsync("¡Enviado!", "Tus documentos se enviaron a Administración PHANTEL para validación.", "Aceptar");
                await CargarEstadoActualAsync();
            }
            else
            {
                await DisplayAlertAsync("Error", "No se pudieron subir los documentos. Intenta de nuevo.", "OK");
            }

            btnEnviarVerificacion.IsEnabled = true;
            btnEnviarVerificacion.Text = "🚀 Enviar Documentos a Revisión";
        }
    }
}