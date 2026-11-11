using System.Collections.ObjectModel;
using PHANTEL.Models;
using PHANTEL.Services;

namespace PHANTEL.Views
{
    public partial class ChatPage : ContentPage
    {
        private readonly SolicitudCita _cita;
        private readonly CloudDatabaseService _dbService = new();
        private readonly ObservableCollection<MensajeChatUi> _mensajes = new();
        private IDisposable? _subscripcionChat;

        public ChatPage(SolicitudCita cita)
        {
            InitializeComponent();
            _cita = cita;
            lblDetalleCita.Text = $"Cita #{_cita.Id} | {_cita.DescripcionFalla}";
            listMensajes.ItemsSource = _mensajes;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            ConectarChatEnVivo();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _subscripcionChat?.Dispose();
        }

        private void ConectarChatEnVivo()
        {
            _subscripcionChat = _dbService.EscucharChatEnVivo(_cita.Id).Subscribe(ev =>
            {
                if (ev.Object != null)
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        var msg = ev.Object;
                        msg.Id = ev.Key;

                        int usuarioActualId = LoginPage.UsuarioActual?.Id ?? 0;
                        bool esMio = (msg.EmisorId == usuarioActualId);

                        var msgUi = new MensajeChatUi
                        {
                            Id = msg.Id,
                            EmisorNombre = msg.EmisorNombre,
                            Texto = msg.Texto,
                            FechaHora = msg.FechaHora,
                            EsMio = esMio
                        };

                        if (!_mensajes.Any(m => m.Id == msgUi.Id))
                        {
                            _mensajes.Add(msgUi);
                            listMensajes.ScrollTo(_mensajes.Count - 1);
                        }
                    });
                }
            });
        }

        private async void OnEnviarMensajeClicked(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(entryMensaje.Text) || LoginPage.UsuarioActual == null)
                return;

            string texto = entryMensaje.Text.Trim();
            entryMensaje.Text = string.Empty;

            var nuevoMensaje = new MensajeChat
            {
                SolicitudId = _cita.Id,
                EmisorId = LoginPage.UsuarioActual.Id,
                EmisorNombre = LoginPage.UsuarioActual.Nombre,
                Texto = texto,
                FechaHora = DateTime.Now,
                EsTecnico = LoginPage.UsuarioActual.TipoPerfil == "Técnico"
            };

            await _dbService.EnviarMensajeChatAsync(nuevoMensaje);
        }
    }

    public class MensajeChatUi
    {
        public string Id { get; set; } = string.Empty;
        public string EmisorNombre { get; set; } = string.Empty;
        public string Texto { get; set; } = string.Empty;
        public DateTime FechaHora { get; set; }
        public bool EsMio { get; set; }
    }
}