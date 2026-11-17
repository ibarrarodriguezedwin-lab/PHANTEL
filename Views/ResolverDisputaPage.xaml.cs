using System.Collections.ObjectModel;
using System.Globalization;
using PHANTEL.Models;
using PHANTEL.Services;

namespace PHANTEL.Views
{
    public partial class ResolverDisputaPage : ContentPage
    {
        private readonly SolicitudCita _cita;
        private readonly CloudDatabaseService _dbService = new();
        private readonly ObservableCollection<MensajeChat> _mensajes = new();
        private IDisposable? _subscripcionChat;
        private Usuario? _cliente;
        private Usuario? _tecnico;

        public ResolverDisputaPage(SolicitudCita cita)
        {
            InitializeComponent();
            _cita = cita;
            listChatAuditoria.ItemsSource = _mensajes;

            lblFolioExpediente.Text = $"Expediente Cita #{_cita.Id} | {_cita.DescripcionFalla}";
            lblMotivoExpediente.Text = $"Reporte: {_cita.MotivoReporte}";
            lblMontoCustodia.Text = $"Total en Custodia: ${_cita.MontoTotalConTraslado:F2} MXN (Ref: {_cita.ReferenciaTransferencia})";

            entryLiberarTecnico.Text = "0";
            ActualizarCalculoReembolso();
            CargarMultimedia();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await CargarPartesInvolucradasAsync();
            ConectarChatEnVivo();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _subscripcionChat?.Dispose();
        }

        private async Task CargarPartesInvolucradasAsync()
        {
            _cliente = await _dbService.ObtenerUsuarioPorIdAsync(_cita.ClienteId);
            _tecnico = await _dbService.ObtenerUsuarioPorIdAsync(_cita.TecnicoId);

            lblDatosCliente.Text = _cliente != null
                ? $"👤 Cliente: {_cliente.Nombre} | Tel: {_cliente.Telefono} | {_cliente.Email}"
                : $"👤 Cliente ID: {_cita.ClienteId}";

            lblDatosTecnico.Text = _tecnico != null
                ? $"👨‍🔧 Técnico: {_tecnico.Nombre} | Tel: {_tecnico.Telefono} | ⭐ {_tecnico.CalificacionPromedio:F1} (Reportes: {_tecnico.ReportesAcumulados})"
                : $"👨‍🔧 Técnico: {_cita.TecnicoEmail}";
        }

        private void CargarMultimedia()
        {
            bool hayAlgo = false;

            if (!string.IsNullOrWhiteSpace(_cita.EvidenciaRutaImagen) && File.Exists(_cita.EvidenciaRutaImagen))
            {
                imgEvidenciaFalla.Source = ImageSource.FromFile(_cita.EvidenciaRutaImagen);
                imgEvidenciaFalla.IsVisible = true;
                lblTituloFotoFalla.IsVisible = true;
                hayAlgo = true;
            }

            if (!string.IsNullOrWhiteSpace(_cita.ComprobantePagoBase64))
            {
                try
                {
                    string clean = _cita.ComprobantePagoBase64.Contains(",")
                        ? _cita.ComprobantePagoBase64.Split(',')[1]
                        : _cita.ComprobantePagoBase64;
                    byte[] bytes = Convert.FromBase64String(clean.Trim());
                    imgComprobantePago.Source = ImageSource.FromStream(() => new MemoryStream(bytes));
                    imgComprobantePago.IsVisible = true;
                    lblTituloComprobante.IsVisible = true;
                    hayAlgo = true;
                }
                catch { }
            }

            lblSinEvidencias.IsVisible = !hayAlgo;
        }

