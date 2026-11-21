using PHANTEL.Models;
using PHANTEL.Services;

namespace PHANTEL.Views
{
    public partial class RegistroPage : ContentPage
    {
        private readonly CloudDatabaseService _dbService = new();

        public RegistroPage()
        {
            InitializeComponent();
        }

        private void OnRolChanged(object sender, EventArgs e)
        {
            bool esTecnico = pickerRol.SelectedIndex == 1;
            layoutSeccionTecnico.IsVisible = esTecnico;

            if (!esTecnico)
                chkAceptoTerminos.IsChecked = false;
        }

        private async void OnRegistrarClicked(object sender, EventArgs e)
        {
            string nombre = entryNombre.Text?.Trim() ?? "";
            string email = entryEmail.Text?.Trim() ?? "";
            string pass = entryPassword.Text?.Trim() ?? "";
            string telefono = entryTelefono.Text?.Trim() ?? "";
            string direccion = entryDireccion.Text?.Trim() ?? "";
            string codigoPostal = entryCodigoPostal.Text?.Trim() ?? "";

            if (pickerRol.SelectedIndex == -1)
            {
                MostrarError("Por favor selecciona si te registras como Cliente o Técnico.");
                return;
            }

            if (string.IsNullOrEmpty(nombre) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(pass))
            {
                MostrarError("Ingresa tu nombre, correo y contraseña.");
                return;
            }

            if (!email.Contains("@") || !email.Contains("."))
            {
                MostrarError("Por favor ingresa un correo electrónico válido.");
                return;
            }

            if (codigoPostal.Length != 5 || !codigoPostal.All(char.IsDigit))
            {
                MostrarError("Por favor ingresa un Código Postal válido de 5 dígitos.");
                return;
            }

            string rolSeleccionado = pickerRol.SelectedIndex == 0 ? "Cliente" : "Técnico";

            if (rolSeleccionado == "Técnico")
            {
                if (pickerEspecialidad.SelectedIndex == -1)
                {
                    MostrarError("Por favor selecciona tu especialidad/oficio.");
                    return;
                }

                if (!chkAceptoTerminos.IsChecked)
                {
                    MostrarError("Debes aceptar los Términos y Condiciones de comisión (3% sobre trabajo presupuestado y 5% por disputa) para crear tu cuenta de Técnico.");
                    await DisplayAlertAsync(
                        "Términos Obligatorios",
                        "Para registrarte como Técnico en PHANTEL debes aceptar la comisión del 3% sobre tus presupuestos de trabajo (tu traslado es libre de comisión) y la penalización del 5% en disputas procedentes.",
                        "Entendido");
                    return;
                }
            }

            lblError.IsVisible = false;

            // Obtener coordenadas base a partir del Código Postal de residencia
            var (latBase, lonBase) = await DatabaseService.ObtenerCoordenadasPorDireccionOCpAsync(
                $"{codigoPostal}, Ciudad de México",
                codigoPostal);

            var nuevoUsuario = new Usuario
            {
                Nombre = nombre,
                Email = email,
                Password = pass,
                Telefono = telefono,
                Direccion = direccion,
                CodigoPostal = codigoPostal,
                Latitud = latBase,
                Longitud = lonBase,
                TipoPerfil = rolSeleccionado,
                AceptoTerminosComision = rolSeleccionado == "Técnico" ? chkAceptoTerminos.IsChecked : true,
                FechaAceptacionTerminos = rolSeleccionado == "Técnico" ? DateTime.Now : null
            };

            if (rolSeleccionado == "Técnico")
            {
                nuevoUsuario.Especialidad = pickerEspecialidad.SelectedItem?.ToString() ?? "Técnico General";
                nuevoUsuario.Experiencia = entryExperiencia.Text?.Trim() ?? "1 año";
                decimal.TryParse(entryTarifa.Text, out decimal tarifa);
                nuevoUsuario.TarifaPorHora = tarifa > 0 ? tarifa : 150m;
            }

            bool exito = await _dbService.RegistrarUsuarioAsync(nuevoUsuario);

            if (!exito)
            {
                MostrarError("Este correo ya se encuentra registrado. Intenta con otro o inicia sesión.");
                return;
            }

            LoginPage.UsuarioActual = nuevoUsuario;

            await DisplayAlertAsync("Éxito", $"¡Bienvenido {nombre}! Tu cuenta de {rolSeleccionado} ha sido guardada en la nube.", "Aceptar");

            if (rolSeleccionado == "Técnico")
            {
                Application.Current!.Windows[0].Page = new NavigationPage(new PanelTecnicoPage());
            }
            else
            {
                Application.Current!.Windows[0].Page = new AppShell();
            }
        }

        private void MostrarError(string mensaje)
        {
            lblError.Text = mensaje;
            lblError.IsVisible = true;
        }

        private async void OnVolverLoginClicked(object sender, EventArgs e)
        {
            await Navigation.PopAsync();
        }
    }
}