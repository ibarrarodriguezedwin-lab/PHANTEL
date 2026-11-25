using Microsoft.Maui.Devices.Sensors;
using PHANTEL.Models;
using PHANTEL.Services;

namespace PHANTEL.Views
{
    public partial class ExploracionPage : ContentPage
    {
        private readonly CloudDatabaseService _dbService = new();
        private List<Usuario> _todosLosTecnicos = new();

        private string _especialidadSeleccionada = "Todos";
        private string _modalidadFiltro = "Todas"; // "Todas", "Domicilio" o "Local"
        private int _indiceOrdenSeleccionado = 0; // 0 = Recomendados, 1 = Mejor calificados, 2 = Más cercanos
        private (double Lat, double Lon)? _ubicacionCliente;

        private readonly List<CategoriaIcono> _categorias = new()
        {
            new() { Nombre = "Todos",                                 NombreCorto = "Todos",            Emoji = "🧰", EstaSeleccionada = true },
            new() { Nombre = "Electricista",                          NombreCorto = "Electricista",     Imagen = "electricista.png" },
            new() { Nombre = "Plomero",                               NombreCorto = "Plomero",          Imagen = "plomero.png" },
            new() { Nombre = "Cerrajero",                             NombreCorto = "Cerrajero",        Imagen = "cerrajero.png" },
            new() { Nombre = "Mecánico automotriz",                   NombreCorto = "Mecánico",         Imagen = "mecanico.png" },
            new() { Nombre = "Técnico en informática",                NombreCorto = "Informática",      Imagen = "informatica.png" },
            new() { Nombre = "Técnico en refrigeración",              NombreCorto = "Refrigeración",    Imagen = "refrigeracion.png" },
            new() { Nombre = "Carpintero",                            NombreCorto = "Carpintero",       Imagen = "carpintero.png" },
            new() { Nombre = "Técnico en electrónica",                NombreCorto = "Electrónica",      Imagen = "electronica.png" },
            new() { Nombre = "Pintor",                                NombreCorto = "Pintor",           Imagen = "pintor.png" },
            new() { Nombre = "Instalación de redes",                  NombreCorto = "Redes",            Imagen = "redes.png" },
            new() { Nombre = "Albañil",                               NombreCorto = "Albañil",          Imagen = "albanil.png" },
            new() { Nombre = "Herrero",                               NombreCorto = "Herrero",          Imagen = "herrero.png" },
            new() { Nombre = "Jardinero",                             NombreCorto = "Jardinero",        Imagen = "jardinero.png" },
            new() { Nombre = "Plomero destapa caños",                 NombreCorto = "Destapa caños",    Imagen = "destapacanos.png" },
            new() { Nombre = "Técnico en paneles solares",            NombreCorto = "Paneles solares",  Imagen = "panelessolares.png" },
            new() { Nombre = "Técnico en lavadoras y secadoras",      NombreCorto = "Lavadoras",        Imagen = "lavadoras.png" },
            new() { Nombre = "Técnico en aire acondicionado",         NombreCorto = "Aire acond.",      Imagen = "aireacondicionado.png" },
            new() { Nombre = "Impermeabilizador",                     NombreCorto = "Imperm.",          Imagen = "imperm.png" },
            new() { Nombre = "Yesero",                                NombreCorto = "Yesero",           Imagen = "yesero.png" },
            new() { Nombre = "Instalador de pisos",                   NombreCorto = "Pisos",            Imagen = "pisos.png" },
            new() { Nombre = "Técnico en cámaras de seguridad",       NombreCorto = "Cámaras",          Imagen = "camaras.png" },
            new() { Nombre = "Gasfitero / Gasero",                    NombreCorto = "Gas",              Imagen = "gas.png" },
            new() { Nombre = "Tapicero",                              NombreCorto = "Tapicero",         Imagen = "tapicero.png" },
            new() { Nombre = "Vidriero",                              NombreCorto = "Vidriero",         Imagen = "vidriero.png" },
            new() { Nombre = "Ebanista",                              NombreCorto = "Ebanista",         Imagen = "ebanista.png" },
            new() { Nombre = "Técnico en portones automáticos",       NombreCorto = "Portones",         Imagen = "portones.png" },
            new() { Nombre = "Instalador de paneles de yeso (Drywall)", NombreCorto = "Drywall",        Imagen = "drywall.png" },
            new() { Nombre = "Limpiador de cisternas",                NombreCorto = "Cisternas",        Imagen = "cisterna.png" },
            new() { Nombre = "Técnico en telefonía celular",          NombreCorto = "Celulares",        Imagen = "telefonos.png" },
            new() { Nombre = "Especialista en control de plagas",     NombreCorto = "Plagas",           Imagen = "plagas.png" },
        };

        public ExploracionPage()
        {
            InitializeComponent();
            carruselCategorias.ItemsSource = _categorias;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            ConfigurarCarruselSegunRol();
            CargarPerfilCliente();
            await CargarTecnicosAsync();
        }

