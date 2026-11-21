using Microsoft.Maui.Devices.Sensors;
using PHANTEL.Models;
using PHANTEL.Services;

namespace PHANTEL.Views
{
    public partial class ExploracionPage : ContentPage
    {
        private readonly CloudDatabaseService _dbService = new();
        private List<Usuario> _todosLosTecnicos = new();

        // Especialidad elegida con los iconos ("Todos" = sin filtro)
        private string _especialidadSeleccionada = "Todos";

        // Ubicación del cliente (se obtiene solo cuando elige "Más cercanos")
        private (double Lat, double Lon)? _ubicacionCliente;

        // --- CARRUSEL DE ICONOS (solo clientes) ---
        // "Nombre" debe ser idéntico a la Especialidad guardada en la base de datos.
        // Para usar PNG propios, llena Imagen = "plomero.png".
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
            pickerOrden.SelectedIndex = 0; // "Recomendados"
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            ConfigurarCarruselSegunRol();
            CargarPerfilCliente();
            await CargarTecnicosAsync();
        }

        /// <summary>
        /// El carrusel solo se muestra a clientes. Técnicos y administradores no lo ven.
        /// </summary>
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
                // Ya vienen ordenados: primero los técnicos con EstadoVerificacion == "Verificado" y luego por calificación
                _todosLosTecnicos = await _dbService.ObtenerTecnicosAsync();
                AplicarFiltros();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR CARGA TECNICOS]: {ex.Message}");
            }
        }

        /// <summary>
        /// 1) Filtra por especialidad (iconos) y texto (buscador).
        /// 2) Ordena según el Picker "Ordenar por".
        /// </summary>
        private void AplicarFiltros()
        {
            string especialidad = _especialidadSeleccionada;
            string textoBusqueda = searchBarTecnicos.Text?.Trim().ToLower() ?? "";

            var filtrados = _todosLosTecnicos.Where(t =>
            {
                bool cumpleEspecialidad = especialidad == "Todos" ||
                                          t.Especialidad.Equals(especialidad, StringComparison.OrdinalIgnoreCase);

                bool cumpleTexto = string.IsNullOrEmpty(textoBusqueda) ||
                                   t.Nombre.ToLower().Contains(textoBusqueda) ||
                                   t.Especialidad.ToLower().Contains(textoBusqueda) ||
                                   (textoBusqueda.Contains("verificado") && t.EstaVerificado);

                return cumpleEspecialidad && cumpleTexto;
            });

            IEnumerable<Usuario> ordenados = pickerOrden.SelectedIndex switch
            {
                // Mejor calificados
                1 => filtrados.OrderByDescending(t => t.CalificacionPromedio)
                              .ThenByDescending(t => t.EstaVerificado),

                // Más cercanos (solo si ya tenemos la ubicación del cliente)
                2 when _ubicacionCliente != null => filtrados.OrderBy(t => DistanciaKm(t)),

                // Recomendados: verificados primero y luego por calificación
                _ => filtrados.OrderByDescending(t => t.EstaVerificado)
                              .ThenByDescending(t => t.CalificacionPromedio)
            };

            listTecnicos.ItemsSource = ordenados.ToList();

            ActualizarSeleccionIconos(especialidad);
        }

        /// <summary>
        /// Resalta en el carrusel el icono que coincide con la especialidad elegida.
        /// </summary>
        private void ActualizarSeleccionIconos(string especialidad)
        {
            foreach (var cat in _categorias)
            {
                cat.EstaSeleccionada = cat.Nombre.Equals(especialidad, StringComparison.OrdinalIgnoreCase);
            }
        }

        // --- DISTANCIA / UBICACIÓN ---

        private double DistanciaKm(Usuario tecnico)
        {
            // Técnicos sin ubicación guardada se van al final
            if (_ubicacionCliente == null || (tecnico.Latitud == 0 && tecnico.Longitud == 0))
                return double.MaxValue;

            return Haversine(_ubicacionCliente.Value.Lat, _ubicacionCliente.Value.Lon,
                             tecnico.Latitud, tecnico.Longitud);
        }

        private static double Haversine(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371.0; // radio de la Tierra en km
            double dLat = (lat2 - lat1) * Math.PI / 180.0;
            double dLon = (lon2 - lon1) * Math.PI / 180.0;
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0) *
                       Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        }

        private async Task<(double Lat, double Lon)?> ObtenerUbicacionClienteAsync()
        {
            // 1) Si el cliente ya tiene ubicación guardada en su perfil, se usa esa
            var u = LoginPage.UsuarioActual;
            if (u != null && (u.Latitud != 0 || u.Longitud != 0))
                return (u.Latitud, u.Longitud);

            // 2) Si no, se pide la ubicación actual del dispositivo
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

        // --- EVENTOS DE BÚSQUEDA, ORDEN Y LIMPIAR ---

        private void OnSearchTextChanged(object sender, TextChangedEventArgs e) => AplicarFiltros();

        private async void OnOrdenChanged(object? sender, EventArgs e)
        {
            // "Más cercanos": necesita la ubicación del cliente
            if (pickerOrden.SelectedIndex == 2 && _ubicacionCliente == null)
            {
                _ubicacionCliente = await ObtenerUbicacionClienteAsync();

                if (_ubicacionCliente == null)
                {
                    await DisplayAlertAsync("Ubicación no disponible",
                        "No pudimos obtener tu ubicación. Activa el permiso de ubicación para ordenar por cercanía.",
                        "OK");
                    pickerOrden.SelectedIndex = 0; // vuelve a "Recomendados"
                    return;
                }
            }

            AplicarFiltros();
        }

        private void OnLimpiarFiltrosClicked(object sender, EventArgs e)
        {
            searchBarTecnicos.Text = string.Empty;
            _especialidadSeleccionada = "Todos";
            pickerOrden.SelectedIndex = 0; // "Recomendados"
            AplicarFiltros();
        }

        // --- CARRUSEL DE ICONOS: AL TOCAR UN ICONO SE FILTRA POR ESA ESPECIALIDAD ---

        private void OnCategoriaTapped(object? sender, TappedEventArgs e)
        {
            if (sender is not BindableObject vista || vista.BindingContext is not CategoriaIcono categoria)
                return;

            // Tocar de nuevo el mismo icono quita el filtro
            _especialidadSeleccionada = _especialidadSeleccionada == categoria.Nombre
                ? "Todos"
                : categoria.Nombre;

            AplicarFiltros();
        }

        // --- MANEJO DEL MENÚ DE PERFIL DEL CLIENTE ---

        private async void OnPerfilClienteClicked(object sender, EventArgs e)
        {
            var u = LoginPage.UsuarioActual;
            if (u == null) return;

            string opcion = await DisplayActionSheetAsync(
                $"Cuenta: {u.Nombre}",
                "Cancelar",
                null,
                "📷 Editar Foto de Perfil",
                "📋 Historial de Citas",
                "🚪 Cerrar Sesión");

            switch (opcion)
            {
                case "📷 Editar Foto de Perfil":
                    await CambiarFotoPerfilAsync();
                    break;

                case "📋 Historial de Citas":
                    await Navigation.PushAsync(new MisSolicitudesPage());
                    break;

                case "🚪 Cerrar Sesión":
                    Preferences.Default.Remove("SesionEmail");
                    LoginPage.UsuarioActual = null;
                    Application.Current!.Windows[0].Page = new NavigationPage(new LoginPage());
                    break;
            }
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

        // --- SOLICITUDES DE TÉCNICOS ---

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
                await Navigation.PushAsync(new NuevaSolicitudPage(tecnico));
                listTecnicos.SelectedItem = null;
            }
        }
    }
}