        private void ConectarChatEnVivo()
        {
            _subscripcionChat = _dbService.EscucharChatEnVivo(_cita.Id).Subscribe(ev =>
            {
                if (ev.Object != null)
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        var msg = ev.Object;
                        msg.Id = ev.Key;
                        if (!_mensajes.Any(m => m.Id == msg.Id))
                        {
                            _mensajes.Add(msg);
                            listChatAuditoria.ScrollTo(_mensajes.Count - 1);
                        }
                    });
                }
            });
        }

        // --- COMUNICACIÓN BILATERAL CON TÉCNICO Y CLIENTE ---
        private async void OnEnviarMensajeAdminClicked(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(entryMensajeAdmin.Text)) return;

            string texto = entryMensajeAdmin.Text.Trim();
            entryMensajeAdmin.Text = string.Empty;

            await _dbService.EnviarMensajeChatAsync(new MensajeChat
            {
                SolicitudId = _cita.Id,
                EmisorId = LoginPage.UsuarioActual?.Id ?? 0,
                EmisorNombre = "⚖️ ADMINISTRACIÓN PHANTEL",
                Texto = texto,
                FechaHora = DateTime.Now,
                EsTecnico = false
            });
        }

        private async void OnCuestionarTecnicoClicked(object sender, EventArgs e)
        {
            await _dbService.EnviarMensajeChatAsync(new MensajeChat
            {
                SolicitudId = _cita.Id,
                EmisorId = LoginPage.UsuarioActual?.Id ?? 0,
                EmisorNombre = "⚖️ ADMINISTRACIÓN PHANTEL",
                Texto = "📢 REQUERIMIENTO AL TÉCNICO: Por favor explica tu versión de los hechos y envía evidencia del trabajo realizado para evaluar la liberación de fondos.",
                FechaHora = DateTime.Now,
                EsTecnico = false
            });
        }

        private async void OnCuestionarClienteClicked(object sender, EventArgs e)
        {
            await _dbService.EnviarMensajeChatAsync(new MensajeChat
            {
                SolicitudId = _cita.Id,
                EmisorId = LoginPage.UsuarioActual?.Id ?? 0,
                EmisorNombre = "⚖️ ADMINISTRACIÓN PHANTEL",
                Texto = "📢 REQUERIMIENTO AL CLIENTE: Por favor detalla y envía evidencia clara de la falla o incumplimiento reportado para validar tu solicitud de reembolso.",
                FechaHora = DateTime.Now,
                EsTecnico = false
            });
        }

        private async void OnContactarClienteWhatsClicked(object sender, EventArgs e)
        {
            if (_cliente == null || string.IsNullOrWhiteSpace(_cliente.Telefono))
            {
                await DisplayAlertAsync("Sin teléfono", "El cliente no tiene teléfono registrado.", "OK");
                return;
            }
            string tel = _cliente.Telefono.StartsWith("52") ? _cliente.Telefono : $"52{_cliente.Telefono.Trim()}";
            string msg = $"Hola {_cliente.Nombre}, te contacta Administración PHANTEL respecto a la disputa en la Cita #{_cita.Id}.";
            try { await Launcher.Default.OpenAsync(new Uri($"https://wa.me/{tel}?text={Uri.EscapeDataString(msg)}")); } catch { }
        }

        private async void OnContactarTecnicoWhatsClicked(object sender, EventArgs e)
        {
            if (_tecnico == null || string.IsNullOrWhiteSpace(_tecnico.Telefono))
            {
                await DisplayAlertAsync("Sin teléfono", "El técnico no tiene teléfono registrado.", "OK");
                return;
            }
            string tel = _tecnico.Telefono.StartsWith("52") ? _tecnico.Telefono : $"52{_tecnico.Telefono.Trim()}";
            string msg = $"Hola {_tecnico.Nombre}, te contacta Administración PHANTEL para auditar el reporte en la Cita #{_cita.Id}.";
            try { await Launcher.Default.OpenAsync(new Uri($"https://wa.me/{tel}?text={Uri.EscapeDataString(msg)}")); } catch { }
        }

        // --- CÁLCULO DE DIVISIÓN DE FONDOS Y DICTAMEN ---
        private void OnPresetTodoTecnicoClicked(object sender, EventArgs e)
        {
            entryLiberarTecnico.Text = _cita.MontoTotalConTraslado.ToString("F2", CultureInfo.InvariantCulture);
            switchPenalizar.IsToggled = false;
            switchSuspender.IsToggled = false;
        }

        private void OnPresetTodoClienteClicked(object sender, EventArgs e)
        {
            entryLiberarTecnico.Text = "0.00";
            switchPenalizar.IsToggled = true;
        }

        private void OnMontoLiberarChanged(object sender, TextChangedEventArgs e) => ActualizarCalculoReembolso();

        private void ActualizarCalculoReembolso()
        {
            decimal total = _cita.MontoTotalConTraslado;
            decimal liberar = 0m;
            decimal.TryParse((entryLiberarTecnico.Text ?? "0").Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out liberar);

            liberar = Math.Clamp(liberar, 0m, total);
            decimal reembolso = Math.Max(0m, total - liberar);

            lblCalculoReembolso.Text = $"Reembolso automático al Cliente: ${reembolso:F2} MXN";
        }

        private async void OnEjecutarResolucionClicked(object sender, EventArgs e)
        {
            decimal total = _cita.MontoTotalConTraslado;
            if (!decimal.TryParse((entryLiberarTecnico.Text ?? "0").Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal liberar)
                || liberar < 0 || liberar > total)
            {
                await DisplayAlertAsync("Monto inválido", $"El monto a liberar debe estar entre $0.00 y ${total:F2} MXN.", "OK");
                return;
            }

            string justificacion = entryJustificacionAdmin.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(justificacion))
            {
                await DisplayAlertAsync("Falta dictamen", "Escribe el motivo oficial de tu resolución antes de liquidar los fondos.", "OK");
                return;
            }

            decimal reembolso = Math.Max(0m, total - liberar);
            bool penalizar = switchPenalizar.IsToggled;
            bool suspender = switchSuspender.IsToggled;

            bool confirmar = await DisplayAlertAsync(
                "⚖️ Confirmar Resolución Final",
                $"• Liberar al Técnico: ${liberar:F2} MXN\n" +
                $"• Reembolsar al Cliente: ${reembolso:F2} MXN\n" +
                $"• Penalizar Técnico: {(penalizar ? "Sí (-0.5 ★)" : "No")}\n" +
                $"• Suspender Cuenta: {(suspender ? "SÍ" : "No")}\n\n" +
                $"¿Deseas ejecutar esta liquidación?",
                "Sí, ejecutar",
                "Cancelar");

            if (!confirmar) return;

            bool ok = await _dbService.ResolverDisputaAdminAsync(_cita.Id, liberar, reembolso, penalizar, suspender, justificacion);
            if (ok)
            {
                await DisplayAlertAsync("Resolución Aplicada", "Los fondos han sido liquidados y ambas partes fueron notificadas en el chat.", "OK");
                await Navigation.PopAsync();
            }
            else
            {
                await DisplayAlertAsync("Error", "No se pudo guardar la resolución en Firebase.", "OK");
            }
        }
    }
}