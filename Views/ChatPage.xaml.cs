using System.Collections.ObjectModel;
using System.Globalization;
using PHANTEL.Models;
using PHANTEL.Services;

namespace PHANTEL.Views
{
    public partial class ChatPage : ContentPage
    {
        private SolicitudCita _cita;
        private readonly CloudDatabaseService _dbService = new();
        private readonly ObservableCollection<MensajeChatUi> _mensajes = new();
        private IDisposable? _subscripcionChat;

        // Datos bancarios concentradores de PHANTEL (Custodia Escrow)
        private const string CLABE_PHANTEL = "646180157042891234";
        private const string BANCO_PHANTEL = "STP / Mercado Pago México";
        private const string BENEFICIARIO_PHANTEL = "PHANTEL SERVICES MÉXICO S.A. DE C.V.";
        private const string WHATSAPP_ADMIN = "525582393553";

        public ChatPage(SolicitudCita cita)
        {
            InitializeComponent();
            _cita = cita;
            lblDetalleCita.Text = $"Cita #{_cita.Id} | {_cita.DescripcionFalla}";
            listMensajes.ItemsSource = _mensajes;
            ActualizarTarjetaPresupuestoUI();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            ConectarChatEnVivo();
            await RefrescarEstadoPresupuestoAsync();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _subscripcionChat?.Dispose();
        }

        private async Task RefrescarEstadoPresupuestoAsync()
        {
            var citaActualizada = await _dbService.ObtenerSolicitudPorIdAsync(_cita.Id);
            if (citaActualizada != null)
            {
                _cita = citaActualizada;
            }
            MainThread.BeginInvokeOnMainThread(ActualizarTarjetaPresupuestoUI);
        }

