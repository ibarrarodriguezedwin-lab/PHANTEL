using PHANTEL.Services;
using PHANTEL.Views;

namespace PHANTEL
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var window = new Window(new NavigationPage(new LoginPage()));
            VerificarSesionActivaAsync(window);
            return window;
        }

        private async void VerificarSesionActivaAsync(Window window)
        {
            string emailGuardado = Preferences.Default.Get("SesionEmail", string.Empty);

            if (!string.IsNullOrWhiteSpace(emailGuardado))
            {
                try
                {
                    var dbService = new CloudDatabaseService();
                    var usuario = await dbService.ObtenerUsuarioPorEmailAsync(emailGuardado);

                    if (usuario != null)
                    {
                        LoginPage.UsuarioActual = usuario;

                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            if (usuario.TipoPerfil == "Técnico")
                                window.Page = new NavigationPage(new PanelTecnicoPage());
                            else
                                window.Page = new AppShell();
                        });
                    }
                    else
                    {
                        Preferences.Default.Remove("SesionEmail");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ERROR RESTAURAR SESION]: {ex.Message}");
                }
            }
        }
    }
}
