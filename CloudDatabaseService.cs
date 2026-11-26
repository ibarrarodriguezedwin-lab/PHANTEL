using Firebase.Database;
using Firebase.Database.Query;
using Firebase.Database.Streaming;
using PHANTEL.Helpers;
using PHANTEL.Models;

namespace PHANTEL.Services
{
    public class CloudDatabaseService
    {
        private readonly FirebaseClient _firebase = new("https://phantel-enterprises-default-rtdb.firebaseio.com/");

        // --- CACHÉ EN MEMORIA PARA REDUCIR DESCARGAS DE FIREBASE ---
        private static List<Usuario>? _cacheUsuarios;
        private static DateTime _ultimoFetchUsuarios = DateTime.MinValue;

        private static List<SolicitudCita>? _cacheSolicitudes;
        private static DateTime _ultimoFetchSolicitudes = DateTime.MinValue;

        private static readonly TimeSpan TiempoCache = TimeSpan.FromSeconds(30);

        public static void InvalidarCacheUsuarios() => _cacheUsuarios = null;
        public static void InvalidarCacheSolicitudes() => _cacheSolicitudes = null;

        // --- CALIFICACIONES REALES (promedio, número de reseñas y lista por técnico) ---
        private static List<Calificacion>? _cacheCalificaciones;
        private static DateTime _ultimoFetchCalificaciones = DateTime.MinValue;

        public static void InvalidarCacheCalificaciones() => _cacheCalificaciones = null;

        private async Task<List<Calificacion>> ObtenerTodasCalificacionesConCacheAsync()
        {
            if (_cacheCalificaciones != null && (DateTime.Now - _ultimoFetchCalificaciones) < TiempoCache)
                return _cacheCalificaciones;

            var todas = await _firebase.Child("Calificaciones").OnceAsync<Calificacion>();
            _cacheCalificaciones = todas
                .Where(c => c.Object != null)
                .Select(c => c.Object)
                .ToList();

            _ultimoFetchCalificaciones = DateTime.Now;
            return _cacheCalificaciones;
        }

        // Una reseña es del técnico si coincide su Id o su correo (el correo no cambia entre sesiones)
        private static bool CalificacionEsDe(Calificacion c, Usuario t) =>
            (t.Id != 0 && c.TecnicoId == t.Id) ||
            (!string.IsNullOrWhiteSpace(c.TecnicoEmail) && !string.IsNullOrWhiteSpace(t.Email) &&
             c.TecnicoEmail.Trim().ToLower() == t.Email.Trim().ToLower());

        public async Task<List<Calificacion>> ObtenerCalificacionesTecnicoAsync(Usuario tecnico)
        {
            try
            {
                var todas = await ObtenerTodasCalificacionesConCacheAsync();
                return todas
                    .Where(c => CalificacionEsDe(c, tecnico))
                    .OrderByDescending(c => c.Fecha ?? DateTime.MinValue)
                    .ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FIREBASE ERROR LISTA RESEÑAS]: {ex.Message}");
                return new List<Calificacion>();
            }
        }

        private async Task AplicarResumenCalificacionesAsync(IEnumerable<Usuario> tecnicos)
        {
            try
            {
                var todas = await ObtenerTodasCalificacionesConCacheAsync();

                foreach (var t in tecnicos)
                {
                    var propias = todas.Where(c => CalificacionEsDe(c, t)).ToList();

                    if (propias.Count > 0)
                    {
                        double promedio = Math.Round(propias.Average(c => c.Puntuacion), 1);
                        t.TotalCalificaciones = propias.Count;
                        t.CalificacionPromedio = Math.Max(1.0, Math.Min(5.0, promedio + t.AjusteReputacion));
                    }
                    else
                    {
                        t.TotalCalificaciones = 0;
                        t.CalificacionPromedio = 0.0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FIREBASE ERROR RESUMEN CALIFICACIONES]: {ex.Message}");
            }
        }

        private async Task<List<Usuario>> ObtenerTodosUsuariosConCacheAsync()
        {
            if (_cacheUsuarios != null && (DateTime.Now - _ultimoFetchUsuarios) < TiempoCache)
                return _cacheUsuarios;

            var usuarios = await _firebase.Child("Usuarios").OnceAsync<Usuario>();
            _cacheUsuarios = usuarios
                .Select(u =>
                {
                    var obj = u.Object;
                    if (obj != null && obj.Id == 0) obj.Id = Math.Abs(u.Key.GetHashCode());
                    return obj!;
                })
                .Where(u => u != null)
                .ToList();

            _ultimoFetchUsuarios = DateTime.Now;
            return _cacheUsuarios;
        }

        private async Task<List<SolicitudCita>> ObtenerTodasSolicitudesConCacheAsync()
        {
            if (_cacheSolicitudes != null && (DateTime.Now - _ultimoFetchSolicitudes) < TiempoCache)
                return _cacheSolicitudes;

            var solicitudes = await _firebase.Child("Solicitudes").OnceAsync<SolicitudCita>();
            _cacheSolicitudes = solicitudes
                .Select(s =>
                {
                    var obj = s.Object;
                    if (obj != null && obj.Id == 0) obj.Id = Math.Abs(s.Key.GetHashCode());
                    return obj!;
                })
                .Where(s => s != null)
                .ToList();

            _ultimoFetchSolicitudes = DateTime.Now;
            return _cacheSolicitudes;
        }

        // --- 1. USUARIOS (REGISTRO, LOGIN, SESIÓN, CP Y RECUPERACIÓN) ---
        public async Task<bool> RegistrarUsuarioAsync(Usuario usuario)
        {
            try
            {
                var usuarios = await _firebase.Child("Usuarios").OnceAsync<Usuario>();
                var existe = usuarios.FirstOrDefault(u => u.Object.Email.ToLower() == usuario.Email.Trim().ToLower());

                if (existe != null) return false;

                usuario.Password = SecurityHelper.HashPassword(usuario.Password);

                var result = await _firebase.Child("Usuarios").PostAsync(usuario);
                int idUnico = Math.Abs(result.Key.GetHashCode());
                usuario.Id = idUnico;

                await _firebase.Child("Usuarios").Child(result.Key).PutAsync(usuario);
                InvalidarCacheUsuarios();
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FIREBASE ERROR REGISTRO]: {ex.Message}");
                return false;
            }
        }