        private void ActualizarTarjetaPresupuestoUI()
        {
            bool esTecnico = LoginPage.UsuarioActual?.TipoPerfil == "Técnico";
            string estadoPago = string.IsNullOrWhiteSpace(_cita.EstadoPago) ? "Sin Cotizar" : _cita.EstadoPago;

            lblMontoTotal.Text = $"${_cita.MontoTotalConTraslado:F2} MXN";
            lblEstadoPago.Text = $"🛡️ Estado: {estadoPago}";

            if (_cita.MontoPresupuesto > 0)
            {
                lblConceptoPresupuesto.Text =
                    $"Concepto: {_cita.ConceptoPresupuesto}\n" +
                    $"Traslado: ${_cita.TarifaTraslado:F2} + Reparación: ${_cita.MontoPresupuesto:F2}";
            }
            else
            {
                lblConceptoPresupuesto.Text = $"Traslado base: ${_cita.TarifaTraslado:F2} MXN (Pendiente cotización de reparación).";
            }

            // Ocultar todos los botones por defecto y activar solo los que corresponden
            btnEnviarPresupuesto.IsVisible = false;
            btnPagarCustodia.IsVisible = false;
            gridAccionesCustodiaCliente.IsVisible = false;

            switch (estadoPago)
            {
                case "Sin Cotizar":
                    cardPresupuesto.Stroke = Color.FromArgb("#007ACC");
                    lblAdvertenciaSeguridad.Text = "🔒 Ningún pago se realiza fuera de PHANTEL. Espera la cotización formal aquí.";
                    lblAdvertenciaSeguridad.TextColor = Color.FromArgb("#B0C4DE");

                    if (esTecnico)
                        btnEnviarPresupuesto.IsVisible = true;
                    break;

                case "Cotizado":
                    cardPresupuesto.Stroke = Color.FromArgb("#28A745");
                    if (esTecnico)
                    {
                        btnEnviarPresupuesto.IsVisible = true;
                        lblAdvertenciaSeguridad.Text = "⏳ Esperando transferencia del cliente a la cuenta protegida de PHANTEL.";
                        lblAdvertenciaSeguridad.TextColor = Color.FromArgb("#FFC107");
                    }
                    else
                    {
                        btnPagarCustodia.IsVisible = true;
                        lblAdvertenciaSeguridad.Text = "🛡️ Paga por transferencia SPEI a PHANTEL. Tu dinero queda retenido hasta que confirmes que el trabajo quedó bien.";
                        lblAdvertenciaSeguridad.TextColor = Color.FromArgb("#28A745");
                    }
                    break;

                case "En Custodia (Retenido)":
                    cardPresupuesto.Stroke = Color.FromArgb("#FFC107");
                    if (esTecnico)
                    {
                        lblAdvertenciaSeguridad.Text =
                            $"⚠️ PAGO EN CUSTODIA (${_cita.MontoTotalConTraslado:F2} MXN): El cliente ya transfirió a PHANTEL (Ref: {_cita.ReferenciaTransferencia}). " +
                            "El dinero está RETENIDO y se te liberará al finalizar sin reportes. Cualquier incumplimiento o fraude conlleva retención, multa y suspensión de cuenta.";
                        lblAdvertenciaSeguridad.TextColor = Color.FromArgb("#FFC107");
                    }
                    else
                    {
                        gridAccionesCustodiaCliente.IsVisible = true;
                        lblAdvertenciaSeguridad.Text =
                            $"🔒 Tu pago (${_cita.MontoTotalConTraslado:F2} MXN) está protegido en custodia. " +
                            "Libéralo solo cuando el técnico termine bien; si hay problema, congélalo con 'Reportar'.";
                        lblAdvertenciaSeguridad.TextColor = Color.FromArgb("#00D2FF");
                    }
                    break;

                case "Liberado al Técnico":
                    cardPresupuesto.Stroke = Color.FromArgb("#198754");
                    lblAdvertenciaSeguridad.Text = "✅ Servicio confirmado sin reportes. Fondos liberados al técnico.";
                    lblAdvertenciaSeguridad.TextColor = Color.FromArgb("#28A745");
                    break;

                case "Fondos Liquidados (Cerrado)":
                case "Reembolsado al Cliente (Cerrado)":
                case "Resolución Parcial":
                case "Reembolsado / Penalizado":
                    cardPresupuesto.Stroke = Color.FromArgb("#00D2FF");
                    lblEstadoPago.Text = $"⚖️ Estado: {estadoPago}";
                    lblAdvertenciaSeguridad.Text = string.IsNullOrWhiteSpace(_cita.DictamenAdmin)
                        ? "⚖️ Caso cerrado y liquidado oficialmente por Administración PHANTEL."
                        : $"⚖️ Dictamen Admin: {_cita.DictamenAdmin}";
                    lblAdvertenciaSeguridad.TextColor = Color.FromArgb("#00D2FF");
                    break;

                case "Retenido por Disputa":
                    cardPresupuesto.Stroke = Color.FromArgb("#DC3545");
                    lblAdvertenciaSeguridad.Text =
                        "🚨 PAGO CONGELADO POR DISPUTA: Administración PHANTEL está auditando el caso para reembolso al cliente y penalización al técnico.";
                    lblAdvertenciaSeguridad.TextColor = Color.FromArgb("#FF6B6B");
                    break;
            }
        }

        // --- 1. TÉCNICO ENVÍA COTIZACIÓN FORMAL ---
        private async void OnEnviarPresupuestoClicked(object sender, EventArgs e)
        {
            if (LoginPage.UsuarioActual == null) return;

            string concepto = await DisplayPromptAsync(
                "Cotización de Servicio",
                "Describe brevemente el trabajo y refacciones incluidas:",
                accept: "Siguiente",
                cancel: "Cancelar",
                placeholder: "Ej. Cambio de centro de carga y mantenimiento");

            if (string.IsNullOrWhiteSpace(concepto)) return;

            string montoTexto = await DisplayPromptAsync(
                "Monto de Reparación (MXN)",
                $"Ingresa el costo del trabajo (se sumará automáticamente el traslado de ${_cita.TarifaTraslado:F2}):",
                accept: "Enviar Cotización",
                cancel: "Cancelar",
                placeholder: "Ej. 450",
                keyboard: Keyboard.Numeric);

            if (string.IsNullOrWhiteSpace(montoTexto)) return;

            if (!decimal.TryParse(montoTexto.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal monto) || monto <= 0)
            {
                await DisplayAlertAsync("Monto inválido", "Ingresa una cantidad numérica mayor a $0.", "OK");
                return;
            }

            bool guardado = await _dbService.EnviarPresupuestoAsync(_cita.Id, concepto.Trim(), monto);
            if (guardado)
            {
                _cita.ConceptoPresupuesto = concepto.Trim();
                _cita.MontoPresupuesto = monto;
                _cita.EstadoPago = "Cotizado";
                ActualizarTarjetaPresupuestoUI();

                string msgSistema =
                    $"💰 COTIZACIÓN FORMAL ENVIADA:\n" +
                    $"• Concepto: {concepto.Trim()}\n" +
                    $"• Mano de obra / refacciones: ${monto:F2} MXN\n" +
                    $"• Tarifa de traslado: ${_cita.TarifaTraslado:F2} MXN\n" +
                    $"• TOTAL PROTEGIDO: ${_cita.MontoTotalConTraslado:F2} MXN\n\n" +
                    $"Puedes realizar tu transferencia protegida desde el botón verde arriba.";

                await EnviarMensajeSistemaAsync(msgSistema);
            }
            else
            {
                await DisplayAlertAsync("Error", "No se pudo guardar el presupuesto en la nube.", "OK");
            }
        }

