using PHANTEL.Models;
using PHANTEL.Services;

namespace PHANTEL.Views
{
    public partial class AdminVerificacionesPage : ContentPage
    {
        private readonly CloudDatabaseService _dbService = new();

        public AdminVerificacionesPage()
        {
            InitializeComponent();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await CargarPendientesAsync();
        }

        private async Task CargarPendientesAsync()
        {
            var lista = await _dbService.ObtenerTecnicosPendientesVerificacionAsync();
            listPendientes.ItemsSource = lista;
        }

        private async void OnRevisarDocumentosClicked(object sender, EventArgs e)
        {
            if ((sender as Button)?.CommandParameter is not Usuario tecnico) return;

            // Mostrar una página modal rápida con ambas fotos de INE y botones Aprobar / Rechazar
            var imgFrente = new Image { HeightRequest = 200, Aspect = Aspect.AspectFit };
            if (!string.IsNullOrWhiteSpace(tecnico.IneFrenteBase64))
                imgFrente.Source = ImageSource.FromStream(() => new MemoryStream(Convert.FromBase64String(tecnico.IneFrenteBase64)));

            var imgReverso = new Image { HeightRequest = 200, Aspect = Aspect.AspectFit };
            if (!string.IsNullOrWhiteSpace(tecnico.IneReversoBase64))
                imgReverso.Source = ImageSource.FromStream(() => new MemoryStream(Convert.FromBase64String(tecnico.IneReversoBase64)));

            var modal = new ContentPage
            {
                BackgroundColor = Color.FromArgb("#121212"),
                Title = $"Auditar INE - {tecnico.Nombre}"
            };

            var btnAprobar = new Button
            {
                Text = "✔️ Aprobar y Otorgar Insignia Verificado",
                BackgroundColor = Color.FromArgb("#28A745"),
                TextColor = Colors.White,
                FontAttributes = FontAttributes.Bold,
                CornerRadius = 10
            };

            var btnRechazar = new Button
            {
                Text = "❌ Rechazar Documentación",
                BackgroundColor = Color.FromArgb("#DC3545"),
                TextColor = Colors.White,
                FontAttributes = FontAttributes.Bold,
                CornerRadius = 10
            };

            var btnCerrar = new Button
            {
                Text = "Cerrar",
                BackgroundColor = Color.FromArgb("#343A40"),
                TextColor = Colors.White,
                CornerRadius = 10
            };

            btnAprobar.Clicked += async (_, _) =>
            {
                bool ok = await _dbService.DictaminarVerificacionTecnicoAsync(tecnico.Id, tecnico.Email, aprobar: true);
                if (ok)
                {
                    await DisplayAlertAsync("¡Aprobado!", $"{tecnico.Nombre} ahora tiene la insignia ✔️ Técnico Verificado.", "OK");
                    await Navigation.PopModalAsync();
                    await CargarPendientesAsync();
                }
            };

            btnRechazar.Clicked += async (_, _) =>
            {
                string motivo = await DisplayPromptAsync("Motivo de Rechazo", "Indica por qué se rechaza (ej. foto borrosa o INE vencida):");
                if (string.IsNullOrWhiteSpace(motivo)) return;

                bool ok = await _dbService.DictaminarVerificacionTecnicoAsync(tecnico.Id, tecnico.Email, aprobar: false, motivoRechazo: motivo.Trim());
                if (ok)
                {
                    await DisplayAlertAsync("Rechazado", "Se notificó al técnico para que vuelva a subir sus documentos.", "OK");
                    await Navigation.PopModalAsync();
                    await CargarPendientesAsync();
                }
            };

            btnCerrar.Clicked += async (_, _) => await Navigation.PopModalAsync();

            modal.Content = new ScrollView
            {
                Content = new VerticalStackLayout
                {
                    Padding = 20,
                    Spacing = 12,
                    Children =
                    {
                        new Label { Text = $"Técnico: {tecnico.Nombre} ({tecnico.Email})", TextColor = Colors.White, FontAttributes = FontAttributes.Bold, FontSize = 16 },
                        new Label { Text = $"Especialidad: {tecnico.Especialidad} | Tel: {tecnico.Telefono}", TextColor = Color.FromArgb("#B0C4DE") },
                        new Label { Text = "Frente de INE:", TextColor = Color.FromArgb("#00D2FF"), FontAttributes = FontAttributes.Bold },
                        imgFrente,
                        new Label { Text = "Reverso de INE:", TextColor = Color.FromArgb("#00D2FF"), FontAttributes = FontAttributes.Bold },
                        imgReverso,
                        btnAprobar,
                        btnRechazar,
                        btnCerrar
                    }
                }
            };

            await Navigation.PushModalAsync(modal);
        }
    }
}