using System.Reactive.Linq;
using Firebase.Database;
using Firebase.Database.Query;
using PHANTEL.Models;
using Plugin.LocalNotification;
using Plugin.LocalNotification.Core.Models;

namespace PHANTEL.Services
{
    public static class NotificacionRealtimeService
    {
        private static readonly FirebaseClient _firebase = new("https://phantel-enterprises-default-rtdb.firebaseio.com/");
        private static readonly List<IDisposable> _suscripciones = new();
        private static readonly HashSet<int> _chatsSuscritos = new();
        private static readonly HashSet<string> _eventosNotificados = new();
        private static Usuario? _usuarioActual;
        private static int _contadorNotificacionId = 1000;
        private static bool _cargaInicialCompletada = false;

        public static async Task IniciarEscuchaAsync(Usuario usuario)
        {
            DetenerEscucha();
            if (usuario == null) return;

            _usuarioActual = usuario;
            _cargaInicialCompletada = false;

            if (await LocalNotificationCenter.Current.AreNotificationsEnabled() == false)
            {
                await LocalNotificationCenter.Current.RequestNotificationPermission();
            }

            // Cargar estado actual para no disparar notificaciones viejas al iniciar sesión
            var cloud = new CloudDatabaseService();
            var citasExistentes = usuario.TipoPerfil == "Admin"
                ? await cloud.ObtenerDisputasActivasAsync()
                : await cloud.ObtenerSolicitudesPorUsuarioAsync(usuario.Id, usuario.TipoPerfil == "Técnico");

            foreach (var c in citasExistentes)
            {
                RegistrarEstadosPrevios(c);
                SuscribirChatDeCita(c.Id);
            }

            _cargaInicialCompletada = true;

            // 1. ESCUCHAR SOLICITUDES EN TIEMPO REAL
            var subSolicitudes = _firebase
                .Child("Solicitudes")
                .AsObservable<SolicitudCita>()
                .Where(e => e.Object != null)
                .Subscribe(evento =>
                {
                    var cita = evento.Object;
                    if (cita.Id == 0 && !string.IsNullOrEmpty(evento.Key))
                        cita.Id = Math.Abs(evento.Key.GetHashCode());

                    if (_usuarioActual == null) return;
                    string emailNorm = _usuarioActual.Email.Trim().ToLower();

                    bool esSuTecnico = (_usuarioActual.Id != 0 && cita.TecnicoId == _usuarioActual.Id)
                                       || (!string.IsNullOrWhiteSpace(cita.TecnicoEmail) && cita.TecnicoEmail.Trim().ToLower() == emailNorm);

                    bool esSuCliente = _usuarioActual.Id != 0 && cita.ClienteId == _usuarioActual.Id;

                    // Suscribir automáticamente el chat de cualquier cita que le pertenezca
                    if (_usuarioActual.TipoPerfil == "Admin" || esSuTecnico || esSuCliente)
                    {
                        SuscribirChatDeCita(cita.Id);
                    }

                    if (!_cargaInicialCompletada) return;

                    // --- A) NOTIFICACIONES PARA EL TÉCNICO ---
                    if (_usuarioActual.TipoPerfil == "Técnico" && esSuTecnico)
                    {
                        if (cita.Estado == "Pendiente")
                        {
                            LanzarNotificacionUnica(
                                $"tec_nueva_{cita.Id}",
                                "🔧 Nueva Solicitud de Trabajo",
                                $"Domicilio: {cita.DireccionCliente} • Traslado: ${cita.TarifaTraslado:F2} MXN");
                        }

                        if (cita.EstadoPago == "En Custodia (Retenido)")
                        {
                            LanzarNotificacionUnica(
                                $"tec_pago_custodia_{cita.Id}",
                                "💳 ¡Cliente Pagó en Custodia!",
                                $"Cita #{cita.Id}: Fondos asegurados (${cita.MontoTotalConTraslado:F2} MXN). Ya puedes realizar el servicio.");
                        }

                        if (cita.EstadoPago == "Liberado al Técnico")
                        {
                            LanzarNotificacionUnica(
                                $"tec_pago_liberado_{cita.Id}",
                                "🎉 ¡Pago Liberado a tu Saldo!",
                                $"Se acreditaron ${cita.MontoNetoTecnico:F2} MXN netos por la cita #{cita.Id}.");
                        }
                    }

                    // --- B) NOTIFICACIONES PARA EL CLIENTE ---
                    if (_usuarioActual.TipoPerfil == "Cliente" && esSuCliente)
                    {
                        if (cita.Estado == "Aceptada")
                        {
                            LanzarNotificacionUnica(
                                $"cli_aceptada_{cita.Id}",
                                "✅ Técnico en Camino / Cita Aceptada",
                                $"Tu solicitud #{cita.Id} fue aceptada. Ya puedes chatear con el técnico.");
                        }

                        if (cita.EstadoPago == "Cotizado" && cita.MontoPresupuesto > 0)
                        {
                            LanzarNotificacionUnica(
                                $"cli_presupuesto_{cita.Id}_{cita.MontoPresupuesto}",
                                "💰 Nuevo Presupuesto Recibido",
                                $"{cita.ConceptoPresupuesto}: ${cita.MontoPresupuesto:F2} MXN (+ traslado). Entra al chat para pagar en custodia.");
                        }

                        if (cita.Estado == "Finalizada")
                        {
                            LanzarNotificacionUnica(
                                $"cli_finalizada_{cita.Id}",
                                "🏁 Servicio Finalizado",
                                $"El técnico concluyó la cita #{cita.Id}. Confirma y libera el pago si todo quedó bien.");
                        }
                    }

                    // --- C) NOTIFICACIONES PARA EL ADMINISTRADOR ---
                    if (_usuarioActual.TipoPerfil == "Admin" && (cita.EnDisputa || cita.EstadoPago == "Retenido por Disputa"))
                    {
                        LanzarNotificacionUnica(
                            $"admin_disputa_{cita.Id}",
                            "🚨 Nueva Disputa / Reporte PHANTEL",
                            $"Cita #{cita.Id} reportada. Fondos retenidos en custodia para dictamen.");
                    }
                });

            _suscripciones.Add(subSolicitudes);

            // 2. PARA ADMINISTRADORES: Escuchar verificaciones KYC (INE enviada)
            if (usuario.TipoPerfil == "Admin")
            {
                var subKyc = _firebase
                    .Child("Usuarios")
                    .AsObservable<Usuario>()
                    .Where(e => e.Object != null && e.Object.TipoPerfil == "Técnico" && e.Object.EstadoVerificacion == "En Revisión")
                    .Subscribe(evento =>
                    {
                        if (!_cargaInicialCompletada) return;
                        var tec = evento.Object;
                        LanzarNotificacionUnica(
                            $"admin_kyc_{tec.Email.Trim().ToLower()}",
                            "🪪 Nueva Verificación KYC Pendiente",
                            $"{tec.Nombre} ({tec.Especialidad}) envió su INE para revisión.");
                    });

                _suscripciones.Add(subKyc);
            }
        }

