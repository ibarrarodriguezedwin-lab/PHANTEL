using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using PHANTEL.Models;
using SQLite;

namespace PHANTEL.Services
{
    public class DatabaseService
    {
        private SQLiteAsyncConnection? _db;
        private static readonly HttpClient _httpClient = CreateHttpClient();

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("PHANTEL-App/1.0 (contacto@phantel.mx)");
            return client;
        }

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
                        new Usuario { Nombre = "Edwin Ibarra", Email = "cliente@phantel.com", Password = "123", TipoPerfil = "Cliente", CodigoPostal = "04400", Latitud = 19.3467, Longitud = -99.1617 },
                        new Usuario { Nombre = "Carlos Mendoza", Email = "tecnico@phantel.com", Password = "123", TipoPerfil = "Técnico", Especialidad = "Plomería", CalificacionPromedio = 4.8, CodigoPostal = "04300", Latitud = 19.3500, Longitud = -99.1400 },
                        new Usuario { Nombre = "Ana Gómez", Email = "ana@phantel.com", Password = "123", TipoPerfil = "Técnico", Especialidad = "Electricidad", CalificacionPromedio = 4.9, CodigoPostal = "03100", Latitud = 19.3400, Longitud = -99.1350 }
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

        // --- GEOLOCALIZACIÓN REAL POR CP / DIRECCIÓN Y TARIFA ($8.00 MXN/KM ÍNTEGRO) ---
        public static async Task<(double lat, double lon)> ObtenerCoordenadasPorDireccionOCpAsync(string consulta, string? cpRespaldo = null)
        {
            // Detectar si la dirección escrita ya incluye un Código Postal de 5 dígitos (ej. "01090")
            string? cpDetectado = null;
            if (!string.IsNullOrWhiteSpace(consulta))
            {
                var matchCp = Regex.Match(consulta, @"\b\d{5}\b");
                if (matchCp.Success)
                    cpDetectado = matchCp.Value;
            }

            string cpEfectivo = !string.IsNullOrWhiteSpace(cpDetectado) ? cpDetectado : (cpRespaldo ?? string.Empty);

            // 1. Consulta en OpenStreetMap (Nominatim) - funciona idéntico en Windows y Android
            var coordsNominatim = await ConsultarNominatimAsync(consulta);
            if (coordsNominatim.HasValue)
                return coordsNominatim.Value;

            // 2. Si la calle completa no apareció, consultar directamente por el CP detectado (ej. "01090, Ciudad de México")
            if (!string.IsNullOrWhiteSpace(cpEfectivo) && cpEfectivo.Length == 5)
            {
                var coordsCp = await ConsultarNominatimAsync($"{cpEfectivo}, Ciudad de México, México");
                if (coordsCp.HasValue)
                    return coordsCp.Value;
            }

            // 3. Geocodificador nativo MAUI como tercer nivel
            try
            {
                if (!string.IsNullOrWhiteSpace(consulta))
                {
                    var ubicaciones = await Geocoding.Default.GetLocationsAsync($"{consulta}, México");
                    var primera = ubicaciones?.FirstOrDefault();
                    if (primera != null && Math.Abs(primera.Latitude) > 0.1)
                        return (primera.Latitude, primera.Longitude);
                }
            }
            catch { }

            // 4. Respaldo por prefijo de alcaldía CDMX
            if (!string.IsNullOrWhiteSpace(cpEfectivo) && cpEfectivo.Length >= 2)
            {
                return cpEfectivo.Substring(0, 2) switch
                {
                    "01" => (19.3587, -99.2033), // Álvaro Obregón
                    "02" => (19.4841, -99.1844), // Azcapotzalco
                    "03" => (19.3719, -99.1578), // Benito Juárez
                    "04" => (19.3467, -99.1617), // Coyoacán
                    "05" => (19.3573, -99.2997), // Cuajimalpa
                    "06" => (19.4326, -99.1332), // Cuauhtémoc
                    "07" => (19.4829, -99.1135), // Gustavo A. Madero
                    "08" => (19.3955, -99.0977), // Iztacalco
                    "09" => (19.3574, -99.0671), // Iztapalapa
                    "10" => (19.3050, -99.2414), // Magdalena Contreras
                    "11" => (19.4073, -99.1908), // Miguel Hidalgo
                    "12" => (19.1922, -99.0231), // Milpa Alta
                    "13" => (19.2869, -99.0142), // Tláhuac
                    "14" => (19.2671, -99.2030), // Tlalpan / Ajusco Medio
                    "15" => (19.4236, -99.1044), // Venustiano Carranza
                    "16" => (19.2634, -99.1048), // Xochimilco
                    _ => (19.3452, -99.1384)
                };
            }

            return (19.3452, -99.1384);
        }

        private static async Task<(double lat, double lon)?> ConsultarNominatimAsync(string? textoBusqueda)
        {
            if (string.IsNullOrWhiteSpace(textoBusqueda)) return null;

            try
            {
                string q = textoBusqueda.Contains("México", StringComparison.OrdinalIgnoreCase)
                    ? textoBusqueda
                    : $"{textoBusqueda}, México";

                string url = $"https://nominatim.openstreetmap.org/search?format=json&limit=1&countrycodes=mx&q={Uri.EscapeDataString(q)}";
                string json = await _httpClient.GetStringAsync(url);

                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
                {
                    var item = doc.RootElement[0];
                    if (item.TryGetProperty("lat", out var latProp) &&
                        item.TryGetProperty("lon", out var lonProp) &&
                        double.TryParse(latProp.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double lat) &&
                        double.TryParse(lonProp.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double lon) &&
                        Math.Abs(lat) > 0.1)
                    {
                        return (lat, lon);
                    }
                }
            }
            catch { }

            return null;
        }

        public static double CalcularDistanciaKm(double lat1, double lon1, double lat2, double lon2)
        {
            if (Math.Abs(lat1) < 0.1 || Math.Abs(lon1) < 0.1 || Math.Abs(lat2) < 0.1 || Math.Abs(lon2) < 0.1)
                return 2.0;

            double R = 6371.0;
            double dLat = ToRadians(lat2 - lat1);
            double dLon = ToRadians(lon2 - lon1);
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                       Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            // Factor de ruta urbana (1.30x sobre línea recta para reflejar vialidades reales)
            return Math.Round((R * c) * 1.30, 1);
        }

        public static decimal CalcularTarifaTraslado(double distanciaKm)
        {
            // $8.00 MXN por km desde el CP del técnico hasta el domicilio del servicio (mínimo $16.00 MXN, libre de comisión)
            decimal costoPorKm = 8.00m;
            decimal tarifa = (decimal)distanciaKm * costoPorKm;
            if (tarifa < 16.00m) tarifa = 16.00m;

            return Math.Round(tarifa, 2);
        }

        private static double ToRadians(double angle) => (Math.PI / 180) * angle;
    }
}