using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Maui.Views;
using Firebase.Database.Query;
using PHANTEL.Helpers;
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

        private const string CLABE_PHANTEL = "646180157042891234";
        private const string BANCO_PHANTEL = "STP / Mercado Pago México";
        private const string BENEFICIARIO_PHANTEL = "PHANTEL SERVICES MÉXICO S.A. DE C.V.";
        private const string WHATSAPP_ADMIN = "525582393553";

        public ChatPage(SolicitudCita cita)
        {
            InitializeComponent();
            _cita = cita;
            lblDetalleCita.Text = $"Cita #{_cita.Id} · {_cita.DescripcionFalla}";
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
            lblEstadoPago.Text = $"Estado: {estadoPago}";

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

            btnEnviarPresupuesto.IsVisible = false;
            btnPagarCustodia.IsVisible = false;
            gridAccionesCustodiaCliente.IsVisible = false;

            switch (estadoPago)
            {
                case "Sin Cotizar":
                    cardPresupuesto.Stroke = Color.FromArgb("#2C2C2E");
                    lblAdvertenciaSeguridad.Text = "Ningún pago se realiza fuera de PHANTEL. Espera la cotización formal aquí.";
                    lblAdvertenciaSeguridad.TextColor = Color.FromArgb("#8E8E93");

                    if (esTecnico)
                        btnEnviarPresupuesto.IsVisible = true;
                    break;

                case "Cotizado":
                    cardPresupuesto.Stroke = Color.FromArgb("#4DA3E0");
                    if (esTecnico)
                    {
                        btnEnviarPresupuesto.IsVisible = true;
                        lblAdvertenciaSeguridad.Text = "Esperando transferencia del cliente a la cuenta protegida de PHANTEL.";
                        lblAdvertenciaSeguridad.TextColor = Color.FromArgb("#E0A030");
                    }
                    else
                    {
                        btnPagarCustodia.IsVisible = true;
                        lblAdvertenciaSeguridad.Text = "Paga por transferencia SPEI a PHANTEL. Tu dinero queda retenido hasta que confirmes que el trabajo quedó bien.";
                        lblAdvertenciaSeguridad.TextColor = Color.FromArgb("#D1D1D6");
                    }
                    break;

                case "En Custodia (Retenido)":
                    cardPresupuesto.Stroke = Color.FromArgb("#E0A030");
                    if (esTecnico)
                    {
                        lblAdvertenciaSeguridad.Text =
                            $"PAGO EN CUSTODIA (${_cita.MontoTotalConTraslado:F2} MXN): El cliente ya transfirió a PHANTEL (Ref: {_cita.ReferenciaTransferencia}). " +
                            "Al finalizar sin reportes recibirás tu traslado íntegro + tu trabajo menos 3% de comisión. En disputa procedente aplica 5% de penalización.";
                        lblAdvertenciaSeguridad.TextColor = Color.FromArgb("#E0A030");
                    }
                    else
                    {
                        gridAccionesCustodiaCliente.IsVisible = true;
                        lblAdvertenciaSeguridad.Text =
                            $"Tu pago (${_cita.MontoTotalConTraslado:F2} MXN) está protegido en custodia. " +
                            "Libéralo solo cuando el técnico termine bien; si hay problema, congélalo con 'Reportar'.";
                        lblAdvertenciaSeguridad.TextColor = Color.FromArgb("#4DA3E0");
                    }
                    break;

                case "Liberado al Técnico":
                    cardPresupuesto.Stroke = Color.FromArgb("#30D158");
                    lblAdvertenciaSeguridad.Text = "Servicio confirmado sin reportes. Fondos liberados al técnico.";
                    lblAdvertenciaSeguridad.TextColor = Color.FromArgb("#30D158");
                    break;

                case "Fondos Liquidados (Cerrado)":
                case "Reembolsado al Cliente (Cerrado)":
                case "Resolución Parcial":
                case "Reembolsado / Penalizado":
                    cardPresupuesto.Stroke = Color.FromArgb("#4DA3E0");
                    lblEstadoPago.Text = $"Estado: {estadoPago}";
                    lblAdvertenciaSeguridad.Text = string.IsNullOrWhiteSpace(_cita.DictamenAdmin)
                        ? "Caso cerrado y liquidado oficialmente por Administración PHANTEL."
                        : $"Dictamen Admin: {_cita.DictamenAdmin}";
                    lblAdvertenciaSeguridad.TextColor = Color.FromArgb("#4DA3E0");
                    break;

                case "Retenido por Disputa":
                    cardPresupuesto.Stroke = Color.FromArgb("#FF6B6B");
                    lblAdvertenciaSeguridad.Text =
                        "PAGO CONGELADO POR DISPUTA: Administración PHANTEL está auditando el caso para reembolso al cliente y penalización al técnico.";
                    lblAdvertenciaSeguridad.TextColor = Color.FromArgb("#FF6B6B");
                    break;
            }
        }

        // --- 1. TÉCNICO ENVÍA COTIZACIÓN FORMAL ---
        private void OnEnviarPresupuestoTapped(object? sender, TappedEventArgs e)
            => OnEnviarPresupuestoClicked(sender ?? this, EventArgs.Empty);

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
                $"Ingresa el costo de tu trabajo (se sumará automáticamente tu traslado íntegro de ${_cita.TarifaTraslado:F2}):",
                accept: "Revisar Comisión",
                cancel: "Cancelar",
                placeholder: "Ej. 450",
                keyboard: Keyboard.Numeric);

            if (string.IsNullOrWhiteSpace(montoTexto)) return;

            if (!decimal.TryParse(montoTexto.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal monto) || monto <= 0)
            {
                await DisplayAlertAsync("Monto inválido", "Ingresa una cantidad numérica mayor a $0.", "OK");
                return;
            }

            decimal totalServicio = monto + _cita.TarifaTraslado;
            decimal comisionPhantel = Math.Round(monto * 0.03m, 2);
            decimal netoTotalTecnico = (monto - comisionPhantel) + _cita.TarifaTraslado;

            bool confirmarEnvio = await DisplayAlertAsync(
                "Aviso de Comisión PHANTEL (3%)",
                $"Antes de enviar tu cotización formal, recuerda cómo se aplica la comisión:\n\n" +
                $"• Presupuesto por tu trabajo: ${monto:F2} MXN\n" +
                $"• Comisión PHANTEL (3% solo sobre tu trabajo): -${comisionPhantel:F2} MXN\n" +
                $"• Traslado estimado (100% libre de comisión): +${_cita.TarifaTraslado:F2} MXN\n" +
                $"• Tu ganancia neta al aceptar y liberar el cliente: ${netoTotalTecnico:F2} MXN\n\n" +
                $"Nota: En caso de disputa procedente aplica una penalización del 5% sobre el presupuesto.\n\n" +
                $"¿Deseas enviar esta cotización al cliente?",
                "Aceptar y Enviar",
                "Cancelar");

            if (!confirmarEnvio) return;

            bool guardado = await _dbService.EnviarPresupuestoAsync(_cita.Id, concepto.Trim(), monto);
            if (guardado)
            {
                _cita.ConceptoPresupuesto = concepto.Trim();
                _cita.MontoPresupuesto = monto;
                _cita.PorcentajeComisionAplicado = 0.03m;
                _cita.MontoComisionPhantel = comisionPhantel;
                _cita.MontoNetoTecnico = netoTotalTecnico;
                _cita.EstadoPago = "Cotizado";
                ActualizarTarjetaPresupuestoUI();

                string msgSistema =
                    $"COTIZACIÓN FORMAL ENVIADA:\n" +
                    $"• Concepto: {concepto.Trim()}\n" +
                    $"• Mano de obra / refacciones: ${monto:F2} MXN\n" +
                    $"• Tarifa de traslado: ${_cita.TarifaTraslado:F2} MXN\n" +
                    $"• TOTAL PROTEGIDO: ${totalServicio:F2} MXN\n\n" +
                    $"Puedes realizar tu transferencia protegida desde el botón superior.";

                await EnviarMensajeSistemaAsync(msgSistema);
            }
            else
            {
                await DisplayAlertAsync("Error", "No se pudo guardar el presupuesto en la nube.", "OK");
            }
        }

        // --- 2. CLIENTE REALIZA TRANSFERENCIA EN CUSTODIA ---
        private void OnPagarCustodiaTapped(object? sender, TappedEventArgs e)
            => OnPagarCustodiaClicked(sender ?? this, EventArgs.Empty);

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
                $"Tu dinero NO se entrega al técnico todavía; queda retenido en garantía hasta que confirmes que el servicio se completó bien.";

            bool continuar = await DisplayAlertAsync(
                "Pago Protegido PHANTEL",
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
                "Adjuntar Captura",
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
                        comprobanteBase64 = await ImageHelper.ComprimirImagenABase64Async(
                            foto,
                            maxAncho: 700f,
                            calidad: 0.65f);
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
                    $"PAGO EN CUSTODIA REGISTRADO (${_cita.MontoTotalConTraslado:F2} MXN)\n" +
                    $"• Referencia SPEI: {folioRastreo.Trim()}\n" +
                    $"• Estado: RETENIDO POR PHANTEL\n\n" +
                    $"AVISO AL TÉCNICO: El pago ha sido asegurado en garantía. Se liberará a tu cuenta cuando el cliente confirme el trabajo (traslado íntegro + trabajo menos 3% de comisión). " +
                    $"En disputa procedente aplica penalización del 5% sobre el presupuesto y/o suspensión.";

                await EnviarMensajeSistemaAsync(advertenciaChat);
                await DisplayAlertAsync("Pago Protegido", "Tu transferencia quedó registrada en custodia. El técnico ya recibió la notificación.", "OK");
            }
            else
            {
                await DisplayAlertAsync("Error", "No se pudo registrar el pago en custodia.", "OK");
            }
        }

        // --- 3. CLIENTE LIBERA EL PAGO AL TÉCNICO ---
        private void OnLiberarPagoTapped(object? sender, TappedEventArgs e)
            => OnLiberarPagoClicked(sender ?? this, EventArgs.Empty);

        private async void OnLiberarPagoClicked(object sender, EventArgs e)
        {
            bool confirmar = await DisplayAlertAsync(
                "Liberar Pago al Técnico",
                $"¿Confirmas que el servicio se realizó correctamente y sin problemas?\n\nSe liberarán ${_cita.MontoTotalConTraslado:F2} MXN (traslado íntegro + trabajo menos 3% de comisión) al técnico. Esta acción no se puede deshacer.",
                "Sí, liberar dinero",
                "Aún no");

            if (!confirmar) return;

            bool liberado = await _dbService.LiberarPagoATecnicoAsync(_cita.Id);
            if (liberado)
            {
                decimal comision = Math.Round(_cita.MontoPresupuesto * 0.03m, 2);
                decimal neto = (_cita.MontoPresupuesto - comision) + _cita.TarifaTraslado;

                _cita.EstadoPago = "Liberado al Técnico";
                _cita.Estado = "Finalizada";
                ActualizarTarjetaPresupuestoUI();

                await EnviarMensajeSistemaAsync(
                    $"PAGO LIBERADO: El cliente confirmó satisfacción con el servicio.\n" +
                    $"• Traslado (100% íntegro): ${_cita.TarifaTraslado:F2} MXN\n" +
                    $"• Presupuesto de trabajo: ${_cita.MontoPresupuesto:F2} MXN\n" +
                    $"• Comisión PHANTEL (3% sobre trabajo): -${comision:F2} MXN\n" +
                    $"• Neto liberado al técnico: ${neto:F2} MXN");

                await DisplayAlertAsync("Listo", "El pago ha sido liberado al técnico y la cita quedó finalizada.", "OK");
            }
            else
            {
                await DisplayAlertAsync("Aviso", "No se pudo liberar el pago (verifica si la cita no tiene una disputa abierta).", "OK");
            }
        }

        // --- 4. CLIENTE REPORTA PROBLEMA ---
        private void OnReportarDisputaChatTapped(object? sender, TappedEventArgs e)
            => OnReportarDisputaChatClicked(sender ?? this, EventArgs.Empty);

        private async void OnReportarDisputaChatClicked(object sender, EventArgs e)
        {
            string motivo = await DisplayActionSheetAsync(
                "Selecciona el motivo del reporte (Retención de Pago):",
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
                    $"PAGO CONGELADO POR DISPUTA:\n" +
                    $"• Motivo: {motivo}\n" +
                    $"• Monto retenido: ${_cita.MontoTotalConTraslado:F2} MXN\n" +
                    $"• Acción: Los fondos quedan bloqueados hasta resolución de Administración PHANTEL (aplica penalización del 5% en disputas procedentes).");

                bool abrirWhats = await DisplayAlertAsync(
                    "Dinero Protegido y Retenido",
                    "El pago al técnico ha sido bloqueado inmediatamente. ¿Deseas abrir WhatsApp con un Administrador de PHANTEL para agilizar tu reembolso?",
                    "Contactar Administrador",
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

        // --- 5. ADJUNTAR FOTOS O VIDEOS EN EL CHAT ---
        private async void OnAdjuntarMultimediaTapped(object? sender, TappedEventArgs e)
        {
            if (LoginPage.UsuarioActual == null) return;

            string opcion = await DisplayActionSheetAsync(
                "Adjuntar evidencia en el chat",
                "Cancelar",
                null,
                "Tomar Foto",
                "Elegir Foto de Galería",
                "Grabar Video",
                "Elegir Video de Galería");

            if (string.IsNullOrEmpty(opcion) || opcion == "Cancelar") return;

            try
            {
                if (opcion == "Tomar Foto")
                {
                    var foto = await MediaPicker.Default.CapturePhotoAsync();
                    if (foto != null) await EnviarFotoChatAsync(foto);
                }
                else if (opcion == "Elegir Foto de Galería")
                {
                    var fotos = await MediaPicker.Default.PickPhotosAsync(new MediaPickerOptions { Title = "Selecciona una foto" });
                    var foto = fotos?.FirstOrDefault();
                    if (foto != null) await EnviarFotoChatAsync(foto);
                }
                else if (opcion == "Grabar Video")
                {
                    var video = await MediaPicker.Default.CaptureVideoAsync();
                    if (video != null) await EnviarVideoChatAsync(video);
                }
                else if (opcion == "Elegir Video de Galería")
                {
                    var videos = await MediaPicker.Default.PickVideosAsync(new MediaPickerOptions { Title = "Selecciona un video" });
                    var video = videos?.FirstOrDefault();
                    if (video != null) await EnviarVideoChatAsync(video);
                }
            }
            catch (Exception ex)
            {
                await DisplayAlertAsync("Multimedia", $"No se pudo procesar el archivo: {ex.Message}", "OK");
            }
        }

        private async Task EnviarFotoChatAsync(FileResult foto)
        {
            if (LoginPage.UsuarioActual == null) return;

            string base64 = await ImageHelper.ComprimirImagenABase64Async(foto, maxAncho: 720f, calidad: 0.65f);
            if (string.IsNullOrEmpty(base64))
            {
                await DisplayAlertAsync("Error", "No se pudo comprimir la imagen.", "OK");
                return;
            }

            var nuevoMensaje = new MensajeChat
            {
                SolicitudId = _cita.Id,
                EmisorId = LoginPage.UsuarioActual.Id,
                EmisorNombre = LoginPage.UsuarioActual.Nombre,
                Texto = entryMensaje.Text?.Trim() ?? string.Empty,
                ImagenBase64 = base64,
                FechaHora = DateTime.Now,
                EsTecnico = LoginPage.UsuarioActual.TipoPerfil == "Técnico",
                Entregado = true,
                Leido = false
            };

            entryMensaje.Text = string.Empty;
            await _dbService.EnviarMensajeChatAsync(nuevoMensaje);
        }

        private async Task EnviarVideoChatAsync(FileResult video)
        {
            if (LoginPage.UsuarioActual == null) return;

            using var stream = await video.OpenReadAsync();
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            byte[] bytes = ms.ToArray();

            if (bytes.Length > 7 * 1024 * 1024)
            {
                await DisplayAlertAsync("Video demasiado pesado", "El video supera los 7 MB permitidos. Graba un clip más corto para enviarlo en el chat.", "OK");
                return;
            }

            var nuevoMensaje = new MensajeChat
            {
                SolicitudId = _cita.Id,
                EmisorId = LoginPage.UsuarioActual.Id,
                EmisorNombre = LoginPage.UsuarioActual.Nombre,
                Texto = entryMensaje.Text?.Trim() ?? string.Empty,
                VideoBase64 = Convert.ToBase64String(bytes),
                NombreArchivoVideo = string.IsNullOrWhiteSpace(video.FileName) ? "video_chat.mp4" : video.FileName,
                FechaHora = DateTime.Now,
                EsTecnico = LoginPage.UsuarioActual.TipoPerfil == "Técnico",
                Entregado = true,
                Leido = false
            };

            entryMensaje.Text = string.Empty;
            await _dbService.EnviarMensajeChatAsync(nuevoMensaje);
        }

        // --- VISOR DE FOTO Y REPRODUCTOR DE VIDEO ADJUNTOS ---
        private async void OnVerFotoAdjuntaTapped(object? sender, TappedEventArgs e)
        {
            if (e.Parameter is not MensajeChatUi msg || string.IsNullOrEmpty(msg.ImagenBase64)) return;

            byte[] bytes = Convert.FromBase64String(msg.ImagenBase64);
            var img = new Image
            {
                Source = ImageSource.FromStream(() => new MemoryStream(bytes)),
                Aspect = Aspect.AspectFit,
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill
            };

            var btnCerrar = new Button
            {
                Text = "Cerrar",
                BackgroundColor = Color.FromArgb("#F2F2F7"),
                TextColor = Color.FromArgb("#121212"),
                FontAttributes = FontAttributes.Bold,
                CornerRadius = 12,
                Margin = new Thickness(20, 10, 20, 24),
                VerticalOptions = LayoutOptions.End
            };

            var modal = new ContentPage
            {
                BackgroundColor = Color.FromArgb("#0A0A0C"),
                Content = new Grid
                {
                    Children = { img, btnCerrar }
                }
            };

            btnCerrar.Clicked += async (_, _) => await Navigation.PopModalAsync();
            await Navigation.PushModalAsync(modal);
        }

        private async void OnReproducirVideoAdjuntoTapped(object? sender, TappedEventArgs e)
        {
            if (e.Parameter is not MensajeChatUi msg || string.IsNullOrEmpty(msg.VideoBase64)) return;

            try
            {
                string rutaTemp = Path.Combine(FileSystem.CacheDirectory, $"chat_vid_{msg.Id}.mp4");
                if (!File.Exists(rutaTemp))
                {
                    byte[] bytes = Convert.FromBase64String(msg.VideoBase64);
                    await File.WriteAllBytesAsync(rutaTemp, bytes);
                }

                var player = new MediaElement
                {
                    Source = MediaSource.FromFile(rutaTemp),
                    ShouldAutoPlay = true,
                    ShouldShowPlaybackControls = true,
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Fill
                };

                var btnCerrar = new Button
                {
                    Text = "Cerrar video",
                    BackgroundColor = Color.FromArgb("#F2F2F7"),
                    TextColor = Color.FromArgb("#121212"),
                    FontAttributes = FontAttributes.Bold,
                    CornerRadius = 12,
                    Margin = new Thickness(20, 12),
                    VerticalOptions = LayoutOptions.Start,
                    HorizontalOptions = LayoutOptions.End
                };

                var modal = new ContentPage
                {
                    BackgroundColor = Color.FromArgb("#0A0A0C"),
                    Content = new Grid
                    {
                        Children = { player, btnCerrar }
                    }
                };

                btnCerrar.Clicked += async (_, _) =>
                {
                    player.Stop();
                    await Navigation.PopModalAsync();
                };

                await Navigation.PushModalAsync(modal);
            }
            catch (Exception ex)
            {
                await DisplayAlertAsync("Video", $"No se pudo reproducir el video: {ex.Message}", "OK");
            }
        }

        // --- CHAT EN VIVO Y CONFIRMACIÓN DE LECTURA ---
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
                        bool esSistema = msg.EmisorNombre.Contains("SISTEMA PHANTEL", StringComparison.OrdinalIgnoreCase);

                        // Si el mensaje lo envió la otra persona y aún no estaba leído, marcarlo como leído en Firebase
                        if (!esMio && !msg.Leido && !string.IsNullOrEmpty(msg.Id))
                        {
                            msg.Leido = true;
                            _ = MarcarMensajeComoLeidoAsync(_cita.Id, msg.Id);
                        }

                        var existente = _mensajes.FirstOrDefault(m => m.Id == msg.Id);
                        if (existente != null)
                        {
                            // Actualizar estado de leído en tiempo real
                            if (existente.Leido != msg.Leido || existente.Entregado != msg.Entregado)
                            {
                                existente.Entregado = msg.Entregado;
                                existente.Leido = msg.Leido;
                                int idx = _mensajes.IndexOf(existente);
                                _mensajes[idx] = existente;
                            }
                            return;
                        }

                        var msgUi = new MensajeChatUi
                        {
                            Id = msg.Id,
                            EmisorNombre = esSistema ? "SISTEMA PHANTEL" : msg.EmisorNombre,
                            Texto = msg.Texto,
                            ImagenBase64 = msg.ImagenBase64,
                            VideoBase64 = msg.VideoBase64,
                            NombreArchivoVideo = msg.NombreArchivoVideo,
                            FechaHora = msg.FechaHora,
                            EsMio = esMio,
                            EsSistema = esSistema,
                            Entregado = msg.Entregado,
                            Leido = msg.Leido
                        };

                        _mensajes.Add(msgUi);
                        listMensajes.ScrollTo(_mensajes.Count - 1);

                        if (esSistema)
                        {
                            await RefrescarEstadoPresupuestoAsync();
                        }
                    });
                }
            });
        }

        private async Task MarcarMensajeComoLeidoAsync(int solicitudId, string mensajeId)
        {
            try
            {
                var firebase = new Firebase.Database.FirebaseClient("https://phantel-app-default-rtdb.firebaseio.com/");
                await firebase
                    .Child("chats")
                    .Child(solicitudId.ToString())
                    .Child(mensajeId)
                    .Child("Leido")
                    .PutAsync(true);
            }
            catch { }
        }

        private void OnEnviarMensajeTapped(object? sender, TappedEventArgs e)
            => OnEnviarMensajeClicked(sender ?? this, EventArgs.Empty);

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
                EsTecnico = LoginPage.UsuarioActual.TipoPerfil == "Técnico",
                Entregado = true,
                Leido = false
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
                EmisorNombre = "SISTEMA PHANTEL",
                Texto = textoAviso,
                FechaHora = DateTime.Now,
                EsTecnico = LoginPage.UsuarioActual.TipoPerfil == "Técnico",
                Entregado = true,
                Leido = false
            };

            await _dbService.EnviarMensajeChatAsync(aviso);
        }
    }

    public class MensajeChatUi
    {
        public string Id { get; set; } = string.Empty;
        public string EmisorNombre { get; set; } = string.Empty;
        public string Texto { get; set; } = string.Empty;
        public string ImagenBase64 { get; set; } = string.Empty;
        public string VideoBase64 { get; set; } = string.Empty;
        public string NombreArchivoVideo { get; set; } = string.Empty;
        public DateTime FechaHora { get; set; }
        public bool EsMio { get; set; }
        public bool EsSistema { get; set; }
        public bool Entregado { get; set; } = true;
        public bool Leido { get; set; }

        public bool TieneTexto => !string.IsNullOrWhiteSpace(Texto);
        public bool TieneImagen => !string.IsNullOrWhiteSpace(ImagenBase64);
        public bool TieneVideo => !string.IsNullOrWhiteSpace(VideoBase64);
        public string EtiquetaVideo => string.IsNullOrWhiteSpace(NombreArchivoVideo) ? "Ver video adjunto" : $"Video · {NombreArchivoVideo}";

        // Estilo de burbujas:
        // - Enviado (EsMio): Fondo Negro (#0A0A0C), borde sutil (#2C2C2E), letras Blancas (#F2F2F7)
        // - Sistema: Fondo Gris Oscuro (#1C1C1E), borde azul (#4DA3E0), letras Blancas
        // - Recibido (Otro): Fondo Blanco (#F2F2F7), borde blanco, letras Negras (#121212)
        public Color ColorFondo => EsSistema
            ? Color.FromArgb("#1C1C1E")
            : (EsMio ? Color.FromArgb("#0A0A0C") : Color.FromArgb("#F2F2F7"));

        public Color ColorBorde => EsSistema
            ? Color.FromArgb("#4DA3E0")
            : (EsMio ? Color.FromArgb("#2C2C2E") : Color.FromArgb("#F2F2F7"));

        public Color ColorTexto => (EsSistema || EsMio)
            ? Color.FromArgb("#F2F2F7")
            : Color.FromArgb("#121212");

        public Color ColorNombre => EsSistema
            ? Color.FromArgb("#4DA3E0")
            : Color.FromArgb("#5A5A5F");

        public Color ColorHora => (EsSistema || EsMio)
            ? Color.FromArgb("#8E8E93")
            : Color.FromArgb("#6E6E73");

        public bool MostrarNombreEmisor => !EsMio || EsSistema;

        // Confirmaciones de entrega / lectura
        public string TextoCheck => Leido ? "✓✓" : (Entregado ? "✓✓" : "✓");
        public Color ColorCheck => Leido ? Color.FromArgb("#00D2FF") : Color.FromArgb("#8E8E93");
    }
}