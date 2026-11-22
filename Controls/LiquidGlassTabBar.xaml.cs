namespace PHANTEL.Controls
{
    public partial class LiquidGlassTabBar : ContentView
    {
        public static readonly BindableProperty TabActivoProperty =
            BindableProperty.Create(
                nameof(TabActivo),
                typeof(string),
                typeof(LiquidGlassTabBar),
                "Explorar",
                propertyChanged: OnTabActivoChanged);

        public string TabActivo
        {
            get => (string)GetValue(TabActivoProperty);
            set => SetValue(TabActivoProperty, value);
        }

        public LiquidGlassTabBar()
        {
            InitializeComponent();
            ActualizarEstilo();
        }

        private static void OnTabActivoChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is LiquidGlassTabBar bar)
                bar.ActualizarEstilo();
        }

        private void ActualizarEstilo()
        {
            bool esExplorar = TabActivo == "Explorar";

            // Estilo píldora iluminada Liquid Glass en la pestaña activa
            pillExplorar.BackgroundColor = esExplorar ? Color.FromArgb("#28FFFFFF") : Colors.Transparent;
            pillExplorar.Stroke = esExplorar ? Color.FromArgb("#35FFFFFF") : Colors.Transparent;
            lblExplorar.TextColor = esExplorar ? Colors.White : Color.FromArgb("#9CA3AF");
            lblExplorar.FontAttributes = esExplorar ? FontAttributes.Bold : FontAttributes.None;

            pillMisCitas.BackgroundColor = !esExplorar ? Color.FromArgb("#28FFFFFF") : Colors.Transparent;
            pillMisCitas.Stroke = !esExplorar ? Color.FromArgb("#35FFFFFF") : Colors.Transparent;
            lblMisCitas.TextColor = !esExplorar ? Colors.White : Color.FromArgb("#9CA3AF");
            lblMisCitas.FontAttributes = !esExplorar ? FontAttributes.Bold : FontAttributes.None;
        }

        private async void OnExplorarTapped(object? sender, TappedEventArgs e)
        {
            if (TabActivo == "Explorar") return;
            await Shell.Current.GoToAsync("//ExplorarRoute");
        }

        private async void OnMisCitasTapped(object? sender, TappedEventArgs e)
        {
            if (TabActivo == "MisCitas") return;
            await Shell.Current.GoToAsync("//MisCitasRoute");
        }
    }
}