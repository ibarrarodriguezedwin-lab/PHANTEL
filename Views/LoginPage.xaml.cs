using CommunityToolkit.Maui.Views;
using PHANTEL.Models;
using PHANTEL.Services;

namespace PHANTEL.Views
{
    public partial class LoginPage : ContentPage
    {
        private readonly CloudDatabaseService _dbService = new();

        public static Usuario? UsuarioActual { get; set; }

        public LoginPage()
        {
            InitializeComponent();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await IniciarVideoFondoAsync();
        }

        private async Task IniciarVideoFondoAsync()
        {
            try
            {
                if (mediaFondo == null) return;

                string rutaCacheVideo = Path.Combine(FileSystem.CacheDirectory, "fondo_login_cache.mp4");

                if (!File.Exists(rutaCacheVideo))
                {
                    using var stream = await FileSystem.OpenAppPackageFileAsync("fondo_login.mp4");
                    using var fileStream = File.Create(rutaCacheVideo);
                    await stream.CopyToAsync(fileStream);
                }

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    mediaFondo.Source = MediaSource.FromFile(rutaCacheVideo);
                    mediaFondo.Volume = 1.0;
                    mediaFondo.Play();
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MEDIA INIT FALLBACK]: {ex.Message}");
                // Respaldo si el archivo está en Resources/Raw o Embed
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    mediaFondo.Source = MediaSource.FromResource("fondo_login.mp4");
                    mediaFondo.Play();
                });
            }
        }

        private void OnMediaOpened(object? sender, EventArgs e)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try
                {
                    if (mediaFondo != null)
                    {
                        await Task.Delay(100);
                        mediaFondo.InvalidateMeasure();
                        mediaFondo.Volume = 1.0;
                        mediaFondo.Play();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[MEDIA PLAY ERROR]: {ex.Message}");
                }
            });
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            PausarVideoSeguro();
        }

        private void PausarVideoSeguro()
        {
            try
            {
                if (mediaFondo != null)
                {
                    mediaFondo.Pause();
                    mediaFondo.Stop();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MEDIA ERROR OnDisappearing]: {ex.Message}");
            }
        }

        private async void OnIngresarClicked(object sender, EventArgs e)
        {
            string email = entryEmail.Text?.Trim() ?? "";
            string pass = entryPassword.Text?.Trim() ?? "";

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(pass))
            {
                lblError.Text = "Por favor ingresa correo y contraseña.";
                lblError.IsVisible = true;
                return;
            }

            var usuario = await _dbService.ValidarLoginAsync(email, pass);

            if (usuario != null)
            {
                lblError.IsVisible = false;
                UsuarioActual = usuario;

                Preferences.Default.Set("SesionEmail", usuario.Email.Trim().ToLower());

                await NotificacionRealtimeService.IniciarEscuchaAsync(usuario);

                PausarVideoSeguro();
                mediaFondo?.Handler?.DisconnectHandler();

                if (usuario.CuentaSuspendida)
                {
                    Application.Current!.Windows[0].Page = new NavigationPage(new CuentaSuspendidaPage());
                    return;
                }

                if (usuario.TipoPerfil == "Admin")
                {
                    Application.Current!.Windows[0].Page = new NavigationPage(new AdminDisputasPage());
                }
                else if (usuario.TipoPerfil == "Técnico")
                {
                    Application.Current!.Windows[0].Page = new NavigationPage(new PanelTecnicoPage());
                }
                else
                {
                    Application.Current!.Windows[0].Page = new AppShell();
                }
            }
            else
            {
                lblError.Text = "Credenciales incorrectas o usuario no registrado.";
                lblError.IsVisible = true;
            }
        }

        private async void OnOlvidarPasswordClicked(object sender, EventArgs e)
        {
            string email = await DisplayPromptAsync(
                "Recuperar Contraseña",
                "Ingresa el correo electrónico con el que te registraste:",
                accept: "Siguiente",
                cancel: "Cancelar",
                placeholder: "ejemplo@correo.com",
                keyboard: Keyboard.Email);

            if (string.IsNullOrWhiteSpace(email)) return;

            string telefono = await DisplayPromptAsync(
                "Verificación de Seguridad",
                "Ingresa el número de teléfono asociado a tu cuenta:",
                accept: "Verificar",
                cancel: "Cancelar",
                placeholder: "10 dígitos",
                keyboard: Keyboard.Telephone);

            if (string.IsNullOrWhiteSpace(telefono)) return;

            string nuevaPass = await DisplayPromptAsync(
                "Nueva Contraseña",
                "Escribe tu nueva contraseña (mínimo 6 caracteres):",
                accept: "Guardar",
                cancel: "Cancelar",
                placeholder: "Nueva contraseña");

            if (string.IsNullOrWhiteSpace(nuevaPass) || nuevaPass.Trim().Length < 6)
            {
                await DisplayAlertAsync("Atención", "La contraseña debe tener al menos 6 caracteres.", "OK");
                return;
            }

            bool actualizado = await _dbService.RestablecerPasswordAsync(email, telefono, nuevaPass);

            if (actualizado)
            {
                await DisplayAlertAsync("¡Listo!", "Tu contraseña se actualizó correctamente. Ya puedes iniciar sesión.", "Aceptar");
            }
            else
            {
                await DisplayAlertAsync("Datos incorrectos", "El correo y el teléfono no coinciden con ninguna cuenta registrada.", "OK");
            }
        }

        private async void OnIrARegistroClicked(object sender, EventArgs e)
        {
            PausarVideoSeguro();
            await Navigation.PushAsync(new RegistroPage());
        }
    }
}