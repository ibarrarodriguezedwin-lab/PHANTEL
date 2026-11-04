using PHANTEL.Models;
using SQLite;

namespace PHANTEL.Services
{
    public class DatabaseService
    {
        private SQLiteAsyncConnection? _db;

        public async Task InitAsync()
        {
            if (_db != null) return;

            try
            {
                var folderPath = FileSystem.AppDataDirectory;
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                var dbPath = Path.Combine(folderPath, "phantel_db_v5.db3");
                _db = new SQLiteAsyncConnection(dbPath);

                await _db.CreateTableAsync<Usuario>();
                await _db.CreateTableAsync<SolicitudCita>();
                await _db.CreateTableAsync<Calificacion>();

                var conteo = await _db.Table<Usuario>().CountAsync();
                if (conteo == 0)
                {
                    var usuariosSemilla = new List<Usuario>
                    {
                        new Usuario { Nombre = "Edwin Ibarra", Email = "cliente@phantel.com", Password = "123", TipoPerfil = "Cliente" },
                        new Usuario { Nombre = "Carlos Mendoza", Email = "tecnico@phantel.com", Password = "123", TipoPerfil = "Técnico", Especialidad = "Plomería", CalificacionPromedio = 4.8, Latitud = 19.3500, Longitud = -99.1400 },
                        new Usuario { Nombre = "Ana Gómez", Email = "ana@phantel.com", Password = "123", TipoPerfil = "Técnico", Especialidad = "Electricidad", CalificacionPromedio = 4.9, Latitud = 19.3400, Longitud = -99.1350 }
                    };

                    await _db.InsertAllAsync(usuariosSemilla);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al inicializar BD: {ex.Message}");
            }
        }

        public async Task<Usuario?> ValidarLoginAsync(string email, string password)
        {
            await InitAsync();
            if (_db == null) return null;

            try
            {
                return await _db.Table<Usuario>()
                                .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower() && u.Password == password);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en Login: {ex.Message}");
                return null;
            }
        }

        public async Task<int> GuardarUsuarioAsync(Usuario user)
        {
            await InitAsync();
            if (_db == null) return 0;
            if (user.Id != 0)
                return await _db.UpdateAsync(user);
            return await _db.InsertAsync(user);
        }

        public async Task<List<Usuario>> ObtenerTecnicosPorEspecialidadAsync(string especialidad, string filtroTexto)
        {
            await InitAsync();
            if (_db == null) return new List<Usuario>();

            var query = _db.Table<Usuario>().Where(u => u.TipoPerfil == "Técnico");

            if (!string.IsNullOrWhiteSpace(especialidad) && especialidad != "Todos")
                query = query.Where(u => u.Especialidad == especialidad);

            if (!string.IsNullOrWhiteSpace(filtroTexto))
                query = query.Where(u => u.Nombre.Contains(filtroTexto));

            return await query.ToListAsync();
        }

        public async Task<int> CrearSolicitudAsync(SolicitudCita cita)
        {
            await InitAsync();
            if (_db == null) return 0;
            return await _db.InsertAsync(cita);
        }

        public async Task<int> ActualizarEstadoSolicitudAsync(int citaId, string nuevoEstado)
        {
            await InitAsync();
            if (_db == null) return 0;
            var cita = await _db.Table<SolicitudCita>().FirstOrDefaultAsync(c => c.Id == citaId);
            if (cita != null)
            {
                cita.Estado = nuevoEstado;
                return await _db.UpdateAsync(cita);
            }
            return 0;
        }

        // --- MÉTODO AGREGADO PARA RESOLVER EL ERROR CS1061 ---
        public async Task<int> EliminarSolicitudAsync(int citaId)
        {
            await InitAsync();
            if (_db == null) return 0;
            return await _db.DeleteAsync<SolicitudCita>(citaId);
        }

        public async Task<List<SolicitudCita>> ObtenerSolicitudesPorUsuarioAsync(int usuarioId, bool esTecnico)
        {
            await InitAsync();
            if (_db == null) return new List<SolicitudCita>();
            if (esTecnico)
                return await _db.Table<SolicitudCita>().Where(c => c.TecnicoId == usuarioId).ToListAsync();
            return await _db.Table<SolicitudCita>().Where(c => c.ClienteId == usuarioId).ToListAsync();
        }

        public static double CalcularDistanciaKm(double lat1, double lon1, double lat2, double lon2)
        {
            double R = 6371;
            double dLat = ToRadians(lat2 - lat1);
            double dLon = ToRadians(lon2 - lon1);
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                       Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return Math.Round(R * c, 2);
        }

        public static decimal CalcularTarifaTraslado(double distanciaKm)
        {
            decimal tarifaBase = 35.00m;
            decimal costoPorKm = 12.50m;
            return tarifaBase + (decimal)distanciaKm * costoPorKm;
        }

        private static double ToRadians(double angle) => (Math.PI / 180) * angle;
    }
}