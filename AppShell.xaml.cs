using PHANTEL.Views;

namespace PHANTEL
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            ConfigurarTitulo();
        }

        private void ConfigurarTitulo()
        {
            var usuario = LoginPage.UsuarioActual;
            if (usuario != null && usuario.TipoPerfil == "Técnico")
            {
                Title = $"PHANTEL - Técnico ({usuario.Nombre})";
            }
            else if (usuario != null)
            {
                Title = $"PHANTEL - Cliente ({usuario.Nombre})";
            }
        }
    }
}