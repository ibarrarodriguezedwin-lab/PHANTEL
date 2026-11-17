using PHANTEL.Services;

namespace PHANTEL.Views
{
    public partial class CuentaSuspendidaPage : ContentPage
    {
        private readonly CloudDatabaseService _dbService = new();

        public CuentaSuspendidaPage()
        {
            InitializeComponent();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await CargarDatosSuspensionEnVivoAsync();
        }

        private async Task CargarDatosSuspensionEnVivoAsync()
        {
            string email = LoginPage.UsuarioActual?.Email
                           ?? Preferences.Default.Get("SesionEmail", string.Empty);

            if (!string.IsNullOrWhiteSpace(email))
            {
                var usuarioFresco = await _dbService.ObtenerUsuarioPorEmailAsync(email);
                if (usuarioFresco != null)
                {
                    LoginPage.UsuarioActual = usuarioFresco;
                }
            }

            var u = LoginPage.UsuarioActual;
            if (u == null) return;

            lblNombreTecnico.Text = $"Técnico: {u.Nombre} ({u.Email})";

            string motivo = u.MotivoSuspension;

            // Respaldo: si la suspensión se aplicó antes de agregar MotivoSuspension, leer el dictamen de su última cita cerrada
            if (string.IsNullOrWhiteSpace(motivo))
            {
                var citas = await _dbService.ObtenerSolicitudesPorUsuarioAsync(u.Id, esTecnico: true);
                var ultimaResuelta = citas
                    .Where(c => c.ResueltaPorAdmin || !string.IsNullOrWhiteSpace(c.DictamenAdmin))
                    .OrderByDescending(c => c.FechaCreacion)
                    .FirstOrDefault();

                if (ultimaResuelta != null)
                {
                    motivo = $"Cita #{ultimaResuelta.Id}: {ultimaResuelta.DictamenAdmin}";
                }
            }

            lblMotivoSuspension.Text = string.IsNullOrWhiteSpace(motivo)
                ? "Incumplimiento de políticas de servicio PHANTEL detectado en auditoría."
                : motivo;

            lblSaldoDisponible.Text = $"Saldo liberado disponible: ${u.SaldoLiberado:F2} MXN";
            lblSaldoRetenido.Text = $"Saldo en custodia / retenido: ${u.SaldoEnCustodia:F2} MXN";

            if (!string.IsNullOrWhiteSpace(u.ClabeInterbancaria))
                entryClabeRetiro.Text = u.ClabeInterbancaria;

            btnRetirarSaldo.IsEnabled = u.SaldoLiberado > 0;
            btnRetirarSaldo.Text = u.SaldoLiberado > 0
                ? $"🏦 Retirar ${u.SaldoLiberado:F2} MXN vía SPEI"
                : "✅ Sin saldo pendiente por retirar";
        }

        private async void OnRetirarSaldoClicked(object sender, EventArgs e)
        {
            var u = LoginPage.UsuarioActual;
            if (u == null || u.SaldoLiberado <= 0) return;

            string clabe = entryClabeRetiro.Text?.Trim() ?? "";
            if (clabe.Length != 18 || !clabe.All(char.IsDigit))
            {
                await DisplayAlertAsync("CLABE inválida", "Ingresa los 18 dígitos numéricos de tu CLABE interbancaria.", "OK");
                return;
            }

            decimal monto = u.SaldoLiberado;
            bool ok = await _dbService.SolicitarRetiroTecnicoAsync(u.Id, clabe);
            if (ok)
            {
                u.SaldoLiberado = 0m;
                u.ClabeInterbancaria = clabe;
                await CargarDatosSuspensionEnVivoAsync();
                await DisplayAlertAsync("Retiro Procesado", $"Se programó la transferencia SPEI por ${monto:F2} MXN a la CLABE {clabe}.", "Aceptar");
            }
            else
            {
                await DisplayAlertAsync("Error", "No se pudo procesar el retiro en este momento.", "OK");
            }
        }

        private async void OnContactarAdminClicked(object sender, EventArgs e)
        {
            var u = LoginPage.UsuarioActual;
            string msg = $"Hola Administración PHANTEL, soy el técnico {u?.Nombre} ({u?.Email}). Solicito aclaración sobre la suspensión de mi cuenta.";
            try { await Launcher.Default.OpenAsync(new Uri($"https://wa.me/525582393553?text={Uri.EscapeDataString(msg)}")); } catch { }
        }

        private void OnCerrarSesionClicked(object sender, EventArgs e)
        {
            Preferences.Default.Remove("SesionEmail");
            LoginPage.UsuarioActual = null;
            Application.Current!.Windows[0].Page = new NavigationPage(new LoginPage());
        }
    }
}