        // --- 2. CLIENTE REALIZA TRANSFERENCIA EN CUSTODIA (ESCROW) ---
        private async void OnPagarCustodiaClicked(object sender, EventArgs e)
        {
            if (LoginPage.UsuarioActual == null) return;

            string referenciaSugerida = $"PHANTEL-{_cita.Id}";
            string datosTransferencia =
                $"Realiza tu transferencia SPEI por ${_cita.MontoTotalConTraslado:F2} MXN a la cuenta protegida de PHANTEL:\n\n" +
                $"• Banco: {BANCO_PHANTEL}\n" +
                $"• CLABE: {CLABE_PHANTEL}\n" +
                $"• Beneficiario: {BENEFICIARIO_PHANTEL}\n" +
                $"• Concepto / Referencia: {referenciaSugerida}\n\n" +
                $"⚠️ Tu dinero NO se entrega al técnico todavía; queda retenido en garantía hasta que confirmes que el servicio se completó bien.";

            bool continuar = await DisplayAlertAsync(
                "🛡️ Pago Protegido PHANTEL",
                datosTransferencia,
                "Ya transferí / Registrar pago",
                "Cancelar");

            if (!continuar) return;

            string folioRastreo = await DisplayPromptAsync(
                "Confirmación de Transferencia",
                "Ingresa tu clave de rastreo SPEI o los últimos 6 dígitos de referencia:",
                accept: "Continuar",
                cancel: "Cancelar",
                placeholder: referenciaSugerida);

            if (string.IsNullOrWhiteSpace(folioRastreo)) return;

            string comprobanteBase64 = string.Empty;
            bool subirCaptura = await DisplayAlertAsync(
                "Comprobante de Pago",
                "¿Deseas adjuntar la captura de pantalla de tu transferencia SPEI como respaldo?",
                "📎 Adjuntar Captura",
                "Continuar sin imagen");

            if (subirCaptura)
            {
                try
                {
                    var fotos = await MediaPicker.Default.PickPhotosAsync(new MediaPickerOptions
                    {
                        Title = "Selecciona tu comprobante de transferencia"
                    });
                    var foto = fotos?.FirstOrDefault();
                    if (foto != null)
                    {
                        using var stream = await foto.OpenReadAsync();
                        using var ms = new MemoryStream();
                        await stream.CopyToAsync(ms);
                        comprobanteBase64 = Convert.ToBase64String(ms.ToArray());
                    }
                }
                catch { }
            }

            bool registrado = await _dbService.RegistrarPagoEnCustodiaAsync(_cita.Id, folioRastreo.Trim(), comprobanteBase64);
            if (registrado)
            {
                _cita.EstadoPago = "En Custodia (Retenido)";
                _cita.ReferenciaTransferencia = folioRastreo.Trim();
                ActualizarTarjetaPresupuestoUI();

                string advertenciaChat =
                    $"🔒 PAGO EN CUSTODIA REGISTRADO (${_cita.MontoTotalConTraslado:F2} MXN)\n" +
                    $"• Referencia SPEI: {folioRastreo.Trim()}\n" +
                    $"• Estado: RETENIDO POR PHANTEL\n\n" +
                    $"⚠️ AVISO AL TÉCNICO: El pago ha sido asegurado en garantía. Se liberará a tu cuenta únicamente cuando el cliente confirme que el servicio concluyó sin problemas. " +
                    $"En caso de abandono, cobro indebido o mal servicio, el dinero permanecerá retenido para reembolso al cliente y se aplicará penalización/suspensión a tu perfil.";

                await EnviarMensajeSistemaAsync(advertenciaChat);
                await DisplayAlertAsync("¡Pago Protegido!", "Tu transferencia quedó registrada en custodia. El técnico ya recibió la notificación.", "OK");
            }
            else
            {
                await DisplayAlertAsync("Error", "No se pudo registrar el pago en custodia.", "OK");
            }
        }

