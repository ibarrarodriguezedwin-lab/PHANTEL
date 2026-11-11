using PHANTEL.Models;
using PHANTEL.Services;

namespace PHANTEL.Views
{
    public partial class ExploracionPage : ContentPage
    {
        private readonly CloudDatabaseService _dbService = new();
        private List<Usuario> _todosLosTecnicos = new();

        public ExploracionPage()
        {
            InitializeComponent();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            CargarPerfilCliente();
            await CargarTecnicosAsync();
        }

        private void CargarPerfilCliente()
        {
            var usuarioActual = LoginPage.UsuarioActual;
            if (usuarioActual != null)
            {
                lblClienteNombre.Text = $"Hola, {usuarioActual.Nombre}";

                // Cargar Foto de Perfil en el ImageButton si existe en Base64
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
                // Carga los técnicos desde Firebase Realtime Database
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
            string especialidad = pickerEspecialidad.SelectedItem?.ToString() ?? "Todos";
            string textoBusqueda = searchBarTecnicos.Text?.Trim().ToLower() ?? "";

            var filtrados = _todosLosTecnicos.Where(t =>
            {
                bool cumpleEspecialidad = especialidad == "Todos" ||
                                         t.Especialidad.Equals(especialidad, StringComparison.OrdinalIgnoreCase);

                bool cumpleTexto = string.IsNullOrEmpty(textoBusqueda) ||
                                   t.Nombre.ToLower().Contains(textoBusqueda) ||
                                   t.Especialidad.ToLower().Contains(textoBusqueda);

                return cumpleEspecialidad && cumpleTexto;
            }).ToList();

            listTecnicos.ItemsSource = filtrados;
        }

        private void OnSearchTextChanged(object sender, TextChangedEventArgs e) => AplicarFiltros();
        private void OnEspecialidadChanged(object sender, EventArgs e) => AplicarFiltros();

        private void OnLimpiarFiltrosClicked(object sender, EventArgs e)
        {
            searchBarTecnicos.Text = string.Empty;
            pickerEspecialidad.SelectedIndex = 0;
            AplicarFiltros();
        }

        // --- MANEJO DEL MENÚ DE PERFIL DEL CLIENTE ---


        private async void OnVerCuentaClienteClicked(object sender, EventArgs e)
        {
            var u = LoginPage.UsuarioActual;
            if (u != null)
            {
                await DisplayAlert("Mi Cuenta PHANTEL",
                    $"Nombre: {u.Nombre}\n" +
                    $"Correo: {u.Email}\n" +
                    $"Teléfono: {u.Telefono}\n" +
                    $"Dirección: {u.Direccion}\n" +
                    $"Tipo de Perfil: {u.TipoPerfil}", "Aceptar");
            }
        }

        private void OnCerrarSesionClicked(object sender, EventArgs e)
        {
            LoginPage.UsuarioActual = null;
            Application.Current!.Windows[0].Page = new NavigationPage(new LoginPage());
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