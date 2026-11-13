namespace PHANTEL.Views
{
    public partial class CalificacionPopup : ContentPage
    {
        private int _estrellasSeleccionadas = 5;
        public int PuntuacionResultado => _estrellasSeleccionadas;
        public string ComentarioResultado => editorComentario.Text?.Trim() ?? string.Empty;
        public bool FueConfirmado { get; private set; } = false;

        public CalificacionPopup(string rolEvaluador)
        {
            InitializeComponent();

            if (rolEvaluador == "Tecnico")
            {
                lblTituloCalificacion.Text = "Calificar al Cliente";
                lblSubtitulo.Text = "¿Qué tal fue la atención y trato del cliente?";
            }
            else
            {
                lblTituloCalificacion.Text = "Calificar al Técnico";
                lblSubtitulo.Text = "¿Qué tal te pareció el servicio realizado?";
            }

            PintarEstrellas(5);
        }

        private void OnEstrella1Clicked(object sender, EventArgs e) => SeleccionarEstrellas(1);
        private void OnEstrella2Clicked(object sender, EventArgs e) => SeleccionarEstrellas(2);
        private void OnEstrella3Clicked(object sender, EventArgs e) => SeleccionarEstrellas(3);
        private void OnEstrella4Clicked(object sender, EventArgs e) => SeleccionarEstrellas(4);
        private void OnEstrella5Clicked(object sender, EventArgs e) => SeleccionarEstrellas(5);

        private void SeleccionarEstrellas(int cantidad)
        {
            _estrellasSeleccionadas = cantidad;
            PintarEstrellas(cantidad);

            lblPuntuacionTexto.Text = cantidad switch
            {
                1 => "1 ★ - Malo",
                2 => "2 ★ - Regular",
                3 => "3 ★ - Bueno",
                4 => "4 ★ - Muy Bueno",
                5 => "5 ★ - Excelente",
                _ => "5 ★"
            };
        }

        private void PintarEstrellas(int cantidad)
        {
            btnS1.TextColor = cantidad >= 1 ? Colors.Gold : Colors.Gray;
            btnS2.TextColor = cantidad >= 2 ? Colors.Gold : Colors.Gray;
            btnS3.TextColor = cantidad >= 3 ? Colors.Gold : Colors.Gray;
            btnS4.TextColor = cantidad >= 4 ? Colors.Gold : Colors.Gray;
            btnS5.TextColor = cantidad >= 5 ? Colors.Gold : Colors.Gray;
        }

        private async void OnGuardarClicked(object sender, EventArgs e)
        {
            FueConfirmado = true;
            await Navigation.PopModalAsync();
        }

        private async void OnCancelarClicked(object sender, EventArgs e)
        {
            FueConfirmado = false;
            await Navigation.PopModalAsync();
        }
    }
}