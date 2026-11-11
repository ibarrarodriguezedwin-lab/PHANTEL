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
            // Envolver LoginPage dentro de NavigationPage para habilitar la navegación a RegistroPage
            return new Window(new NavigationPage(new LoginPage()));
        }
    }
}