        // --- 3. CLIENTE LIBERA EL PAGO AL TÉCNICO (TODO SALIÓ BIEN) ---
        private async void OnLiberarPagoClicked(object sender, EventArgs e)
        {
            bool confirmar = await DisplayAlertAsync(
                "✅ Liberar Pago al Técnico",
                $"¿Confirmas que el servicio se realizó correctamente y sin problemas?\n\nSe liberarán ${_cita.MontoTotalConTraslado:F2} MXN a la cuenta del técnico. Esta acción no se puede deshacer.",
                "Sí, liberar dinero",
                "Aún no");

            if (!confirmar) return;

            bool liberado = await _dbService.LiberarPagoATecnicoAsync(_cita.Id);
            if (liberado)
            {
                _cita.EstadoPago = "Liberado al Técnico";
                _cita.Estado = "Finalizada";
                ActualizarTarjetaPresupuestoUI();

                await EnviarMensajeSistemaAsync($"✅ PAGO LIBERADO: El cliente confirmó satisfacción con el servicio. Se liberaron ${_cita.MontoTotalConTraslado:F2} MXN al técnico.");
                await DisplayAlertAsync("¡Listo!", "El pago ha sido liberado al técnico y la cita quedó finalizada.", "OK");
            }
            else
            {
                await DisplayAlertAsync("Aviso", "No se pudo liberar el pago (verifica si la cita no tiene una disputa abierta).", "OK");
            }
        }

        // --- 4. CLIENTE REPORTA PROBLEMA -> RETENCIÓN INMEDIATA + PENALIZACIÓN ---
        private async void OnReportarDisputaChatClicked(object sender, EventArgs e)
        {
            string motivo = await DisplayActionSheetAsync(
                "🚨 Selecciona el motivo del reporte (Retención de Pago):",
                "Cancelar",
                null,
                "El técnico no se presentó",
                "Trabajo incompleto o falla persiste",
                "Intento de cobro por fuera de la app",
                "Daño al equipo o mala conducta");

            if (string.IsNullOrEmpty(motivo) || motivo == "Cancelar") return;

            string detalle = await DisplayPromptAsync(
                "Detalle de la Denuncia",
                "Explica brevemente lo sucedido para que Administración aplique el reembolso y la penalización correspondiente:",
                accept: "Congelar Pago y Denunciar",
                cancel: "Cancelar",
                placeholder: "Describe el problema...");

            if (string.IsNullOrWhiteSpace(detalle)) return;

            string reporteCompleto = $"{motivo} - {detalle.Trim()} ({DateTime.Now:dd/MM/yyyy HH:mm})";
            bool retenido = await _dbService.RetenerPagoPorDisputaAsync(_cita.Id, reporteCompleto);

            if (retenido)
            {
                _cita.EnDisputa = true;
                _cita.EstadoPago = "Retenido por Disputa";
                _cita.MotivoReporte = reporteCompleto;
                ActualizarTarjetaPresupuestoUI();

                await EnviarMensajeSistemaAsync(
                    $"🚨 PAGO CONGELADO POR DISPUTA:\n" +
                    $"• Motivo: {motivo}\n" +
                    $"• Monto retenido: ${_cita.MontoTotalConTraslado:F2} MXN\n" +
                    $"• Acción: Los fondos quedan bloqueados hasta resolución de Administración PHANTEL y aplicación de penalización.");

                bool abrirWhats = await DisplayAlertAsync(
                    "🔒 Dinero Protegido y Retenido",
                    "El pago al técnico ha sido bloqueado inmediatamente. ¿Deseas abrir WhatsApp con un Administrador de PHANTEL para agilizar tu reembolso?",
                    "💬 Contactar Administrador",
                    "Cerrar");

                if (abrirWhats)
                {
                    string textoWhats =
                        $"Hola Administración PHANTEL, abrí una disputa y congelé un pago en custodia:\n\n" +
                        $"• Folio Cita: #{_cita.Id}\n" +
                        $"• Cliente: {LoginPage.UsuarioActual?.Nombre}\n" +
                        $"• Técnico: {_cita.TecnicoEmail}\n" +
                        $"• Monto Retenido: ${_cita.MontoTotalConTraslado:F2} MXN\n" +
                        $"• Referencia SPEI: {_cita.ReferenciaTransferencia}\n" +
                        $"• Motivo: {motivo}\n" +
                        $"• Detalle: {detalle.Trim()}";

                    string url = $"https://wa.me/{WHATSAPP_ADMIN}?text={Uri.EscapeDataString(textoWhats)}";
                    try { await Launcher.Default.OpenAsync(new Uri(url)); } catch { }
                }
            }
        }

