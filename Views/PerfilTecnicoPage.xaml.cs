using PHANTEL.Helpers;
using PHANTEL.Models;
using PHANTEL.Services;

namespace PHANTEL.Views
{
    public partial class PerfilTecnicoPage : ContentPage
    {
        private readonly Usuario _tecnico;
        private readonly bool _soloLectura;
        private readonly CloudDatabaseService _cloud = new();

        /// <param name="tecnico">Técnico a mostrar.</param>
        /// <param name="soloLectura">true cuando el técnico ve sus propias reseñas (oculta "Solicitar servicio").</param>
        public PerfilTecnicoPage(Usuario tecnico, bool soloLectura = false)
        {
            InitializeComponent();

            _tecnico = tecnico;
            _soloLectura = soloLectura;
            BindingContext = tecnico;

            Title = soloLectura ? Textos.T("cfg.resenas") : "Perfil del técnico";
            panelSolicitar.IsVisible = !soloLectura;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await CargarResenasAsync();
        }

        private async Task CargarResenasAsync()
        {
            var resenas = await _cloud.ObtenerCalificacionesTecnicoAsync(_tecnico);

            BindableLayout.SetItemsSource(listaResenas, resenas);
            lblSinResenas.IsVisible = resenas.Count == 0;
            lblNuevo.IsVisible = resenas.Count < 3;

            if (resenas.Count > 0)
            {
                double promedio = Math.Round(resenas.Average(r => r.Puntuacion), 1);
                promedio = Math.Max(1.0, Math.Min(5.0, promedio + _tecnico.AjusteReputacion));

                int llenas = (int)Math.Round(promedio);
                lblPromedio.Text = $"{promedio:F1}";
                lblEstrellasResumen.Text = new string('★', llenas) + new string('☆', 5 - llenas);
                lblTotalResenas.Text = resenas.Count == 1 ? "1 reseña" : $"{resenas.Count} reseñas";
            }
            else
            {
                lblPromedio.Text = "—";
                lblEstrellasResumen.Text = "☆☆☆☆☆";
                lblTotalResenas.Text = "Aún sin reseñas";
            }
        }

        private async void OnSolicitarClicked(object sender, EventArgs e)
        {
            if (_soloLectura) return;
            await Navigation.PushAsync(new NuevaSolicitudPage(_tecnico));
        }
    }
}