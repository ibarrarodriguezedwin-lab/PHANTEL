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

            await CargarDisputasAsync();
        }

        private async Task CargarDisputasAsync()
        {
            var disputas = await _dbService.ObtenerDisputasActivasAsync();
            listDisputas.ItemsSource = disputas;
        }

        private async void OnRefrescarClicked(object sender, EventArgs e) => await CargarDisputasAsync();

        private async void OnAuditarDisputaClicked(object sender, EventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is SolicitudCita cita)
            {
                await Navigation.PushAsync(new ResolverDisputaPage(cita));
            }
        }

        private void OnCerrarSesionAdminClicked(object sender, EventArgs e)
        {
            Preferences.Default.Remove("SesionEmail");
            LoginPage.UsuarioActual = null;
            Application.Current!.Windows[0].Page = new NavigationPage(new LoginPage());
        }
    }
}