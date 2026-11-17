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

        // --- 1. USUARIOS (REGISTRO, LOGIN, SESIÓN Y RECUPERACIÓN) ---
        public async Task<bool> RegistrarUsuarioAsync(Usuario usuario)
        {
            try
            {
                var usuarios = await _firebase.Child("Usuarios").OnceAsync<Usuario>();
                var existe = usuarios.FirstOrDefault(u => u.Object.Email.ToLower() == usuario.Email.Trim().ToLower());

                if (existe != null) return false;

                // Cifrar la contraseña con SHA-256 antes de guardarla en Firebase
                usuario.Password = SecurityHelper.HashPassword(usuario.Password);

                var result = await _firebase.Child("Usuarios").PostAsync(usuario);
                int idUnico = Math.Abs(result.Key.GetHashCode());
                usuario.Id = idUnico;

                await _firebase.Child("Usuarios").Child(result.Key).PutAsync(usuario);
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

                    // Si la cuenta era vieja (texto plano), la migra automáticamente a SHA-256
                    if (u.Password.Trim() == password.Trim() && password.Trim() != passwordHash)
                    {
                        u.Password = passwordHash;
                        requiereActualizar = true;
                    }

                    if (requiereActualizar)
                        await _firebase.Child("Usuarios").Child(coincidencia.Key).PutAsync(u);

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
                var usuarios = await _firebase.Child("Usuarios").OnceAsync<Usuario>();
                var coincidencia = usuarios.FirstOrDefault(u =>
                    u.Object != null &&
                    u.Object.Email.Trim().ToLower() == email.Trim().ToLower());

                return coincidencia?.Object;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FIREBASE ERROR SESION]: {ex.Message}");
                return null;
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
                var usuarios = await _firebase.Child("Usuarios").OnceAsync<Usuario>();
                return usuarios
                    .Select(u => {
                        var user = u.Object;
                        if (user.Id == 0) user.Id = Math.Abs(u.Key.GetHashCode());
                        return user;
                    })
                    .Where(u => u.TipoPerfil == "Técnico" && !u.CuentaSuspendida)
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
                var solicitudes = await _firebase.Child("Solicitudes").OnceAsync<SolicitudCita>();

                if (esTecnico)
                {
                    var lista = solicitudes
                        .Select(s => {
                            var obj = s.Object;
                            if (obj.Id == 0) obj.Id = Math.Abs(s.Key.GetHashCode());
                            return obj;
                        })
                        .Where(s => s.TecnicoId == usuarioId)
                        .ToList();

                    if (!lista.Any())
                    {
                        lista = solicitudes
                            .Select(s => {
                                var obj = s.Object;
                                if (obj.Id == 0) obj.Id = Math.Abs(s.Key.GetHashCode());
                                return obj;
                            })
                            .ToList();
                    }

                    return lista;
                }
                else
                {
                    return solicitudes
                        .Select(s => {
                            var obj = s.Object;
                            if (obj.Id == 0) obj.Id = Math.Abs(s.Key.GetHashCode());
                            return obj;
                        })
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
                var solicitudes = await _firebase.Child("Solicitudes").OnceAsync<SolicitudCita>();
                var target = solicitudes.FirstOrDefault(s => s.Object.Id == citaId || Math.Abs(s.Key.GetHashCode()) == citaId);
                return target?.Object;
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

        // --- 3. CHAT Y FOTO DE PERFIL ---
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

        // --- 5. PRESUPUESTOS Y PAGO EN CUSTODIA (ESTILO MERCADO LIBRE) ---
        public async Task<bool> EnviarPresupuestoAsync(int citaId, string concepto, decimal monto)
        {
            try
            {
                var solicitudes = await _firebase.Child("Solicitudes").OnceAsync<SolicitudCita>();
                var citaTarget = solicitudes.FirstOrDefault(s => s.Object.Id == citaId || Math.Abs(s.Key.GetHashCode()) == citaId);

                if (citaTarget != null)
                {
                    citaTarget.Object.ConceptoPresupuesto = concepto;
                    citaTarget.Object.MontoPresupuesto = monto;
                    citaTarget.Object.EstadoPago = "Cotizado";

                    await _firebase.Child("Solicitudes").Child(citaTarget.Key).PutAsync(citaTarget.Object);
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
                    decimal monto = citaTarget.Object.MontoTotalConTraslado;
                    citaTarget.Object.EstadoPago = "Liberado al Técnico";
                    citaTarget.Object.Estado = "Finalizada";
                    await _firebase.Child("Solicitudes").Child(citaTarget.Key).PutAsync(citaTarget.Object);

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

                        tecNode.Object.SaldoEnCustodia = Math.Max(0m, tecNode.Object.SaldoEnCustodia - monto);
                        tecNode.Object.SaldoLiberado += monto;
                        await _firebase.Child("Usuarios").Child(tecNode.Key).PutAsync(tecNode.Object);
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

        // --- 6. PANEL ADMINISTRADOR: AUDITORÍA, COMUNICACIÓN BILATERAL Y RESOLUCIÓN ---
        public async Task<Usuario?> ObtenerUsuarioPorIdAsync(int usuarioId)
        {
            try
            {
                var usuarios = await _firebase.Child("Usuarios").OnceAsync<Usuario>();
                var target = usuarios.FirstOrDefault(u => u.Object != null && u.Object.Id == usuarioId);
                return target?.Object;
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
                var solicitudes = await _firebase.Child("Solicitudes").OnceAsync<SolicitudCita>();
                return solicitudes
                    .Select(s =>
                    {
                        var obj = s.Object;
                        if (obj.Id == 0) obj.Id = Math.Abs(s.Key.GetHashCode());
                        return obj;
                    })
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

                citaTarget.Object.EnDisputa = false;
                citaTarget.Object.ResueltaPorAdmin = true;
                citaTarget.Object.Estado = "Finalizada";
                citaTarget.Object.PenalizacionAplicada = montoReembolsadoCliente;
                citaTarget.Object.DictamenAdmin = resolucionTexto;

                citaTarget.Object.EstadoPago = montoLiberadoTecnico > 0
                    ? "Fondos Liquidados (Cerrado)"
                    : "Reembolsado al Cliente (Cerrado)";

                citaTarget.Object.MotivoReporte += $"\n[DICTAMEN ADMIN {DateTime.Now:dd/MM/yyyy HH:mm}]: {resolucionTexto}";

                await _firebase.Child("Solicitudes").Child(citaTarget.Key).PutAsync(citaTarget.Object);

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
                    tecNode.Object.SaldoLiberado += montoLiberadoTecnico;

                    if (penalizarReputacion)
                    {
                        tecNode.Object.ReportesAcumulados += 1;
                        tecNode.Object.CalificacionPromedio = Math.Max(1.0, tecNode.Object.CalificacionPromedio - 0.5);
                    }

                    if (suspenderTecnico)
                    {
                        tecNode.Object.CuentaSuspendida = true;
                        tecNode.Object.MotivoSuspension = $"Cita #{citaId}: {resolucionTexto} ({DateTime.Now:dd/MM/yyyy HH:mm})";
                    }

                    await _firebase.Child("Usuarios").Child(tecNode.Key).PutAsync(tecNode.Object);
                }

                await EnviarMensajeChatAsync(new MensajeChat
                {
                    SolicitudId = citaId,
                    EmisorId = 0,
                    EmisorNombre = "⚖️ ADMINISTRACIÓN PHANTEL",
                    Texto = $"RESOLUCIÓN OFICIAL DE DISPUTA (CASO CERRADO):\n" +
                            $"• Liberado al técnico: ${montoLiberadoTecnico:F2} MXN\n" +
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
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}