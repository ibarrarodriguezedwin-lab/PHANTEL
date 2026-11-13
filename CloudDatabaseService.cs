using Firebase.Database;
using Firebase.Database.Query;
using Firebase.Database.Streaming;
using PHANTEL.Models;

namespace PHANTEL.Services
{
    public class CloudDatabaseService
    {
        private readonly FirebaseClient _firebase = new("https://phantel-enterprises-default-rtdb.firebaseio.com/");

        // --- 1. USUARIOS (REGISTRO Y LOGIN) ---
        public async Task<bool> RegistrarUsuarioAsync(Usuario usuario)
        {
            try
            {
                var usuarios = await _firebase.Child("Usuarios").OnceAsync<Usuario>();
                var existe = usuarios.FirstOrDefault(u => u.Object.Email.ToLower() == usuario.Email.Trim().ToLower());

                if (existe != null) return false;

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
                var usuarios = await _firebase.Child("Usuarios").OnceAsync<Usuario>();
                var coincidencia = usuarios.FirstOrDefault(u =>
                    u.Object.Email.Trim().ToLower() == email.Trim().ToLower() &&
                    u.Object.Password.Trim() == password.Trim());

                if (coincidencia != null)
                {
                    var u = coincidencia.Object;
                    if (u.Id == 0)
                    {
                        u.Id = Math.Abs(coincidencia.Key.GetHashCode());
                        await _firebase.Child("Usuarios").Child(coincidencia.Key).PutAsync(u);
                    }
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
                    .Where(u => u.TipoPerfil == "Técnico")
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

        // --- 4. CALIFICACIONES Y ELIMINACIÓN (AGREGADOS PARA CORREGIR EL ERROR) ---
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
    }
}