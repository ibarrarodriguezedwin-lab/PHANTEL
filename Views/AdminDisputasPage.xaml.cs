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
                lblAdminBienvenida.Text = $"Administrador: {LoginPage.UsuarioActual.Nombre}";

            await CargarDisputasYVerificacionesAsync();
        }

        private async Task CargarDisputasYVerificacionesAsync()
        {
            var disputas = await _dbService.ObtenerDisputasActivasAsync();
            listDisputas.ItemsSource = disputas;

            var pendientesKyc = await _dbService.ObtenerTecnicosPendientesVerificacionAsync();
            if (lblValidarTecnicosKyc != null)
            {
                lblValidarTecnicosKyc.Text = pendientesKyc.Count > 0
                    ? $"Verificaciones de identidad ({pendientesKyc.Count} pendiente{(pendientesKyc.Count == 1 ? "" : "s")})"
                    : "Verificaciones de identidad (sin pendientes)";
            }

            await CargarReportesPrivadosClientesAsync();
        }

        private async Task CargarReportesPrivadosClientesAsync()
        {
            var lista = await _dbService.ObtenerReportesPrivadosClientesAsync();
            listReportesClientes.ItemsSource = lista;
        }

        // --- ENVIAR AVISO DE MAL COMPORTAMIENTO AL CLIENTE ---
        private async void OnEnviarAvisoClienteClicked(object sender, EventArgs e)
        {
            var btn = sender as Button;
            var reporte = btn?.CommandParameter as ReporteClienteItem
                          ?? btn?.BindingContext as ReporteClienteItem;

            if (reporte == null)
            {
                await DisplayAlertAsync("Aviso", "No se pudo leer los datos del reporte seleccionado.", "OK");
                return;
            }

            string mensajeAviso = await DisplayPromptAsync(
                "Aviso de Conducta al Cliente",
                $"Escribe el mensaje de advertencia que verá el Cliente #{reporte.ClienteId} en su pantalla principal:",
                accept: "Enviar Aviso",
                cancel: "Cancelar",
                initialValue: $"Advertencia por conducta reportada en la cita #{reporte.SolicitudId}: {reporte.Motivo}. Evita reincidir para conservar tu cuenta activa.");

            if (string.IsNullOrWhiteSpace(mensajeAviso)) return;

            bool enviado = await _dbService.EnviarAvisoConductaClienteAsync(
                reporte.ClienteId,
                reporte.Key,
                mensajeAviso.Trim());

            if (enviado)
            {
                await DisplayAlertAsync("Aviso Enviado", "El aviso quedó activo y el cliente lo verá al entrar a la pantalla principal.", "OK");
                await CargarReportesPrivadosClientesAsync();
            }
            else
            {
                await DisplayAlertAsync("No encontrado", $"No se pudo enviar el aviso al Cliente #{reporte.ClienteId}.", "OK");
            }
        }

        // --- SUSPENDER CUENTA DEL CLIENTE ---
        private async void OnSuspenderClienteClicked(object sender, EventArgs e)
        {
            var btn = sender as Button;
            var reporte = btn?.CommandParameter as ReporteClienteItem
                          ?? btn?.BindingContext as ReporteClienteItem;

            if (reporte == null)
            {
                await DisplayAlertAsync("Aviso", "No se pudo leer los datos del reporte seleccionado.", "OK");
                return;
            }

            string motivoSuspension = await DisplayPromptAsync(
                "Suspender Cuenta del Cliente",
                $"Ingresa el motivo oficial de suspensión para el Cliente #{reporte.ClienteId} (lo verá en su pantalla de bloqueo):",
                accept: "Suspender Cuenta",
                cancel: "Cancelar",
                initialValue: $"Incumplimiento de normas de convivencia y seguridad PHANTEL ({reporte.Motivo}).");

            if (string.IsNullOrWhiteSpace(motivoSuspension)) return;

            bool suspendido = await _dbService.SuspenderClienteAsync(
                reporte.ClienteId,
                reporte.Key,
                motivoSuspension.Trim());

            if (suspendido)
            {
                await DisplayAlertAsync("Cuenta Suspendida", "La cuenta del cliente quedó suspendida y verá el motivo al abrir la app.", "OK");
                await CargarReportesPrivadosClientesAsync();
            }
            else
            {
                await DisplayAlertAsync("No encontrado", $"No se pudo suspender al Cliente #{reporte.ClienteId}.", "OK");
            }
        }

        private void OnAbrirValidacionesKycTapped(object? sender, TappedEventArgs e)
            => OnAbrirValidacionesKycClicked(sender ?? this, EventArgs.Empty);

        private async void OnAbrirValidacionesKycClicked(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new AdminVerificacionesPage());
        }

        private void OnAuditarDisputaTapped(object? sender, TappedEventArgs e)
        {
            var cita = e.Parameter as SolicitudCita
                       ?? (sender as BindableObject)?.BindingContext as SolicitudCita;

            if (cita != null)
                OnAuditarDisputaClicked(new Button { CommandParameter = cita }, EventArgs.Empty);
        }

        private async void OnAuditarDisputaClicked(object sender, EventArgs e)
        {
            if ((sender as Button)?.CommandParameter is SolicitudCita cita)
            {
                await Navigation.PushAsync(new ResolverDisputaPage(cita));
            }
        }

        private void OnRefrescarTapped(object? sender, TappedEventArgs e)
            => OnRefrescarClicked(sender ?? this, EventArgs.Empty);

        private async void OnRefrescarClicked(object sender, EventArgs e)
        {
            await CargarDisputasYVerificacionesAsync();
        }

        private void OnCerrarSesionAdminTapped(object? sender, TappedEventArgs e)
            => OnCerrarSesionAdminClicked(sender ?? this, EventArgs.Empty);

        private void OnCerrarSesionAdminClicked(object sender, EventArgs e)
        {
            NotificacionRealtimeService.DetenerEscucha();
            Preferences.Default.Remove("SesionEmail");
            LoginPage.UsuarioActual = null;
            Application.Current!.Windows[0].Page = new NavigationPage(new LoginPage());
        }
    }
}