        private static void RegistrarEstadosPrevios(SolicitudCita cita)
        {
            lock (_eventosNotificados)
            {
                _eventosNotificados.Add($"tec_nueva_{cita.Id}");
                if (cita.EstadoPago == "En Custodia (Retenido)")
                    _eventosNotificados.Add($"tec_pago_custodia_{cita.Id}");
                if (cita.EstadoPago == "Liberado al Técnico")
                    _eventosNotificados.Add($"tec_pago_liberado_{cita.Id}");
                if (cita.Estado == "Aceptada")
                    _eventosNotificados.Add($"cli_aceptada_{cita.Id}");
                if (cita.EstadoPago == "Cotizado" && cita.MontoPresupuesto > 0)
                    _eventosNotificados.Add($"cli_presupuesto_{cita.Id}_{cita.MontoPresupuesto}");
                if (cita.Estado == "Finalizada")
                    _eventosNotificados.Add($"cli_finalizada_{cita.Id}");
                if (cita.EnDisputa || cita.EstadoPago == "Retenido por Disputa")
                    _eventosNotificados.Add($"admin_disputa_{cita.Id}");
            }
        }

        public static void SuscribirChatDeCita(int solicitudId)
        {
            if (solicitudId == 0 || _usuarioActual == null) return;

            lock (_chatsSuscritos)
            {
                if (_chatsSuscritos.Contains(solicitudId)) return;
                _chatsSuscritos.Add(solicitudId);
            }

            DateTime momentoSuscripcion = DateTime.Now.AddSeconds(-3);

            var subChat = _firebase
                .Child("Chats")
                .Child(solicitudId.ToString())
                .AsObservable<MensajeChat>()
                .Where(e => e.Object != null)
                .Subscribe(evento =>
                {
                    var msg = evento.Object;
                    if (_usuarioActual == null) return;

                    // Ignorar mensajes enviados por mí mismo
                    if (msg.EmisorId == _usuarioActual.Id) return;

                    // Ignorar historial viejo al abrir la app
                    if (msg.FechaHora < momentoSuscripcion) return;

                    string claveMsg = $"chat_{solicitudId}_{evento.Key}";
                    string cuerpo = string.IsNullOrWhiteSpace(msg.Texto)
                        ? "📎 Archivo multimedia adjunto"
                        : msg.Texto;

                    LanzarNotificacionUnica(
                        claveMsg,
                        $"💬 {msg.EmisorNombre} (Cita #{solicitudId})",
                        cuerpo);
                });

            lock (_suscripciones)
            {
                _suscripciones.Add(subChat);
            }
        }

        public static void DetenerEscucha()
        {
            lock (_suscripciones)
            {
                foreach (var sub in _suscripciones)
                {
                    try { sub.Dispose(); } catch { }
                }
                _suscripciones.Clear();
            }

            lock (_chatsSuscritos)
            {
                _chatsSuscritos.Clear();
            }

            lock (_eventosNotificados)
            {
                _eventosNotificados.Clear();
            }

            _usuarioActual = null;
            _cargaInicialCompletada = false;
        }

        private static void LanzarNotificacionUnica(string claveUnica, string titulo, string descripcion)
        {
            lock (_eventosNotificados)
            {
                if (_eventosNotificados.Contains(claveUnica)) return;
                _eventosNotificados.Add(claveUnica);
            }

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try
                {
                    var request = new NotificationRequest
                    {
                        NotificationId = Interlocked.Increment(ref _contadorNotificacionId),
                        Title = titulo,
                        Description = descripcion,
                        BadgeNumber = 1,
                        Schedule = new NotificationRequestSchedule
                        {
                            NotifyTime = DateTime.Now.AddMilliseconds(150)
                        }
                    };

                    await LocalNotificationCenter.Current.Show(request);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[NOTIFICACION ERROR]: {ex.Message}");
                }
            });
        }
    }
}