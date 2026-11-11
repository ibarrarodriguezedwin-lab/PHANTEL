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
            // Muestra datos técnicos solo cuando seleccionen 'Técnico / Trabajador'
            bool esTecnico = pickerRol.SelectedIndex == 1;
            layoutSeccionTecnico.IsVisible = esTecnico;
        }

        private async void OnRegistrarClicked(object sender, EventArgs e)
        {
            string nombre = entryNombre.Text?.Trim() ?? "";
            string email = entryEmail.Text?.Trim() ?? "";
            string pass = entryPassword.Text?.Trim() ?? "";
            string telefono = entryTelefono.Text?.Trim() ?? "";
            string direccion = entryDireccion.Text?.Trim() ?? "";

            // Validaciones básicas de campos obligatorios
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

            lblError.IsVisible = false;
            string rolSeleccionado = pickerRol.SelectedIndex == 0 ? "Cliente" : "Técnico";

            var nuevoUsuario = new Usuario
            {
                Nombre = nombre,
                Email = email,
                Password = pass,
                Telefono = telefono,
                Direccion = direccion,
                TipoPerfil = rolSeleccionado
            };

            if (rolSeleccionado == "Técnico")
            {
                if (pickerEspecialidad.SelectedIndex == -1)
                {
                    MostrarError("Por favor selecciona tu especialidad/oficio.");
                    return;
                }

                nuevoUsuario.Especialidad = pickerEspecialidad.SelectedItem?.ToString() ?? "Técnico General";
                nuevoUsuario.Experiencia = entryExperiencia.Text?.Trim() ?? "1 año";
                decimal.TryParse(entryTarifa.Text, out decimal tarifa);
                nuevoUsuario.TarifaPorHora = tarifa > 0 ? tarifa : 150m;
            }

            // Intenta registrar en Firebase (CloudDatabaseService verifica si el correo ya existe)
            bool exito = await _dbService.RegistrarUsuarioAsync(nuevoUsuario);

            if (!exito)
            {
                MostrarError("Este correo ya se encuentra registrado. Intenta con otro o inicia sesión.");
                return;
            }

            // Asignar sesión activa
            LoginPage.UsuarioActual = nuevoUsuario;

            await DisplayAlert("Éxito", $"¡Bienvenido {nombre}! Tu cuenta de {rolSeleccionado} ha sido guardada en la nube.", "Aceptar");

            // Redirección directa según rol
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