using Firebase.Database;
using Firebase.Database.Query;
using PHANTEL.Helpers;
using PHANTEL.Models;

namespace PHANTEL.Services
{
    /// <summary>
    /// Operaciones de la pantalla de Configuración. Va en la misma carpeta que CloudDatabaseService.cs
    /// y usa la misma base de Firebase. Todos los métodos invalidan la caché de usuarios al terminar.
    /// </summary>
    public class ConfiguracionService
    {
        private readonly FirebaseClient _firebase = new("https://phantel-enterprises-default-rtdb.firebaseio.com/");

        private async Task<FirebaseObject<Usuario>?> BuscarNodoAsync(string email)
        {
            var usuarios = await _firebase.Child("Usuarios").OnceAsync<Usuario>();
            string buscado = email.Trim().ToLower();
            return usuarios.FirstOrDefault(u =>
                u.Object != null && u.Object.Email.Trim().ToLower() == buscado);
        }

        /// <summary>
        /// Lee al usuario, aplica los cambios indicados y lo guarda completo.
        /// Ejemplo: ActualizarUsuarioAsync(email, u => u.Disponible = false)
        /// </summary>
        public async Task<bool> ActualizarUsuarioAsync(string email, Action<Usuario> cambios)
        {
            try
            {
                var nodo = await BuscarNodoAsync(email);
                if (nodo == null) return false;

                if (nodo.Object.Id == 0)
                    nodo.Object.Id = Math.Abs(nodo.Key.GetHashCode());

                cambios(nodo.Object);

                await _firebase.Child("Usuarios").Child(nodo.Key).PutAsync(nodo.Object);
                CloudDatabaseService.InvalidarCacheUsuarios();
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CONFIG ERROR ACTUALIZAR]: {ex.Message}");
                return false;
            }
        }

        private static bool PasswordCoincide(string guardada, string escrita)
        {
            string limpia = escrita.Trim();
            string hash = SecurityHelper.HashPassword(limpia);
            string g = guardada.Trim();
            // Se acepta también texto plano porque el login actual migra cuentas antiguas.
            return g == hash || g == limpia;
        }

        public async Task<bool> CambiarPasswordAsync(string email, string passwordActual, string passwordNueva)
        {
            try
            {
                var nodo = await BuscarNodoAsync(email);
                if (nodo == null) return false;
                if (!PasswordCoincide(nodo.Object.Password, passwordActual)) return false;

                nodo.Object.Password = SecurityHelper.HashPassword(passwordNueva.Trim());
                await _firebase.Child("Usuarios").Child(nodo.Key).PutAsync(nodo.Object);
                CloudDatabaseService.InvalidarCacheUsuarios();
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CONFIG ERROR PASSWORD]: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> EliminarCuentaAsync(string email, string password)
        {
            try
            {
                var nodo = await BuscarNodoAsync(email);
                if (nodo == null) return false;
                if (!PasswordCoincide(nodo.Object.Password, password)) return false;

                await _firebase.Child("Usuarios").Child(nodo.Key).DeleteAsync();
                CloudDatabaseService.InvalidarCacheUsuarios();
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CONFIG ERROR ELIMINAR]: {ex.Message}");
                return false;
            }
        }
    }
}
