using PHANTEL.Views;

namespace PHANTEL
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            // Iniciar primero con la pantalla de Login
            return new Window(new LoginPage());
        }
    }
}