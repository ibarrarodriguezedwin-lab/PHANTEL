using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PHANTEL.Models
{
    public class CategoriaIcono : INotifyPropertyChanged
    {
        public string Nombre { get; set; } = string.Empty;
        public string NombreCorto { get; set; } = string.Empty;
        public string Emoji { get; set; } = string.Empty;
        public string Imagen { get; set; } = string.Empty;

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
                OnPropertyChanged(nameof(ColorTexto));
                OnPropertyChanged(nameof(ColorFondo));
            }
        }

        public Color ColorTexto => EstaSeleccionada ? Color.FromArgb("#121212") : Color.FromArgb("#B0B0B5");
        public Color ColorFondo => EstaSeleccionada ? Color.FromArgb("#F2F2F7") : Color.FromArgb("#1C1C1E");

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? nombre = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nombre));
    }
}