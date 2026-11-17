using PHANTEL.Models;
using PHANTEL.Services;

namespace PHANTEL.Views
{
    public partial class AdminDisputasPage : ContentPage
    {
        private readonly CloudDatabaseService _dbService = new();

        public AdminDisputasPage()
        {
            InitializeComponent();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            if (LoginPage.UsuarioActual != null)
                lblAdminBienvenida.Text = $"⚖️ Admin: {LoginPage.UsuarioActual.Nombre}";

            await CargarDisputasYVerificacionesAsync();
        }

        private async Task CargarDisputasYVerificacionesAsync()
        {
            var disputas = await _dbService.ObtenerDisputasActivasAsync();
            listDisputas.ItemsSource = disputas;

            var pendientesKyc = await _dbService.ObtenerTecnicosPendientesVerificacionAsync();
            if (btnValidarTecnicosKyc != null)
            {
                btnValidarTecnicosKyc.Text = pendientesKyc.Count > 0
                    ? $"🪪 Validar Identidad de Técnicos ({pendientesKyc.Count} pendiente{(pendientesKyc.Count == 1 ? "" : "s")})"
                    : "🪪 Validar Identidad de Técnicos (Sin pendientes)";
            }
        }

        private async void OnAbrirValidacionesKycClicked(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new AdminVerificacionesPage());
        }

        private async void OnAuditarDisputaClicked(object sender, EventArgs e)
        {
            if ((sender as Button)?.CommandParameter is SolicitudCita cita)
            {
                await Navigation.PushAsync(new ResolverDisputaPage(cita));
            }
        }

        private async void OnRefrescarClicked(object sender, EventArgs e)
        {
            await CargarDisputasYVerificacionesAsync();
        }

        private void OnCerrarSesionAdminClicked(object sender, EventArgs e)
        {
            Preferences.Default.Remove("SesionEmail");
            LoginPage.UsuarioActual = null;
            Application.Current!.Windows[0].Page = new NavigationPage(new LoginPage());
        }
    }
}