        private void ConfigurarCarruselSegunRol()
        {
            string tipo = LoginPage.UsuarioActual?.TipoPerfil ?? "Cliente";
            bool esCliente = tipo != "Admin" && tipo != "Técnico";
            carruselCategorias.IsVisible = esCliente;
        }

        private void CargarPerfilCliente()
        {
            var usuarioActual = LoginPage.UsuarioActual;
            if (usuarioActual != null)
            {
                lblClienteNombre.Text = $"Hola, {usuarioActual.Nombre}";

                if (!string.IsNullOrEmpty(usuarioActual.FotoPerfil))
                {
                    try
                    {
                        string cleanBase64 = usuarioActual.FotoPerfil.Contains(",")
                            ? usuarioActual.FotoPerfil.Split(',')[1]
                            : usuarioActual.FotoPerfil;

                        byte[] imageBytes = Convert.FromBase64String(cleanBase64.Trim());
                        btnPerfilCliente.Source = ImageSource.FromStream(() => new MemoryStream(imageBytes));
                    }
                    catch
                    {
                        btnPerfilCliente.Source = "dotnet_bot.png";
                    }
                }
            }
        }

        private async Task CargarTecnicosAsync()
        {
            try
            {
                _todosLosTecnicos = await _dbService.ObtenerTecnicosAsync();
                AplicarFiltros();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR CARGA TECNICOS]: {ex.Message}");
            }
        }

        private void AplicarFiltros()
        {
            string especialidad = _especialidadSeleccionada;
            string textoBusqueda = entryBuscarTecnico.Text?.Trim().ToLower() ?? "";

            btnLimpiarRapido.IsVisible = !string.IsNullOrEmpty(textoBusqueda) || especialidad != "Todos" || _modalidadFiltro != "Todas";

            var filtrados = _todosLosTecnicos.Where(t =>
            {
                bool cumpleEspecialidad = especialidad == "Todos" ||
                                          t.Especialidad.Equals(especialidad, StringComparison.OrdinalIgnoreCase);

                bool cumpleTexto = string.IsNullOrEmpty(textoBusqueda) ||
                                   t.Nombre.ToLower().Contains(textoBusqueda) ||
                                   t.Especialidad.ToLower().Contains(textoBusqueda) ||
                                   (textoBusqueda.Contains("verificado") && t.EstaVerificado);

                bool cumpleModalidad = _modalidadFiltro switch
                {
                    "Domicilio" => t.AtiendeDomicilio,
                    "Local" => t.AtiendeLocal,
                    _ => true
                };

                return cumpleEspecialidad && cumpleTexto && cumpleModalidad;
            });

            IEnumerable<Usuario> ordenados = _indiceOrdenSeleccionado switch
            {
                1 => filtrados.OrderByDescending(t => t.PuntajeRecomendacion)
                              .ThenByDescending(t => t.EstaVerificado),

                2 when _ubicacionCliente != null => filtrados.OrderBy(t => DistanciaKm(t)),

                _ => filtrados.OrderByDescending(t => t.EstaVerificado)
                              .ThenByDescending(t => t.PuntajeRecomendacion)
            };

            listTecnicos.ItemsSource = ordenados.ToList();
            ActualizarSeleccionIconos(especialidad);
        }

        private void ActualizarSeleccionIconos(string especialidad)
        {
            foreach (var cat in _categorias)
            {
                cat.EstaSeleccionada = cat.Nombre.Equals(especialidad, StringComparison.OrdinalIgnoreCase);
            }
        }

        private double DistanciaKm(Usuario tecnico)
        {
            if (_ubicacionCliente == null || (tecnico.Latitud == 0 && tecnico.Longitud == 0))
                return double.MaxValue;

            return Haversine(_ubicacionCliente.Value.Lat, _ubicacionCliente.Value.Lon,
                             tecnico.Latitud, tecnico.Longitud);
        }

