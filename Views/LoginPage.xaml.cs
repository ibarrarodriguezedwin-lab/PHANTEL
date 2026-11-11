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

        private void OnMediaOpened(object sender, EventArgs e)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    if (mediaFondo != null)
                    {
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

                PausarVideoSeguro();

                if (usuario.TipoPerfil == "Técnico")
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

        private async void OnIrARegistroClicked(object sender, EventArgs e)
        {
            PausarVideoSeguro();
            await Navigation.PushAsync(new RegistroPage());
        }
    }
}