using PHANTEL.Models;
using PHANTEL.Services;

namespace PHANTEL.Views
{
    public partial class LoginPage : ContentPage
    {
        private readonly DatabaseService _dbService = new();

        public static Usuario? UsuarioActual { get; private set; }

        public LoginPage()
        {
            InitializeComponent();
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
                UsuarioActual = usuario; // Guardar sesión activa

                // Redirección según Perfil
                if (usuario.TipoPerfil == "Técnico")
                {
                    // El técnico va a su panel exclusivo de gestión
                    Application.Current!.Windows[0].Page = new NavigationPage(new PanelTecnicoPage());
                }
                else
                {
                    // El cliente entra a la vista general por pestañas
                    Application.Current!.Windows[0].Page = new AppShell();
                }
            }
            else
            {
                lblError.Text = "Credenciales incorrectas.";
                lblError.IsVisible = true;
            }
        }
    }
}