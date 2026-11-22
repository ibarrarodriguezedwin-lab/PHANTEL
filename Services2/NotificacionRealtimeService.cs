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
        private static readonly HashSet<string> _eventosNotificados = new();
        private static int _usuarioActualId = 0;
        private static int _contadorNotificacionId = 1000;

        public static async Task IniciarEscuchaAsync(Usuario usuario)
        {
            DetenerEscucha();
            if (usuario == null) return;

            _usuarioActualId = usuario.Id;
            DateTime inicioSesion = DateTime.Now.AddSeconds(-5);

            // Solicitar permiso de notificaciones en Android 13+
            if (await LocalNotificationCenter.Current.AreNotificationsEnabled() == false)
            {
                await LocalNotificationCenter.Current.RequestNotificationPermission();
            }

            // 1. ESCUCHAR SOLICITUDES (Nuevas citas para Técnico, cambios de estado/presupuestos para Cliente, Disputas para Admin)
            var subSolicitudes = _firebase
                .Child("Solicitudes")
                .AsObservable<SolicitudCita>()
                .Where(e => e.Object != null)
                .Subscribe(evento =>
                {
                    var cita = evento.Object;
                    string emailNorm = usuario.Email.Trim().ToLower();

                    // A) Para Técnicos: Nueva solicitud pendiente
                    if (usuario.TipoPerfil == "Técnico" &&
                        (cita.TecnicoId == usuario.Id || cita.TecnicoEmail.Trim().ToLower() == emailNorm) &&
                        cita.Estado == "Pendiente" &&
                        cita.FechaCreacion >= inicioSesion)
                    {
                        LanzarNotificacionUnica(
                            $"sol_nueva_{cita.Id}",
                            "🔧 Nueva Solicitud de Trabajo",
                            $"Servicio en {cita.DireccionCliente} • Traslado: ${cita.TarifaTraslado:F2} MXN");
                    }

                    // B) Para Clientes: Cita aceptada, finalizada o presupuesto recibido
                    if (usuario.TipoPerfil == "Cliente" && cita.ClienteId == usuario.Id)
                    {
                        if (cita.Estado == "Aceptada")
                        {
                            LanzarNotificacionUnica(
                                $"sol_aceptada_{cita.Id}",
                                "✅ Técnico en Camino / Cita Aceptada",
                                $"Tu solicitud #{cita.Id} fue aceptada. Ya puedes usar el chat en vivo.");
                        }
                        else if (cita.EstadoPago == "Cotizado" && cita.MontoPresupuesto > 0)
                        {
                            LanzarNotificacionUnica(
                                $"sol_cotizada_{cita.Id}_{cita.MontoPresupuesto}",
                                "💰 Presupuesto Formal Recibido",
                                $"Cotización por ${cita.MontoPresupuesto:F2} MXN en tu cita #{cita.Id}.");
                        }
                        else if (cita.Estado == "Finalizada")
                        {
                            LanzarNotificacionUnica(
                                $"sol_fin_{cita.Id}",
                                "🏁 Servicio Finalizado",
                                $"El técnico marcó como concluida la cita #{cita.Id}.");
                        }
                    }

                    // C) Para Administradores: Nuevo reporte o disputa abierta
                    if (usuario.TipoPerfil == "Admin" && (cita.EnDisputa || cita.EstadoPago == "Retenido por Disputa"))
                    {
                        LanzarNotificacionUnica(
                            $"admin_disputa_{cita.Id}",
                            "🚨 Nueva Disputa / Reporte PHANTEL",
                            $"Cita #{cita.Id} reportada. Fondos retenidos en custodia para dictamen.");
                    }
                });

            _suscripciones.Add(subSolicitudes);

            // 2. ESCUCHAR MENSAJES DE CHAT EN VIVO (Para Cliente, Técnico o Admin en citas activas)
            var subChats = _firebase
                .Child("Chats")
                .AsObservable<Dictionary<string, MensajeChat>>()
                .Where(e => e.Object != null)
                .Subscribe(async evento =>
                {
                    if (!int.TryParse(evento.Key, out int solicitudId)) return;

                    var ultimoMsg = evento.Object.Values
                        .Where(m => m != null && m.EmisorId != _usuarioActualId && m.FechaHora >= inicioSesion)
                        .OrderByDescending(m => m.FechaHora)
                        .FirstOrDefault();

                    if (ultimoMsg == null) return;

                    var cloud = new CloudDatabaseService();
                    var cita = await cloud.ObtenerSolicitudPorIdAsync(solicitudId);
                    if (cita == null) return;

                    bool involucrado = usuario.TipoPerfil == "Admin"
                        || cita.ClienteId == usuario.Id
                        || cita.TecnicoId == usuario.Id
                        || cita.TecnicoEmail.Trim().ToLower() == usuario.Email.Trim().ToLower();

                    if (involucrado)
                    {
                        string claveMsg = $"chat_{solicitudId}_{ultimoMsg.FechaHora.Ticks}";
                        LanzarNotificacionUnica(
                            claveMsg,
                            $"💬 Nuevo mensaje de {ultimoMsg.EmisorNombre}",
                            string.IsNullOrWhiteSpace(ultimoMsg.Texto) ? "📎 Archivo multimedia adjunto" : ultimoMsg.Texto);
                    }
                });

            _suscripciones.Add(subChats);

            // 3. PARA ADMINISTRADORES: Escuchar solicitudes de verificación KYC (INE enviada)
            if (usuario.TipoPerfil == "Admin")
            {
                var subKyc = _firebase
                    .Child("Usuarios")
                    .AsObservable<Usuario>()
                    .Where(e => e.Object != null && e.Object.TipoPerfil == "Técnico" && e.Object.EstadoVerificacion == "En Revisión")
                    .Subscribe(evento =>
                    {
                        var tec = evento.Object;
                        LanzarNotificacionUnica(
                            $"admin_kyc_{tec.Email.Trim().ToLower()}",
                            "🪪 Nueva Verificación KYC Pendiente",
                            $"{tec.Nombre} ({tec.Especialidad}) envió su INE para revisión.");
                    });

                _suscripciones.Add(subKyc);
            }
        }

        public static void DetenerEscucha()
        {
            foreach (var sub in _suscripciones)
            {
                try { sub.Dispose(); } catch { }
            }
            _suscripciones.Clear();
            _eventosNotificados.Clear();
            _usuarioActualId = 0;
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
                            NotifyTime = DateTime.Now.AddMilliseconds(200)
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