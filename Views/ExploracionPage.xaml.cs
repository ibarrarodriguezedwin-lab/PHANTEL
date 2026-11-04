using PHANTEL.Models;
using PHANTEL.Services;

namespace PHANTEL.Views
{
    public partial class ExploracionPage : ContentPage
    {
        private readonly DatabaseService _dbService = new();

        public ExploracionPage()
        {
            InitializeComponent();
            pickerEspecialidad.SelectedIndex = 0;
            CargarTecnicos();
        }

        private async void CargarTecnicos()
        {
            string especialidad = pickerEspecialidad.SelectedItem?.ToString() ?? "Todos";
            string texto = searchBarTecnicos.Text;
            var lista = await _dbService.ObtenerTecnicosPorEspecialidadAsync(especialidad, texto);
            listTecnicos.ItemsSource = lista;
        }

        private void OnSearchTextChanged(object sender, TextChangedEventArgs e) => CargarTecnicos();
        private void OnEspecialidadChanged(object sender, EventArgs e) => CargarTecnicos();

        private void OnLimpiarFiltrosClicked(object sender, EventArgs e)
        {
            searchBarTecnicos.Text = string.Empty;
            pickerEspecialidad.SelectedIndex = 0;
            CargarTecnicos();
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
                await Navigation.PushAsync(new NuevaSolicitudPage(tecnico));
                listTecnicos.SelectedItem = null;
            }
        }
    }
}