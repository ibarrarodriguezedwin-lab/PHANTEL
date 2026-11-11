using Firebase.Database;
using Firebase.Database.Query;
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

                // Crear registro en Firebase
                var result = await _firebase.Child("Usuarios").PostAsync(usuario);

                // Generar un ID entero positivo unico basado en la clave de Firebase
                int idUnico = Math.Abs(result.Key.GetHashCode());
                usuario.Id = idUnico;

                // Actualizar el objeto en Firebase con su ID asignado
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

                    // Si en la base de datos el ID es 0, se lo asignamos y guardamos en Firebase
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
                    // Busca coincidencia por TecnicoId directo
                    var lista = solicitudes
                        .Select(s => {
                            var obj = s.Object;
                            if (obj.Id == 0) obj.Id = Math.Abs(s.Key.GetHashCode());
                            return obj;
                        })
                        .Where(s => s.TecnicoId == usuarioId)
                        .ToList();

                    // Si no encontró coincidencia exacta por ID (por citas agendadas antes del cambio de ID), 
                    // trae las solicitudes pendientes asociadas
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
    }
}