        public async Task<Usuario?> ValidarLoginAsync(string email, string password)
        {
            try
            {
                string passwordHash = SecurityHelper.HashPassword(password);
                var usuarios = await _firebase.Child("Usuarios").OnceAsync<Usuario>();

                var coincidencia = usuarios.FirstOrDefault(u =>
                    u.Object.Email.Trim().ToLower() == email.Trim().ToLower() &&
                    (u.Object.Password.Trim() == passwordHash || u.Object.Password.Trim() == password.Trim()));

                if (coincidencia != null)
                {
                    var u = coincidencia.Object;
                    bool requiereActualizar = false;

                    if (u.Id == 0)
                    {
                        u.Id = Math.Abs(coincidencia.Key.GetHashCode());
                        requiereActualizar = true;
                    }

                    if (u.Password.Trim() == password.Trim() && password.Trim() != passwordHash)
                    {
                        u.Password = passwordHash;
                        requiereActualizar = true;
                    }

                    if (requiereActualizar)
                        await _firebase.Child("Usuarios").Child(coincidencia.Key).PutAsync(u);

                    InvalidarCacheUsuarios();
                    return u;
                }
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FIREBASE ERROR LOGIN]: {ex.Message}");
                return null;
            }
        }

        public async Task<Usuario?> ObtenerUsuarioPorEmailAsync(string email)
        {
            try
            {
                var usuarios = await ObtenerTodosUsuariosConCacheAsync();
                var encontrado = usuarios.FirstOrDefault(u =>
                    u.Email.Trim().ToLower() == email.Trim().ToLower());

                if (encontrado != null && encontrado.TipoPerfil == "Técnico")
                    await AplicarResumenCalificacionesAsync(new[] { encontrado });

                return encontrado;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FIREBASE ERROR SESION]: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> ActualizarCodigoPostalUsuarioAsync(int usuarioId, string email, string nuevoCp, double lat, double lon)
        {
            try
            {
                var usuarios = await _firebase.Child("Usuarios").OnceAsync<Usuario>();
                var nodo = usuarios.FirstOrDefault(u =>
                    u.Object != null &&
                    ((usuarioId != 0 && u.Object.Id == usuarioId) ||
                     (!string.IsNullOrWhiteSpace(email) && u.Object.Email.Trim().ToLower() == email.Trim().ToLower())));

                if (nodo != null)
                {
                    if (nodo.Object.Id == 0)
                        nodo.Object.Id = Math.Abs(nodo.Key.GetHashCode());

                    nodo.Object.CodigoPostal = nuevoCp.Trim();
                    nodo.Object.Latitud = lat;
                    nodo.Object.Longitud = lon;

                    await _firebase
                        .Child("Usuarios")
                        .Child(nodo.Key)
                        .PutAsync(nodo.Object);

                    InvalidarCacheUsuarios();
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FIREBASE ERROR ACTUALIZAR CP]: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> RestablecerPasswordAsync(string email, string telefono, string nuevaPassword)
        {
            try
            {
                var usuarios = await _firebase.Child("Usuarios").OnceAsync<Usuario>();
                var coincidencia = usuarios.FirstOrDefault(u =>
                    u.Object != null &&
                    u.Object.Email.Trim().ToLower() == email.Trim().ToLower() &&
                    u.Object.Telefono.Trim() == telefono.Trim());

                if (coincidencia != null)
                {
                    coincidencia.Object.Password = SecurityHelper.HashPassword(nuevaPassword);
                    await _firebase
                        .Child("Usuarios")
                        .Child(coincidencia.Key)
                        .PutAsync(coincidencia.Object);
                    InvalidarCacheUsuarios();
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FIREBASE ERROR RESET PASS]: {ex.Message}");
                return false;
            }
        }

        public async Task<List<Usuario>> ObtenerTecnicosAsync()
        {
            try
            {
                var usuarios = await ObtenerTodosUsuariosConCacheAsync();
                var tecnicos = usuarios
                    .Where(u => u.TipoPerfil == "Técnico" && !u.CuentaSuspendida && u.Disponible)
                    .ToList();

                await AplicarResumenCalificacionesAsync(tecnicos);

                return tecnicos
                    .OrderByDescending(u => u.EstadoVerificacion == "Verificado")
                    .ThenByDescending(u => u.PuntajeRecomendacion)
                    .ToList();
            }
            catch
            {
                return new List<Usuario>();
            }
        }

        // --- 2. SOLICITUDES DE CITAS EN TIEMPO REAL ---
        public async Task<bool> CrearSolicitudAsync(SolicitudCita cita)
        {
            try
            {
                var result = await _firebase.Child("Solicitudes").PostAsync(cita);
                cita.Id = Math.Abs(result.Key.GetHashCode());
                await _firebase.Child("Solicitudes").Child(result.Key).PutAsync(cita);
                InvalidarCacheSolicitudes();
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FIREBASE ERROR SOLICITUD]: {ex.Message}");
                return false;
            }
        }

        public async Task<List<SolicitudCita>> ObtenerSolicitudesPorUsuarioAsync(int usuarioId, bool esTecnico)
        {
            try
            {
                var solicitudes = await ObtenerTodasSolicitudesConCacheAsync();

                if (esTecnico)
                {
                    var lista = solicitudes
                        .Where(s => s.TecnicoId == usuarioId)
                        .ToList();

                    return lista.Any() ? lista : solicitudes.ToList();
                }
                else
                {
                    return solicitudes
                        .Where(s => s.ClienteId == usuarioId)
                        .ToList();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FIREBASE ERROR SOLICITUDES]: {ex.Message}");
                return new List<SolicitudCita>();
            }
        }

        public async Task<SolicitudCita?> ObtenerSolicitudPorIdAsync(int citaId)
        {
            try
            {
                var solicitudes = await ObtenerTodasSolicitudesConCacheAsync();
                return solicitudes.FirstOrDefault(s => s.Id == citaId);
            }
            catch
            {
                return null;
            }
        }

        public async Task ActualizarEstadoSolicitudAsync(int citaId, string nuevoEstado)
        {
            try
            {
                var solicitudes = await _firebase.Child("Solicitudes").OnceAsync<SolicitudCita>();
                var citaTarget = solicitudes.FirstOrDefault(s => s.Object.Id == citaId || Math.Abs(s.Key.GetHashCode()) == citaId);

                if (citaTarget != null)
                {
                    await _firebase
                        .Child("Solicitudes")
                        .Child(citaTarget.Key)
                        .Child("Estado")
                        .PutAsync($"\"{nuevoEstado}\"");
                    InvalidarCacheSolicitudes();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FIREBASE ERROR UPDATE ESTADO]: {ex.Message}");
            }
        }

        public async Task<bool> MarcarCitaEnDisputaAsync(int citaId, string canalContacto)
        {
            try
            {
                var solicitudes = await _firebase.Child("Solicitudes").OnceAsync<SolicitudCita>();
                var citaTarget = solicitudes.FirstOrDefault(s => s.Object.Id == citaId || Math.Abs(s.Key.GetHashCode()) == citaId);

                if (citaTarget != null)
                {
                    citaTarget.Object.EnDisputa = true;
                    if (citaTarget.Object.EstadoPago == "En Custodia (Retenido)")
                        citaTarget.Object.EstadoPago = "Retenido por Disputa";

                    citaTarget.Object.MotivoReporte = $"Reporte iniciado por cliente vía {canalContacto} el {DateTime.Now:dd/MM/yyyy HH:mm}";

                    await _firebase
                        .Child("Solicitudes")
                        .Child(citaTarget.Key)
                        .PutAsync(citaTarget.Object);
                    InvalidarCacheSolicitudes();
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FIREBASE ERROR DISPUTA]: {ex.Message}");
                return false;
            }
        }

        // --- 3. CHAT, LECTURA EN VIVO Y FOTO DE PERFIL ---
        public async Task<bool> EnviarMensajeChatAsync(MensajeChat mensaje)
        {
            try
            {
                await _firebase
                    .Child("Chats")
                    .Child(mensaje.SolicitudId.ToString())
                    .PostAsync(mensaje);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FIREBASE ERROR ENVIAR CHAT]: {ex.Message}");
                return false;
            }
        }

        public IObservable<FirebaseEvent<MensajeChat>> EscucharChatEnVivo(int solicitudId)
        {
            return _firebase
                .Child("Chats")
                .Child(solicitudId.ToString())
                .AsObservable<MensajeChat>();
        }

        public async Task MarcarMensajeLeidoAsync(int solicitudId, string mensajeId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(mensajeId)) return;

                await _firebase
                    .Child("Chats")
                    .Child(solicitudId.ToString())
                    .Child(mensajeId)
                    .Child("Leido")
                    .PutAsync("true");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FIREBASE ERROR MARCAR LEIDO]: {ex.Message}");
            }
        }

        public async Task<bool> ActualizarFotoPerfilAsync(int usuarioId, string nuevaFotoBase64)
        {
            try
            {
                var usuarios = await _firebase.Child("Usuarios").OnceAsync<Usuario>();
                var nodoUsuario = usuarios.FirstOrDefault(u => u.Object != null && u.Object.Id == usuarioId);

                if (nodoUsuario != null)
                {
                    nodoUsuario.Object.FotoPerfil = nuevaFotoBase64;
                    await _firebase
                        .Child("Usuarios")
                        .Child(nodoUsuario.Key)
                        .PutAsync(nodoUsuario.Object);
                    InvalidarCacheUsuarios();
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FIREBASE ERROR ACTUALIZAR FOTO]: {ex.Message}");
                return false;
            }
        }

        // --- 4. CALIFICACIONES, CONTROL DE POPUP Y ELIMINACIÓN ---
        public async Task<bool> GuardarCalificacionAsync(Calificacion calificacion)
        {
            try
            {
                await _firebase.Child("Calificaciones").PostAsync(calificacion);
                InvalidarCacheCalificaciones();
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FIREBASE ERROR CALIFICACION]: {ex.Message}");
                return false;
            }
        }

        public async Task MarcarCitaCalificadaAsync(int citaId, bool esTecnico)
        {
            try
            {
                var solicitudes = await _firebase.Child("Solicitudes").OnceAsync<SolicitudCita>();
                var citaTarget = solicitudes.FirstOrDefault(s => s.Object.Id == citaId || Math.Abs(s.Key.GetHashCode()) == citaId);

                if (citaTarget != null)
                {
                    string campo = esTecnico ? "CalificadaPorTecnico" : "CalificadaPorCliente";
                    await _firebase
                        .Child("Solicitudes")
                        .Child(citaTarget.Key)
                        .Child(campo)
                        .PutAsync("true");
                    InvalidarCacheSolicitudes();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FIREBASE ERROR MARCAR CALIFICADA]: {ex.Message}");
            }
        }

        public async Task<bool> EliminarSolicitudAsync(int solicitudId)
        {
            try
            {
                var solicitudes = await _firebase.Child("Solicitudes").OnceAsync<SolicitudCita>();
                var itemABorrar = solicitudes.FirstOrDefault(s => s.Object.Id == solicitudId || Math.Abs(s.Key.GetHashCode()) == solicitudId);

                if (itemABorrar != null)
                {
                    await _firebase.Child("Solicitudes").Child(itemABorrar.Key).DeleteAsync();
                    InvalidarCacheSolicitudes();
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FIREBASE ERROR ELIMINAR]: {ex.Message}");
                return false;
            }
        }

        // --- 5. PRESUPUESTOS, COMISIONES (3% SOLO SOBRE TRABAJO) Y PAGO EN CUSTODIA ---
        public async Task<bool> EnviarPresupuestoAsync(int citaId, string concepto, decimal monto)
        {
            try
            {
                var solicitudes = await _firebase.Child("Solicitudes").OnceAsync<SolicitudCita>();
                var citaTarget = solicitudes.FirstOrDefault(s => s.Object.Id == citaId || Math.Abs(s.Key.GetHashCode()) == citaId);

                if (citaTarget != null)
                {
                    decimal comision3Porciento = Math.Round(monto * 0.03m, 2);
                    decimal netoTotalTecnico = (monto - comision3Porciento) + citaTarget.Object.TarifaTraslado;

                    citaTarget.Object.ConceptoPresupuesto = concepto;
                    citaTarget.Object.MontoPresupuesto = monto;
                    citaTarget.Object.PorcentajeComisionAplicado = 0.03m;
                    citaTarget.Object.MontoComisionPhantel = comision3Porciento;
                    citaTarget.Object.MontoNetoTecnico = netoTotalTecnico;
                    citaTarget.Object.EstadoPago = "Cotizado";

                    await _firebase.Child("Solicitudes").Child(citaTarget.Key).PutAsync(citaTarget.Object);
                    InvalidarCacheSolicitudes();
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FIREBASE ERROR PRESUPUESTO]: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> RegistrarPagoEnCustodiaAsync(int citaId, string referencia, string comprobanteBase64)
        {
            try
            {
                var solicitudes = await _firebase.Child("Solicitudes").OnceAsync<SolicitudCita>();
                var citaTarget = solicitudes.FirstOrDefault(s => s.Object.Id == citaId || Math.Abs(s.Key.GetHashCode()) == citaId);

                if (citaTarget != null)
                {
                    citaTarget.Object.EstadoPago = "En Custodia (Retenido)";
                    citaTarget.Object.ReferenciaTransferencia = referencia;
                    citaTarget.Object.ComprobantePagoBase64 = comprobanteBase64;
                    await _firebase.Child("Solicitudes").Child(citaTarget.Key).PutAsync(citaTarget.Object);
                    InvalidarCacheSolicitudes();

                    var usuarios = await _firebase.Child("Usuarios").OnceAsync<Usuario>();
                    var tecNode = usuarios.FirstOrDefault(u =>
                        u.Object != null &&
                        (
                            (citaTarget.Object.TecnicoId != 0 && u.Object.Id == citaTarget.Object.TecnicoId) ||
                            (!string.IsNullOrWhiteSpace(citaTarget.Object.TecnicoEmail) &&
                             u.Object.Email.Trim().ToLower() == citaTarget.Object.TecnicoEmail.Trim().ToLower())
                        ));

                    if (tecNode != null)
                    {
                        if (tecNode.Object.Id == 0)
                            tecNode.Object.Id = Math.Abs(tecNode.Key.GetHashCode());

                        tecNode.Object.SaldoEnCustodia += citaTarget.Object.MontoTotalConTraslado;
                        await _firebase.Child("Usuarios").Child(tecNode.Key).PutAsync(tecNode.Object);
                        InvalidarCacheUsuarios();
                    }

                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FIREBASE ERROR CUSTODIA]: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> LiberarPagoATecnicoAsync(int citaId)
        {
            try
            {
                var solicitudes = await _firebase.Child("Solicitudes").OnceAsync<SolicitudCita>();
                var citaTarget = solicitudes.FirstOrDefault(s => s.Object.Id == citaId || Math.Abs(s.Key.GetHashCode()) == citaId);

                if (citaTarget != null && !citaTarget.Object.EnDisputa && !citaTarget.Object.ResueltaPorAdmin)
                {
                    decimal totalBruto = citaTarget.Object.MontoTotalConTraslado;
                    decimal presupuestoTrabajo = citaTarget.Object.MontoPresupuesto;
                    decimal trasladoIntegro = citaTarget.Object.TarifaTraslado;

                    decimal comision3Porciento = Math.Round(presupuestoTrabajo * 0.03m, 2);
                    decimal netoTecnico = (presupuestoTrabajo - comision3Porciento) + trasladoIntegro;

                    citaTarget.Object.PorcentajeComisionAplicado = 0.03m;
                    citaTarget.Object.MontoComisionPhantel = comision3Porciento;
                    citaTarget.Object.MontoNetoTecnico = netoTecnico;
                    citaTarget.Object.EstadoPago = "Liberado al Técnico";
                    citaTarget.Object.Estado = "Finalizada";

                    await _firebase.Child("Solicitudes").Child(citaTarget.Key).PutAsync(citaTarget.Object);
                    InvalidarCacheSolicitudes();

                    var usuarios = await _firebase.Child("Usuarios").OnceAsync<Usuario>();
                    var tecNode = usuarios.FirstOrDefault(u =>
                        u.Object != null &&
                        (
                            (citaTarget.Object.TecnicoId != 0 && u.Object.Id == citaTarget.Object.TecnicoId) ||
                            (!string.IsNullOrWhiteSpace(citaTarget.Object.TecnicoEmail) &&
                             u.Object.Email.Trim().ToLower() == citaTarget.Object.TecnicoEmail.Trim().ToLower())
                        ));

                    if (tecNode != null)
                    {
                        if (tecNode.Object.Id == 0)
                            tecNode.Object.Id = Math.Abs(tecNode.Key.GetHashCode());

                        tecNode.Object.SaldoEnCustodia = Math.Max(0m, tecNode.Object.SaldoEnCustodia - totalBruto);
                        tecNode.Object.SaldoLiberado += netoTecnico;
                        await _firebase.Child("Usuarios").Child(tecNode.Key).PutAsync(tecNode.Object);
                        InvalidarCacheUsuarios();
                    }
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FIREBASE ERROR LIBERAR PAGO]: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> RetenerPagoPorDisputaAsync(int citaId, string motivo)
        {
            try
            {
                var solicitudes = await _firebase.Child("Solicitudes").OnceAsync<SolicitudCita>();
                var citaTarget = solicitudes.FirstOrDefault(s => s.Object.Id == citaId || Math.Abs(s.Key.GetHashCode()) == citaId);

                if (citaTarget != null)
                {
                    citaTarget.Object.EnDisputa = true;
                    citaTarget.Object.EstadoPago = "Retenido por Disputa";
                    citaTarget.Object.MotivoReporte = motivo;

                    await _firebase.Child("Solicitudes").Child(citaTarget.Key).PutAsync(citaTarget.Object);
                    InvalidarCacheSolicitudes();
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FIREBASE ERROR RETENER DISPUTA]: {ex.Message}");
                return false;
            }
        }

        // --- 6. PANEL ADMINISTRADOR: AUDITORÍA, PENALIZACIÓN (5% SOBRE PRESUPUESTO) Y RESOLUCIÓN ---
        public async Task<Usuario?> ObtenerUsuarioPorIdAsync(int usuarioId)
        {
            try
            {
                var usuarios = await ObtenerTodosUsuariosConCacheAsync();
                return usuarios.FirstOrDefault(u => u.Id == usuarioId);
            }
            catch
            {
                return null;
            }
        }

        public async Task<List<SolicitudCita>> ObtenerDisputasActivasAsync()
        {
            try
            {
                var solicitudes = await ObtenerTodasSolicitudesConCacheAsync();
                return solicitudes
                    .Where(s => s.EnDisputa || s.EstadoPago == "Retenido por Disputa")
                    .OrderByDescending(s => s.FechaCreacion)
                    .ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FIREBASE ERROR DISPUTAS ADMIN]: {ex.Message}");
                return new List<SolicitudCita>();
            }
        }

        public async Task<bool> ResolverDisputaAdminAsync(
            int citaId,
            decimal montoLiberadoTecnico,
            decimal montoReembolsadoCliente,
            bool penalizarReputacion,
            bool suspenderTecnico,
            string resolucionTexto)
        {
            try
            {
                var solicitudes = await _firebase.Child("Solicitudes").OnceAsync<SolicitudCita>();
                var citaTarget = solicitudes.FirstOrDefault(s => s.Object.Id == citaId || Math.Abs(s.Key.GetHashCode()) == citaId);

                if (citaTarget == null) return false;

                decimal totalOriginal = citaTarget.Object.MontoTotalConTraslado;
                decimal baseTrabajo = citaTarget.Object.MontoPresupuesto > 0
                    ? citaTarget.Object.MontoPresupuesto
                    : totalOriginal;

                decimal parteTrabajoLiberada = Math.Max(0m, montoLiberadoTecnico - citaTarget.Object.TarifaTraslado);
                decimal comisionBase = Math.Round(parteTrabajoLiberada * 0.03m, 2);
                decimal multaDisputa5Porciento = montoReembolsadoCliente > 0
                    ? Math.Round(baseTrabajo * 0.05m, 2)
                    : 0m;

                decimal netoFinalTecnico = Math.Max(0m, montoLiberadoTecnico - comisionBase - multaDisputa5Porciento);

                citaTarget.Object.EnDisputa = false;
                citaTarget.Object.ResueltaPorAdmin = true;
                citaTarget.Object.Estado = "Finalizada";
                citaTarget.Object.PorcentajeComisionAplicado = montoReembolsadoCliente > 0 ? 0.08m : 0.03m;
                citaTarget.Object.MontoComisionPhantel = comisionBase + multaDisputa5Porciento;
                citaTarget.Object.MontoNetoTecnico = netoFinalTecnico;
                citaTarget.Object.PenalizacionAplicada = montoReembolsadoCliente + multaDisputa5Porciento;
                citaTarget.Object.DictamenAdmin = resolucionTexto;

                citaTarget.Object.EstadoPago = montoLiberadoTecnico > 0
                    ? "Fondos Liquidados (Cerrado)"
                    : "Reembolsado al Cliente (Cerrado)";

                citaTarget.Object.MotivoReporte += $"\n[DICTAMEN ADMIN {DateTime.Now:dd/MM/yyyy HH:mm}]: {resolucionTexto}";

                await _firebase.Child("Solicitudes").Child(citaTarget.Key).PutAsync(citaTarget.Object);
                InvalidarCacheSolicitudes();

                var usuarios = await _firebase.Child("Usuarios").OnceAsync<Usuario>();
                var tecNode = usuarios.FirstOrDefault(u =>
                    u.Object != null &&
                    (
                        (citaTarget.Object.TecnicoId != 0 && u.Object.Id == citaTarget.Object.TecnicoId) ||
                        (!string.IsNullOrWhiteSpace(citaTarget.Object.TecnicoEmail) &&
                         u.Object.Email.Trim().ToLower() == citaTarget.Object.TecnicoEmail.Trim().ToLower())
                    ));

                if (tecNode != null)
                {
                    if (tecNode.Object.Id == 0)
                        tecNode.Object.Id = Math.Abs(tecNode.Key.GetHashCode());

                    tecNode.Object.SaldoEnCustodia = Math.Max(0m, tecNode.Object.SaldoEnCustodia - totalOriginal);
                    tecNode.Object.SaldoLiberado = Math.Max(0m, tecNode.Object.SaldoLiberado + montoLiberadoTecnico - comisionBase - multaDisputa5Porciento);

                    if (penalizarReputacion)
                    {
                        tecNode.Object.ReportesAcumulados += 1;
                        tecNode.Object.AjusteReputacion -= 0.5;
                    }

                    if (suspenderTecnico)
                    {
                        tecNode.Object.CuentaSuspendida = true;
                        tecNode.Object.MotivoSuspension = $"Cita #{citaId}: {resolucionTexto} ({DateTime.Now:dd/MM/yyyy HH:mm})";
                    }

                    await _firebase.Child("Usuarios").Child(tecNode.Key).PutAsync(tecNode.Object);
                    InvalidarCacheUsuarios();
                }

                await EnviarMensajeChatAsync(new MensajeChat
                {
                    SolicitudId = citaId,
                    EmisorId = 0,
                    EmisorNombre = "ADMINISTRACIÓN PHANTEL",
                    Texto = $"RESOLUCIÓN OFICIAL DE DISPUTA (CASO CERRADO):\n" +
                            $"• Monto bruto técnico: ${montoLiberadoTecnico:F2} MXN\n" +
                            $"• Comisión (3%) + Penalización disputa (5%): -${(comisionBase + multaDisputa5Porciento):F2} MXN\n" +
                            $"• Neto acreditado al técnico: ${netoFinalTecnico:F2} MXN\n" +
                            $"• Reembolsado al cliente: ${montoReembolsadoCliente:F2} MXN\n" +
                            $"• Sanción: {(suspenderTecnico ? "CUENTA SUSPENDIDA" : (penalizarReputacion ? "Penalización en reputación (-0.5 ★)" : "Sin sanción"))}\n" +
                            $"• Dictamen: {resolucionTexto}",
                    FechaHora = DateTime.Now,
                    EsTecnico = false
                });

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FIREBASE ERROR RESOLVER DISPUTA]: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> SolicitarRetiroTecnicoAsync(int tecnicoId, string clabeInterbancaria, string? emailTecnico = null)
        {
            try
            {
                var usuarios = await _firebase.Child("Usuarios").OnceAsync<Usuario>();
                var tecNode = usuarios.FirstOrDefault(u =>
                    u.Object != null &&
                    (
                        (tecnicoId != 0 && u.Object.Id == tecnicoId) ||
                        (!string.IsNullOrWhiteSpace(emailTecnico) &&
                         u.Object.Email.Trim().ToLower() == emailTecnico.Trim().ToLower())
                    ));

                if (tecNode != null && tecNode.Object.SaldoLiberado > 0)
                {
                    tecNode.Object.ClabeInterbancaria = clabeInterbancaria.Trim();
                    tecNode.Object.SaldoLiberado = 0m;
                    await _firebase.Child("Usuarios").Child(tecNode.Key).PutAsync(tecNode.Object);
                    InvalidarCacheUsuarios();
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        // --- 7. VERIFICACIÓN DE IDENTIDAD DE TÉCNICOS (KYC) ---
        public async Task<bool> EnviarDocumentosVerificacionAsync(int tecnicoId, string emailTecnico, string ineFrenteBase64, string ineReversoBase64)
        {
            try
            {
                var usuarios = await _firebase.Child("Usuarios").OnceAsync<Usuario>();
                var tecNode = usuarios.FirstOrDefault(u =>
                    u.Object != null &&
                    (
                        (tecnicoId != 0 && u.Object.Id == tecnicoId) ||
                        (!string.IsNullOrWhiteSpace(emailTecnico) &&
                         u.Object.Email.Trim().ToLower() == emailTecnico.Trim().ToLower())
                    ));

                if (tecNode != null)
                {
                    tecNode.Object.IneFrenteBase64 = ineFrenteBase64;
                    tecNode.Object.IneReversoBase64 = ineReversoBase64;
                    tecNode.Object.EstadoVerificacion = "En Revisión";
                    tecNode.Object.MotivoRechazoVerificacion = string.Empty;

                    await _firebase.Child("Usuarios").Child(tecNode.Key).PutAsync(tecNode.Object);
                    InvalidarCacheUsuarios();
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FIREBASE ERROR ENVIAR KYC]: {ex.Message}");
                return false;
            }
        }

        public async Task<List<Usuario>> ObtenerTecnicosPendientesVerificacionAsync()
        {
            try
            {
                var usuarios = await ObtenerTodosUsuariosConCacheAsync();
                return usuarios
                    .Where(u => u.TipoPerfil == "Técnico" && u.EstadoVerificacion == "En Revisión")
                    .ToList();
            }
            catch
            {
                return new List<Usuario>();
            }
        }

        public async Task<bool> DictaminarVerificacionTecnicoAsync(int tecnicoId, string emailTecnico, bool aprobar, string motivoRechazo = "")
        {
            try
            {
                var usuarios = await _firebase.Child("Usuarios").OnceAsync<Usuario>();
                var tecNode = usuarios.FirstOrDefault(u =>
                    u.Object != null &&
                    (
                        (tecnicoId != 0 && u.Object.Id == tecnicoId) ||
                        (!string.IsNullOrWhiteSpace(emailTecnico) &&
                         u.Object.Email.Trim().ToLower() == emailTecnico.Trim().ToLower())
                    ));

                if (tecNode != null)
                {
                    tecNode.Object.EstadoVerificacion = aprobar ? "Verificado" : "Rechazado";
                    tecNode.Object.MotivoRechazoVerificacion = aprobar ? string.Empty : motivoRechazo;

                    await _firebase.Child("Usuarios").Child(tecNode.Key).PutAsync(tecNode.Object);
                    InvalidarCacheUsuarios();
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FIREBASE ERROR DICTAMINAR KYC]: {ex.Message}");
                return false;
            }
        }

        // --- 8. REPORTES PRIVADOS TÉCNICO -> ADMIN, AVISOS DE CONDUCTA Y SUSPENSIÓN DE CLIENTES ---
        public async Task<bool> EnviarReportePrivadoClienteAsync(
            int solicitudId,
            int clienteId,
            int tecnicoId,
            string tecnicoNombre,
            string tecnicoEmail,
            string motivo,
            string detalle)
        {
            try
            {
                string reporteTexto = $"[REPORTE TÉCNICO -> CLIENTE ID #{clienteId}] {motivo}: {detalle.Trim()} ({DateTime.Now:dd/MM/yyyy HH:mm})";

                var solicitudes = await _firebase.Child("Solicitudes").OnceAsync<SolicitudCita>();
                var citaTarget = solicitudes.FirstOrDefault(s => s.Object.Id == solicitudId || Math.Abs(s.Key.GetHashCode()) == solicitudId);

                if (citaTarget != null)
                {
                    citaTarget.Object.ReportePrivadoTecnico = true;
                    citaTarget.Object.MotivoReporteTecnico = reporteTexto;
                    await _firebase.Child("Solicitudes").Child(citaTarget.Key).PutAsync(citaTarget.Object);
                    InvalidarCacheSolicitudes();
                }

                await _firebase.Child("ReportesClientes").Child($"cita_{solicitudId}").PutAsync(new ReporteClienteItem
                {
                    SolicitudId = solicitudId,
                    ClienteId = clienteId,
                    TecnicoId = tecnicoId,
                    TecnicoNombre = tecnicoNombre,
                    TecnicoEmail = tecnicoEmail,
                    Motivo = motivo,
                    Detalle = detalle.Trim(),
                    Fecha = DateTime.Now,
                    EstadoRevision = "Pendiente"
                });

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FIREBASE ERROR REPORTE PRIVADO]: {ex.Message}");
                return false;
            }
        }

        public async Task<List<ReporteClienteItem>> ObtenerReportesPrivadosClientesAsync()
        {
            try
            {
                var items = await _firebase.Child("ReportesClientes").OnceAsync<ReporteClienteItem>();
                return items
                    .Where(x => x.Object != null)
                    .Select(x =>
                    {
                        var r = x.Object;
                        r.Key = x.Key;
                        return r;
                    })
                    .Where(r => r.EstadoRevision != "Resuelto")
                    .OrderByDescending(r => r.Fecha)
                    .ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FIREBASE ERROR LISTA REPORTES CLIENTES]: {ex.Message}");
                return new List<ReporteClienteItem>();
            }
        }

        public async Task<bool> EnviarAvisoConductaClienteAsync(int clienteId, string reporteKey, string mensajeAviso)
        {
            try
            {
                var usuarios = await _firebase.Child("Usuarios").OnceAsync<Usuario>();
                var nodo = usuarios.FirstOrDefault(u => u.Object != null && u.Object.Id == clienteId);

                if (nodo == null) return false;

                nodo.Object.TieneAvisoConducta = true;
                nodo.Object.MensajeAvisoConducta = mensajeAviso.Trim();
                nodo.Object.FechaAvisoConducta = DateTime.Now;

                await _firebase.Child("Usuarios").Child(nodo.Key).PutAsync(nodo.Object);
                InvalidarCacheUsuarios();

                if (!string.IsNullOrEmpty(reporteKey))
                    await _firebase.Child("ReportesClientes").Child(reporteKey).Child("EstadoRevision").PutAsync("\"Resuelto\"");

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FIREBASE ERROR AVISO CONDUCTA]: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> SuspenderClienteAsync(int clienteId, string reporteKey, string motivoSuspension)
        {
            try
            {
                var usuarios = await _firebase.Child("Usuarios").OnceAsync<Usuario>();
                var nodo = usuarios.FirstOrDefault(u => u.Object != null && u.Object.Id == clienteId);

                if (nodo == null) return false;

                nodo.Object.CuentaSuspendida = true;
                nodo.Object.MotivoSuspension = $"{motivoSuspension.Trim()} ({DateTime.Now:dd/MM/yyyy HH:mm})";

                await _firebase.Child("Usuarios").Child(nodo.Key).PutAsync(nodo.Object);
                InvalidarCacheUsuarios();

                if (!string.IsNullOrEmpty(reporteKey))
                    await _firebase.Child("ReportesClientes").Child(reporteKey).Child("EstadoRevision").PutAsync("\"Resuelto\"");

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FIREBASE ERROR SUSPENDER CLIENTE]: {ex.Message}");
                return false;
            }
        }

        public async Task MarcarAvisoConductaLeidoAsync(int clienteId)
        {
            try
            {
                var usuarios = await _firebase.Child("Usuarios").OnceAsync<Usuario>();
                var nodo = usuarios.FirstOrDefault(u => u.Object != null && u.Object.Id == clienteId);

                if (nodo != null)
                {
                    nodo.Object.TieneAvisoConducta = false;
                    await _firebase.Child("Usuarios").Child(nodo.Key).PutAsync(nodo.Object);
                    InvalidarCacheUsuarios();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FIREBASE ERROR MARCAR AVISO LEIDO]: {ex.Message}");
            }
        }
    }
}