        // --- CHAT EN VIVO ---
        private void ConectarChatEnVivo()
        {
            _subscripcionChat = _dbService.EscucharChatEnVivo(_cita.Id).Subscribe(ev =>
            {
                if (ev.Object != null)
                {
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        var msg = ev.Object;
                        msg.Id = ev.Key;

                        int usuarioActualId = LoginPage.UsuarioActual?.Id ?? 0;
                        bool esMio = (msg.EmisorId == usuarioActualId);

                        var msgUi = new MensajeChatUi
                        {
                            Id = msg.Id,
                            EmisorNombre = msg.EmisorNombre,
                            Texto = msg.Texto,
                            FechaHora = msg.FechaHora,
                            EsMio = esMio
                        };

                        if (!_mensajes.Any(m => m.Id == msgUi.Id))
                        {
                            _mensajes.Add(msgUi);
                            listMensajes.ScrollTo(_mensajes.Count - 1);

                            // Si el mensaje nuevo es una cotización, pago, disputa o resolución admin, actualiza la tarjeta superior en vivo
                            if (msg.Texto.StartsWith("💰") || msg.Texto.StartsWith("🔒") || msg.Texto.StartsWith("✅") || msg.Texto.StartsWith("🚨") || msg.Texto.StartsWith("RESOLUCIÓN OFICIAL"))
                            {
                                await RefrescarEstadoPresupuestoAsync();
                            }
                        }
                    });
                }
            });
        }

        private async void OnEnviarMensajeClicked(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(entryMensaje.Text) || LoginPage.UsuarioActual == null)
                return;

            string texto = entryMensaje.Text.Trim();
            entryMensaje.Text = string.Empty;

            var nuevoMensaje = new MensajeChat
            {
                SolicitudId = _cita.Id,
                EmisorId = LoginPage.UsuarioActual.Id,
                EmisorNombre = LoginPage.UsuarioActual.Nombre,
                Texto = texto,
                FechaHora = DateTime.Now,
                EsTecnico = LoginPage.UsuarioActual.TipoPerfil == "Técnico"
            };

            await _dbService.EnviarMensajeChatAsync(nuevoMensaje);
        }

        private async Task EnviarMensajeSistemaAsync(string textoAviso)
        {
            if (LoginPage.UsuarioActual == null) return;

            var aviso = new MensajeChat
            {
                SolicitudId = _cita.Id,
                EmisorId = LoginPage.UsuarioActual.Id,
                EmisorNombre = "🛡️ SISTEMA PHANTEL",
                Texto = textoAviso,
                FechaHora = DateTime.Now,
                EsTecnico = LoginPage.UsuarioActual.TipoPerfil == "Técnico"
            };

            await _dbService.EnviarMensajeChatAsync(aviso);
        }
    }

    public class MensajeChatUi
    {
        public string Id { get; set; } = string.Empty;
        public string EmisorNombre { get; set; } = string.Empty;
        public string Texto { get; set; } = string.Empty;
        public DateTime FechaHora { get; set; }
        public bool EsMio { get; set; }
    }
}