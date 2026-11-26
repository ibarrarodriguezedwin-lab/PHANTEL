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

            bool esTecnico = u.TipoPerfil == "Técnico";
            lblNombreUsuario.Text = $"{(esTecnico ? "Técnico" : "Cliente")}: {u.Nombre} ({u.Email})";
            lblDescripcionSuspension.Text = esTecnico
                ? "Tu perfil técnico ha sido inhabilitado para recibir o atender nuevos servicios dentro de PHANTEL debido a la siguiente resolución administrativa:"
                : "Tu cuenta de cliente ha sido inhabilitada para solicitar servicios dentro de PHANTEL debido a la siguiente resolución administrativa:";

            string motivo = u.MotivoSuspension;

            if (string.IsNullOrWhiteSpace(motivo) && esTecnico)
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
                ? "Incumplimiento de las políticas de seguridad y convivencia PHANTEL."
                : motivo;

            // Mostrar módulo financiero solo si es Técnico
            cardFinanzasTecnico.IsVisible = esTecnico;

            if (esTecnico)
            {
                lblSaldoDisponible.Text = $"Saldo liberado disponible: ${u.SaldoLiberado:F2} MXN";
                lblSaldoRetenido.Text = $"Saldo en custodia / retenido: ${u.SaldoEnCustodia:F2} MXN";

                if (!string.IsNullOrWhiteSpace(u.ClabeInterbancaria))
                    entryClabeRetiro.Text = u.ClabeInterbancaria;

                btnRetirarSaldo.IsEnabled = u.SaldoLiberado > 0;
                btnRetirarSaldo.Text = u.SaldoLiberado > 0
                    ? $"Retirar ${u.SaldoLiberado:F2} MXN vía SPEI"
                    : "Sin saldo pendiente por retirar";
            }
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

        private void OnContactarAdminTapped(object? sender, TappedEventArgs e)
            => OnContactarAdminClicked(sender ?? this, EventArgs.Empty);

        private async void OnContactarAdminClicked(object sender, EventArgs e)
        {
            var u = LoginPage.UsuarioActual;
            string rol = u?.TipoPerfil == "Técnico" ? "el técnico" : "el cliente";
            string msg = $"Hola Administración PHANTEL, soy {rol} {u?.Nombre} ({u?.Email}). Solicito aclaración sobre la suspensión de mi cuenta.";
            try { await Launcher.Default.OpenAsync(new Uri($"https://wa.me/525582393553?text={Uri.EscapeDataString(msg)}")); } catch { }
        }

        private void OnCerrarSesionTapped(object? sender, TappedEventArgs e)
            => OnCerrarSesionClicked(sender ?? this, EventArgs.Empty);

        private void OnCerrarSesionClicked(object sender, EventArgs e)
        {
            NotificacionRealtimeService.DetenerEscucha();
            Preferences.Default.Remove("SesionEmail");
            LoginPage.UsuarioActual = null;
            Application.Current!.Windows[0].Page = new NavigationPage(new LoginPage());
        }
    }
}