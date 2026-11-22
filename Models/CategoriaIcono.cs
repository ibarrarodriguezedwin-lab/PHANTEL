using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PHANTEL.Models
{
    /// <summary>
    /// Representa un icono del carrusel de categorías (Explorar).
    /// "Nombre" debe coincidir EXACTAMENTE con el texto de la especialidad del Picker / base de datos.
    /// </summary>
    public class CategoriaIcono : INotifyPropertyChanged
    {
        public string Nombre { get; set; } = string.Empty;        // Ej: "Plomero" (igual que en el Picker)
        public string NombreCorto { get; set; } = string.Empty;   // Texto que se muestra bajo el icono
        public string Emoji { get; set; } = string.Empty;         // Icono provisional (funciona sin descargar nada)
        public string Imagen { get; set; } = string.Empty;        // Opcional: "plomero.png". Si se llena, reemplaza al emoji

        public bool TieneImagen => !string.IsNullOrEmpty(Imagen);
        public bool SoloEmoji => string.IsNullOrEmpty(Imagen);

        private bool _estaSeleccionada;
        public bool EstaSeleccionada
        {
            get => _estaSeleccionada;
            set
            {
                if (_estaSeleccionada == value) return;
                _estaSeleccionada = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? nombre = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nombre));
    }
}