        private static double Haversine(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371.0;
            double dLat = (lat2 - lat1) * Math.PI / 180.0;
            double dLon = (lon2 - lon1) * Math.PI / 180.0;
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0) *
                       Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        }

        private async Task<(double Lat, double Lon)?> ObtenerUbicacionClienteAsync()
        {
            var u = LoginPage.UsuarioActual;
            if (u != null && (u.Latitud != 0 || u.Longitud != 0))
                return (u.Latitud, u.Longitud);

            try
            {
                var loc = await Geolocation.Default.GetLastKnownLocationAsync()
                          ?? await Geolocation.Default.GetLocationAsync(
                                 new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10)));

                if (loc != null)
                    return (loc.Latitude, loc.Longitude);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR UBICACION]: {ex.Message}");
            }

            return null;
        }

        private void OnSearchTextChanged(object? sender, TextChangedEventArgs e) => AplicarFiltros();

        private async void OnOrdenarPillTapped(object? sender, TappedEventArgs e)
        {
            string opcion = await DisplayActionSheetAsync(
                "Ordenar técnicos por:",
                "Cancelar",
                null,
                "⭐ Recomendados",
                "🏆 Mejor calificados",
                "📍 Más cercanos");

            switch (opcion)
            {
                case "⭐ Recomendados":
                    _indiceOrdenSeleccionado = 0;
                    lblOrdenActual.Text = "Recomendados";
                    break;

                case "🏆 Mejor calificados":
                    _indiceOrdenSeleccionado = 1;
                    lblOrdenActual.Text = "Mejor calificados";
                    break;

                case "📍 Más cercanos":
                    if (_ubicacionCliente == null)
                    {
                        _ubicacionCliente = await ObtenerUbicacionClienteAsync();
                        if (_ubicacionCliente == null)
                        {
                            await DisplayAlertAsync("Ubicación no disponible",
                                "No pudimos obtener tu ubicación. Activa el permiso para ordenar por cercanía.",
                                "OK");
                            return;
                        }
                    }
                    _indiceOrdenSeleccionado = 2;
                    lblOrdenActual.Text = "Más cercanos";
                    break;

                default:
                    return;
            }

            AplicarFiltros();
        }

        private void OnLimpiarFiltrosClicked(object? sender, EventArgs e)
        {
            entryBuscarTecnico.Text = string.Empty;
            _modalidadFiltro = "Todas";
            lblModalidadActual.Text = "Todas";
            _especialidadSeleccionada = "Todos";
            _indiceOrdenSeleccionado = 0;
            lblOrdenActual.Text = "Recomendados";
            AplicarFiltros();
        }

        private void OnCategoriaTapped(object? sender, TappedEventArgs e)
        {
            if (sender is not BindableObject vista || vista.BindingContext is not CategoriaIcono categoria)
                return;

            _especialidadSeleccionada = _especialidadSeleccionada == categoria.Nombre
                ? "Todos"
                : categoria.Nombre;

            AplicarFiltros();
        }

        private async void OnPerfilClienteClicked(object sender, EventArgs e)
        {
            if (LoginPage.UsuarioActual == null) return;
            await Navigation.PushAsync(new ConfiguracionPage());
        }

        private async Task CambiarFotoPerfilAsync()
        {
            try
            {
                var photos = await MediaPicker.Default.PickPhotosAsync(new MediaPickerOptions
                {
                    Title = "Selecciona tu nueva foto de perfil"
                });

                var photo = photos?.FirstOrDefault();
                if (photo != null && LoginPage.UsuarioActual != null)
                {
                    using var stream = await photo.OpenReadAsync();
                    using var ms = new MemoryStream();
                    await stream.CopyToAsync(ms);
                    string base64 = Convert.ToBase64String(ms.ToArray());

                    bool guardado = await _dbService.ActualizarFotoPerfilAsync(LoginPage.UsuarioActual.Id, base64);
                    if (guardado)
                    {
                        LoginPage.UsuarioActual.FotoPerfil = base64;
                        btnPerfilCliente.Source = ImageSource.FromStream(() => new MemoryStream(Convert.FromBase64String(base64)));
                        await DisplayAlertAsync("Éxito", "Tu foto de perfil se actualizó correctamente.", "OK");
                    }
                    else
                    {
                        await DisplayAlertAsync("Error", "No se pudo guardar la foto en la nube.", "OK");
                    }
                }
            }
            catch (Exception ex)
            {
                await DisplayAlertAsync("Error", $"No se pudo cambiar la foto: {ex.Message}", "OK");
            }
        }

        private async void OnModalidadPillTapped(object? sender, TappedEventArgs e)
        {
            string? opcion = await DisplayActionSheetAsync(
                "Tipo de atención", "Cancelar", null,
                "Todas", "A domicilio", "En local fijo");

            switch (opcion)
            {
                case "Todas":
                    _modalidadFiltro = "Todas";
                    break;
                case "A domicilio":
                    _modalidadFiltro = "Domicilio";
                    break;
                case "En local fijo":
                    _modalidadFiltro = "Local";
                    break;
                default:
                    return;
            }

            lblModalidadActual.Text = opcion;
            AplicarFiltros();
        }

        private async void OnSolicitarClicked(object sender, EventArgs e)
        {
            var button = sender as Button;
            var tecnico = button?.CommandParameter as Usuario;
            if (tecnico != null)
            {
                await Navigation.PushAsync(new NuevaSolicitudPage(tecnico));
            }
        }

        private async void OnTecnicoSeleccionado(object sender, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection.FirstOrDefault() is Usuario tecnico)
            {
                await Navigation.PushAsync(new PerfilTecnicoPage(tecnico));
                listTecnicos.SelectedItem = null;
            }
        }
    }
}