using System.Globalization;
using Microsoft.Maui.Controls.Shapes;
using PHANTEL.Helpers;
using PHANTEL.Models;
using PHANTEL.Services;

namespace PHANTEL.Views
{
    public partial class ConfiguracionPage : ContentPage
    {
        // Paleta de la pantalla (la misma gama oscura que ya usa la app, con un solo acento)
        private static readonly Color CSuperficie = Color.FromArgb("#1C1C1E");
        private static readonly Color CLinea = Color.FromArgb("#2C2C2E");
        private static readonly Color CTexto = Color.FromArgb("#F2F2F7");
        private static readonly Color CSecundario = Color.FromArgb("#8E8E93");
        private static readonly Color CAcento = Color.FromArgb("#007ACC");
        private static readonly Color CPeligro = Color.FromArgb("#E5534B");
        private static readonly Color CVerde = Color.FromArgb("#34C759");
        private static readonly Color CAmbar = Color.FromArgb("#E0A030");

        private readonly ConfiguracionService _svc = new();
        private readonly CloudDatabaseService _cloud = new();

        private bool _ignorarSwitch;
        private int _pendientesKyc;
        private int _disputasActivas;

        public ConfiguracionPage()
        {
            InitializeComponent();
        }

        private static string T(string clave) => Textos.T(clave);

        // ------------------------------------------------------------------
        // CICLO DE VIDA
        // ------------------------------------------------------------------

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            var actual = LoginPage.UsuarioActual;
            if (actual == null)
            {
                IrALogin();
                return;
            }

            // Traer datos frescos (verificación, saldos, disponibilidad)
            var fresco = await _cloud.ObtenerUsuarioPorEmailAsync(actual.Email);
            if (fresco != null)
            {
                LoginPage.UsuarioActual = fresco;
                actual = fresco;
            }

            if (!string.IsNullOrWhiteSpace(actual.Idioma))
                Textos.IdiomaActual = actual.Idioma;

            if (actual.TipoPerfil == "Admin")
            {
                try
                {
                    _pendientesKyc = (await _cloud.ObtenerTecnicosPendientesVerificacionAsync()).Count;
                    _disputasActivas = (await _cloud.ObtenerDisputasActivasAsync()).Count();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[CONFIG ADMIN CONTADORES]: {ex.Message}");
                }
            }

            Construir();
        }

        private async Task ReconstruirAsync()
        {
            double y = scroll.ScrollY;
            Construir();
            await Task.Delay(60);
            await scroll.ScrollToAsync(0, y, false);
        }

        private void Construir()
        {
            var u = LoginPage.UsuarioActual;
            if (u == null) return;

            Title = T("cfg.titulo");
            contenedor.Children.Clear();
            contenedor.Children.Add(CrearEncabezado(u));

            switch (u.TipoPerfil)
            {
                case "Admin":
                    ConstruirAdmin(u);
                    break;
                case "Técnico":
                    ConstruirTecnico(u);
                    break;
                default:
                    ConstruirCliente(u);
                    break;
            }
        }

        // ------------------------------------------------------------------
        // CONFIGURACIÓN POR ROL
        // ------------------------------------------------------------------

        private void ConstruirCliente(Usuario u)
        {
            Seccion(T("cfg.cuenta"),
                Fila(T("cfg.correo"), u.Email, null),
                Fila(T("cfg.telefono"), Vacio(u.Telefono), () => EditarTelefonoAsync(u)),
                Fila(T("cfg.idioma"), NombreIdioma(), () => ElegirIdiomaAsync(u)),
                Fila(T("cfg.foto"), null, () => CambiarFotoAsync(u)),
                Fila(T("cfg.password"), null, () => CambiarPasswordAsync(u)));

            Seccion(T("cfg.ubicacion"),
                Fila(T("cfg.cp"), Vacio(u.CodigoPostal), () => EditarCpAsync(u)),
                Fila(T("cfg.domicilio"), Vacio(u.Direccion), () => EditarDomicilioAsync(u)));

            Seccion(T("cfg.actividad"),
                Fila(T("cfg.historial"), null, () => Navigation.PushAsync(new MisSolicitudesPage())));

            AgregarSoporte();
            AgregarSesion(u, permitirEliminar: true);
        }

        private void ConstruirTecnico(Usuario u)
        {
            contenedor.Children.Add(CrearBloqueDisponibilidad(u));

            var filasPerfil = new List<View>
            {
                Fila(T("cfg.especialidad"), Vacio(u.Especialidad), null),
                Fila(T("cfg.tarifa"), $"${u.TarifaPorHora:0.##}", () => EditarTarifaAsync(u)),
                Fila(T("cfg.experiencia"), Vacio(u.Experiencia), () => EditarExperienciaAsync(u)),
                Fila(T("cfg.modalidad"), TextoModalidad(u), () => EditarModalidadAsync(u))
            };

            if (u.AtiendeLocal)
            {
                filasPerfil.Add(Fila(T("cfg.dirlocal"), Vacio(u.DireccionLocal), () => EditarDireccionLocalAsync(u)));
                filasPerfil.Add(Fila(T("cfg.horario"), Vacio(u.HorarioLocal), () => EditarHorarioLocalAsync(u)));
            }

            filasPerfil.Add(Fila(T("cfg.cp.zona"), Vacio(u.CodigoPostal), () => EditarCpAsync(u)));
            filasPerfil.Add(Fila(T("cfg.verificacion"), TextoVerificacion(u), () => Navigation.PushAsync(new VerificacionTecnicoPage())));

            Seccion(T("cfg.perfilpro"), filasPerfil.ToArray());

            Seccion(T("cfg.pagos"),
                Fila(T("cfg.saldo.custodia"), $"${u.SaldoEnCustodia:F2}", null),
                Fila(T("cfg.saldo.disponible"), $"${u.SaldoLiberado:F2}", null),
                Fila(T("cfg.retirar"), null, () => RetirarAsync(u)),
                Fila(T("cfg.comisiones"), null, () => DisplayAlertAsync(T("cfg.comisiones"), T("cfg.comisiones.msg"), "OK")));

            Seccion(T("cfg.cuenta"),
                Fila(T("cfg.correo"), u.Email, null),
                Fila(T("cfg.telefono"), Vacio(u.Telefono), () => EditarTelefonoAsync(u)),
                Fila(T("cfg.idioma"), NombreIdioma(), () => ElegirIdiomaAsync(u)),
                Fila(T("cfg.foto"), null, () => CambiarFotoAsync(u)),
                Fila(T("cfg.password"), null, () => CambiarPasswordAsync(u)));

            Seccion(T("cfg.actividad"),
                Fila(T("cfg.resenas"), u.TotalCalificaciones > 0 ? $"{u.CalificacionPromedio:F1} ★" : null,
                    () => Navigation.PushAsync(new PerfilTecnicoPage(u, soloLectura: true))),
                Fila(T("cfg.historial"), null, () => VerHistorialTecnicoAsync(u)));

            AgregarSoporte();
            AgregarSesion(u, permitirEliminar: true);
        }

        private void ConstruirAdmin(Usuario u)
        {
            Seccion(T("cfg.pendientes"),
                Fila(T("cfg.kyc"), _pendientesKyc.ToString(), () => Navigation.PushAsync(new AdminVerificacionesPage())),
                Fila(T("cfg.disputas"), _disputasActivas.ToString(), () => Navigation.PopAsync()));

            Seccion(T("cfg.cuenta"),
                Fila(T("cfg.correo"), u.Email, null),
                Fila(T("cfg.rol"), T("rol.admin"), null),
                Fila(T("cfg.telefono"), Vacio(u.Telefono), () => EditarTelefonoAsync(u)),
                Fila(T("cfg.idioma"), NombreIdioma(), () => ElegirIdiomaAsync(u)),
                Fila(T("cfg.password"), null, () => CambiarPasswordAsync(u)));

            AgregarSesion(u, permitirEliminar: false);
        }

        private void AgregarSoporte()
        {
            Seccion(T("cfg.soporte"),
                Fila(T("cfg.ayuda"), null, () => DisplayAlertAsync(T("cfg.ayuda"), T("cfg.ayuda.msg"), "OK")),
                Fila(T("cfg.acerca"), string.Format(T("cfg.version"), AppInfo.Current.VersionString), null));
        }

        private void AgregarSesion(Usuario u, bool permitirEliminar)
        {
            var filas = new List<View>
            {
                Fila(T("cfg.cerrar"), null, ConfirmarCerrarSesionAsync, chevron: false)
            };

            if (permitirEliminar)
                filas.Add(Fila(T("cfg.eliminar"), null, () => EliminarCuentaAsync(u), peligro: true, chevron: false));

            Seccion(T("cfg.sesion"), filas.ToArray());
        }

        // ------------------------------------------------------------------
        // PIEZAS DE INTERFAZ
        // ------------------------------------------------------------------

        private View CrearEncabezado(Usuario u)
        {
            var perfil = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Star)
                },
                ColumnSpacing = 16,
                Padding = new Thickness(20, 24, 20, 16)
            };

            perfil.Add(CrearAvatar(u, 56), 0, 0);
            perfil.Add(new VerticalStackLayout
            {
                VerticalOptions = LayoutOptions.Center,
                Spacing = 2,
                Children =
                {
                    new Label
                    {
                        Text = u.Nombre,
                        FontSize = 18,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = CTexto,
                        MaxLines = 1,
                        LineBreakMode = LineBreakMode.TailTruncation
                    },
                    new Label
                    {
                        Text = TextoRol(u),
                        FontSize = 13,
                        TextColor = CSecundario
                    }
                }
            }, 1, 0);

            var contenedorPerfil = new VerticalStackLayout { Spacing = 0 };
            contenedorPerfil.Children.Add(perfil);

            // El administrador no crea perfiles desde aquí
            if (u.TipoPerfil != "Admin")
            {
                contenedorPerfil.Children.Add(new BoxView
                {
                    HeightRequest = 1,
                    Color = CLinea,
                    Margin = new Thickness(20, 0)
                });
                contenedorPerfil.Children.Add(CrearFilaNuevoPerfil());
            }

            return contenedorPerfil;
        }

        private View CrearFilaNuevoPerfil()
        {
            var circulo = new Border
            {
                WidthRequest = 56,
                HeightRequest = 56,
                StrokeThickness = 0,
                BackgroundColor = Color.FromArgb("#3A3A3C"),
                StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(28) },
                Content = new Label
                {
                    Text = "+",
                    FontSize = 28,
                    TextColor = CTexto,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                }
            };

            var fila = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Star)
                },
                ColumnSpacing = 16,
                Padding = new Thickness(20, 16)
            };

            fila.Add(circulo, 0, 0);
            fila.Add(new Label
            {
                Text = T("cfg.crearperfil"),
                FontSize = 17,
                FontAttributes = FontAttributes.Bold,
                TextColor = CTexto,
                VerticalOptions = LayoutOptions.Center
            }, 1, 0);

            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) =>
            {
                try
                {
                    await CrearPerfilAsync();
                }
                catch (Exception ex)
                {
                    await DisplayAlertAsync(T("cfg.atencion"), ex.Message, "OK");
                }
            };
            fila.GestureRecognizers.Add(tap);

            return fila;
        }

        private View CrearAvatar(Usuario u, double tam = 88)
        {
            var borde = new Border
            {
                WidthRequest = tam,
                HeightRequest = tam,
                StrokeThickness = 0,
                BackgroundColor = CLinea,
                HorizontalOptions = LayoutOptions.Start,
                VerticalOptions = LayoutOptions.Center,
                StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(tam / 2) }
            };

            var foto = FotoDe(u);
            if (foto != null)
            {
                borde.Content = new Image
                {
                    Source = foto,
                    Aspect = Aspect.AspectFill,
                    WidthRequest = tam,
                    HeightRequest = tam
                };
            }
            else
            {
                borde.Content = new Label
                {
                    Text = Iniciales(u.Nombre),
                    FontSize = tam * 0.34,
                    TextColor = CTexto,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                };
            }

            if (u.TipoPerfil != "Admin")
            {
                var tap = new TapGestureRecognizer();
                tap.Tapped += async (_, _) => await CambiarFotoAsync(u);
                borde.GestureRecognizers.Add(tap);
            }

            return borde;
        }

        private View CrearBloqueDisponibilidad(Usuario u)
        {
            var titulo = new Label
            {
                Text = T("cfg.disp.titulo"),
                FontSize = 17,
                FontAttributes = FontAttributes.Bold,
                TextColor = CTexto
            };
            var estado = new Label { FontSize = 13 };

            void Pintar(bool disponible)
            {
                estado.Text = disponible ? T("cfg.disp.on") : T("cfg.disp.off");
                estado.TextColor = disponible ? CVerde : CAmbar;
            }

            Pintar(u.Disponible);

            var interruptor = new Switch
            {
                IsToggled = u.Disponible,
                OnColor = CVerde,
                VerticalOptions = LayoutOptions.Center
            };

            interruptor.Toggled += async (_, e) =>
            {
                if (_ignorarSwitch) return;

                bool nuevo = e.Value;
                interruptor.IsEnabled = false;

                bool ok = await _svc.ActualizarUsuarioAsync(u.Email, x => x.Disponible = nuevo);
                if (ok)
                {
                    u.Disponible = nuevo;
                    Pintar(nuevo);
                }
                else
                {
                    _ignorarSwitch = true;
                    interruptor.IsToggled = !nuevo;
                    _ignorarSwitch = false;
                    await DisplayAlertAsync(T("cfg.atencion"), T("cfg.error"), "OK");
                }

                interruptor.IsEnabled = true;
            };

            var fila = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto)
                },
                ColumnSpacing = 16,
                Padding = new Thickness(20, 18),
                BackgroundColor = CSuperficie
            };
            fila.Add(new VerticalStackLayout { Spacing = 4, Children = { titulo, estado } }, 0, 0);
            fila.Add(interruptor, 1, 0);

            return new VerticalStackLayout
            {
                Margin = new Thickness(0, 20, 0, 0),
                Spacing = 0,
                Children =
                {
                    Linea(0),
                    fila,
                    Linea(0),
                    new Label
                    {
                        Text = T("cfg.disp.nota"),
                        FontSize = 12,
                        TextColor = CSecundario,
                        Margin = new Thickness(20, 8, 20, 0)
                    }
                }
            };
        }

        private void Seccion(string titulo, params View[] filas)
        {
            contenedor.Children.Add(new Label
            {
                Text = titulo,
                FontSize = 13,
                TextColor = CSecundario,
                Margin = new Thickness(20, 26, 20, 8)
            });

            var grupo = new VerticalStackLayout { Spacing = 0, BackgroundColor = CSuperficie };
            grupo.Children.Add(Linea(0));
            for (int i = 0; i < filas.Length; i++)
            {
                if (i > 0) grupo.Children.Add(Linea(20));
                grupo.Children.Add(filas[i]);
            }
            grupo.Children.Add(Linea(0));

            contenedor.Children.Add(grupo);
        }

        private static BoxView Linea(double margenIzquierdo) => new()
        {
            HeightRequest = 1,
            Color = CLinea,
            Margin = new Thickness(margenIzquierdo, 0, 0, 0)
        };

        private View Fila(string titulo, string? valor, Func<Task>? accion, bool peligro = false, bool chevron = true)
        {
            string flecha = accion != null && chevron
                ? (string.IsNullOrEmpty(valor) ? "›" : "   ›")
                : "";

            var fila = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Star)
                },
                ColumnSpacing = 16,
                Padding = new Thickness(20, 15),
                MinimumHeightRequest = 52,
                BackgroundColor = CSuperficie
            };

            fila.Add(new Label
            {
                Text = titulo,
                FontSize = 16,
                TextColor = peligro ? CPeligro : CTexto,
                VerticalOptions = LayoutOptions.Center
            }, 0, 0);

            fila.Add(new Label
            {
                Text = (valor ?? "") + flecha,
                FontSize = 15,
                TextColor = CSecundario,
                HorizontalTextAlignment = TextAlignment.End,
                VerticalOptions = LayoutOptions.Center,
                LineBreakMode = LineBreakMode.TailTruncation,
                MaxLines = 1
            }, 1, 0);

            if (accion != null)
            {
                var tap = new TapGestureRecognizer();
                tap.Tapped += async (_, _) =>
                {
                    try
                    {
                        await accion();
                    }
                    catch (Exception ex)
                    {
                        await DisplayAlertAsync(T("cfg.atencion"), ex.Message, "OK");
                    }
                };
                fila.GestureRecognizers.Add(tap);
            }

            return fila;
        }

        // ------------------------------------------------------------------
        // ACCIONES
        // ------------------------------------------------------------------

        private async Task EditarCampoAsync(
            Usuario u, string claveTitulo, string claveMensaje, string valorActual,
            Func<string, string?> validar, Action<Usuario, string> aplicar,
            int maxLength = -1, Keyboard? teclado = null)
        {
            string? nuevo = await DisplayPromptAsync(
                T(claveTitulo), T(claveMensaje),
                accept: T("cfg.guardar"), cancel: T("cfg.cancelar"),
                initialValue: valorActual, maxLength: maxLength,
                keyboard: teclado ?? Keyboard.Default);

            if (nuevo == null) return;
            nuevo = nuevo.Trim();
            if (nuevo == valorActual) return;

            string? error = validar(nuevo);
            if (error != null)
            {
                await DisplayAlertAsync(T("cfg.atencion"), error, "OK");
                return;
            }

            string valor = nuevo;
            bool ok = await _svc.ActualizarUsuarioAsync(u.Email, x => aplicar(x, valor));
            if (!ok)
            {
                await DisplayAlertAsync(T("cfg.atencion"), T("cfg.error"), "OK");
                return;
            }

            aplicar(u, valor);
            await ReconstruirAsync();
        }

        private Task EditarTelefonoAsync(Usuario u) =>
            EditarCampoAsync(u, "cfg.telefono", "cfg.telefono.msg", u.Telefono,
                v => v.Length == 10 && v.All(char.IsDigit) ? null : T("cfg.telefono.invalido"),
                (x, v) => x.Telefono = v, 10, Keyboard.Telephone);

        private Task EditarDomicilioAsync(Usuario u) =>
            EditarCampoAsync(u, "cfg.domicilio", "cfg.domicilio.msg", u.Direccion,
                v => v.Length >= 8 ? null : T("cfg.domicilio.invalido"),
                (x, v) => x.Direccion = v);

        private Task EditarExperienciaAsync(Usuario u) =>
            EditarCampoAsync(u, "cfg.experiencia", "cfg.experiencia.msg", u.Experiencia,
                v => null,
                (x, v) => x.Experiencia = v, 300);

        private Task EditarTarifaAsync(Usuario u) =>
            EditarCampoAsync(u, "cfg.tarifa", "cfg.tarifa.msg",
                u.TarifaPorHora.ToString("0.##", CultureInfo.InvariantCulture),
                v => decimal.TryParse(v.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var d)
                     && d >= 1 && d <= 5000 ? null : T("cfg.tarifa.invalido"),
                (x, v) => x.TarifaPorHora = decimal.Parse(v.Replace(',', '.'), CultureInfo.InvariantCulture),
                8, Keyboard.Numeric);

        private async Task EditarModalidadAsync(Usuario u)
        {
            string domicilio = T("mod.domicilio");
            string local = T("mod.local");
            string ambos = T("mod.ambos");

            string? opcion = await DisplayActionSheetAsync(
                T("cfg.modalidad"), T("cfg.cancelar"), null, domicilio, local, ambos);

            string? elegida = opcion == domicilio ? "Domicilio"
                            : opcion == local ? "Local"
                            : opcion == ambos ? "Ambos"
                            : null;

            if (elegida == null || elegida == u.Modalidad) return;

            string modalidad = elegida;
            string direccionLocal = u.DireccionLocal;

            // Para atender en local hace falta su dirección
            if (modalidad != "Domicilio" && string.IsNullOrWhiteSpace(direccionLocal))
            {
                string? dir = await DisplayPromptAsync(
                    T("cfg.dirlocal"), T("cfg.dirlocal.msg"),
                    accept: T("cfg.guardar"), cancel: T("cfg.cancelar"), maxLength: 150);

                if (string.IsNullOrWhiteSpace(dir) || dir.Trim().Length < 8)
                {
                    await DisplayAlertAsync(T("cfg.atencion"), T("cfg.dirlocal.invalido"), "OK");
                    return;
                }

                direccionLocal = dir.Trim();
            }

            bool ok = await _svc.ActualizarUsuarioAsync(u.Email, x =>
            {
                x.Modalidad = modalidad;
                x.DireccionLocal = direccionLocal;
            });

            if (!ok)
            {
                await DisplayAlertAsync(T("cfg.atencion"), T("cfg.error"), "OK");
                return;
            }

            u.Modalidad = modalidad;
            u.DireccionLocal = direccionLocal;
            await ReconstruirAsync();
        }

        private Task EditarDireccionLocalAsync(Usuario u) =>
            EditarCampoAsync(u, "cfg.dirlocal", "cfg.dirlocal.msg", u.DireccionLocal,
                v => v.Length >= 8 ? null : T("cfg.dirlocal.invalido"),
                (x, v) => x.DireccionLocal = v, 150);

        private Task EditarHorarioLocalAsync(Usuario u) =>
            EditarCampoAsync(u, "cfg.horario", "cfg.horario.msg", u.HorarioLocal,
                v => null,
                (x, v) => x.HorarioLocal = v, 100);

        private async Task EditarCpAsync(Usuario u)
        {
            string? cp = await DisplayPromptAsync(
                T("cfg.cp"), T("cfg.cp.msg"),
                accept: T("cfg.guardar"), cancel: T("cfg.cancelar"),
                initialValue: u.CodigoPostal, maxLength: 5, keyboard: Keyboard.Numeric);

            if (string.IsNullOrWhiteSpace(cp)) return;
            cp = cp.Trim();

            if (cp.Length != 5 || !cp.All(char.IsDigit))
            {
                await DisplayAlertAsync(T("cfg.atencion"), T("cfg.cp.invalido"), "OK");
                return;
            }

            var (lat, lon) = await DatabaseService.ObtenerCoordenadasPorDireccionOCpAsync(
                $"{cp}, Ciudad de México", cp);

            bool ok = await _cloud.ActualizarCodigoPostalUsuarioAsync(u.Id, u.Email, cp, lat, lon);
            if (!ok)
            {
                await DisplayAlertAsync(T("cfg.atencion"), T("cfg.error"), "OK");
                return;
            }

            u.CodigoPostal = cp;
            u.Latitud = lat;
            u.Longitud = lon;
            await ReconstruirAsync();
        }

        private async Task ElegirIdiomaAsync(Usuario u)
        {
            string? opcion = await DisplayActionSheetAsync(
                T("cfg.idioma"), T("cfg.cancelar"), null,
                Idiomas.Select(i => i.Nombre).ToArray());

            var elegido = Idiomas.FirstOrDefault(i => i.Nombre == opcion);
            if (elegido.Codigo == null || elegido.Codigo == Textos.IdiomaActual) return;

            string codigo = elegido.Codigo;
            Textos.IdiomaActual = codigo;
            u.Idioma = codigo;
            await _svc.ActualizarUsuarioAsync(u.Email, x => x.Idioma = codigo);
            await ReconstruirAsync();
        }

        private async Task CrearPerfilAsync()
        {
            bool confirmar = await DisplayAlertAsync(
                T("cfg.crearperfil"), T("cfg.crearperfil.conf"), T("cfg.si"), T("cfg.cancelar"));
            if (!confirmar) return;

            NotificacionRealtimeService.DetenerEscucha();
            Preferences.Default.Remove("SesionEmail");
            LoginPage.UsuarioActual = null;

            var navegacion = new NavigationPage(new LoginPage());
            Application.Current!.Windows[0].Page = navegacion;
            await navegacion.PushAsync(new RegistroPage());
        }

        private async Task CambiarFotoAsync(Usuario u)
        {
            try
            {
                var fotos = await MediaPicker.Default.PickPhotosAsync(new MediaPickerOptions
                {
                    Title = T("cfg.foto")
                });

                var foto = fotos?.FirstOrDefault();
                if (foto == null) return;

                string base64 = await ImageHelper.ComprimirImagenABase64Async(foto, maxAncho: 400f, calidad: 0.65f);
                if (string.IsNullOrEmpty(base64))
                {
                    await DisplayAlertAsync(T("cfg.atencion"), T("cfg.foto.error"), "OK");
                    return;
                }

                bool ok = await _cloud.ActualizarFotoPerfilAsync(u.Id, base64);
                if (!ok)
                {
                    await DisplayAlertAsync(T("cfg.atencion"), T("cfg.error"), "OK");
                    return;
                }

                u.FotoPerfil = base64;
                await ReconstruirAsync();
            }
            catch (Exception ex)
            {
                await DisplayAlertAsync(T("cfg.atencion"), ex.Message, "OK");
            }
        }

        private async Task CambiarPasswordAsync(Usuario u)
        {
            var valores = await PedirClavesAsync(
                T("cfg.password"), null,
                new[] { T("cfg.pass.actual"), T("cfg.pass.nueva"), T("cfg.pass.repite") },
                v => string.IsNullOrEmpty(v[0]) ? T("cfg.pass.vacia")
                   : v[1].Length < 6 ? T("cfg.pass.corta")
                   : v[1] != v[2] ? T("cfg.pass.noigual")
                   : null);

            if (valores == null) return;

            bool ok = await _svc.CambiarPasswordAsync(u.Email, valores[0], valores[1]);
            await DisplayAlertAsync(T("cfg.password"), ok ? T("cfg.pass.ok") : T("cfg.pass.mal"), "OK");
        }

        private async Task RetirarAsync(Usuario u)
        {
            if (u.SaldoLiberado <= 0)
            {
                await DisplayAlertAsync(T("cfg.retirar"), T("cfg.retirar.sin"), "OK");
                return;
            }

            string? clabe = await DisplayPromptAsync(
                T("cfg.retirar"), T("cfg.clabe.msg"),
                accept: T("cfg.guardar"), cancel: T("cfg.cancelar"),
                initialValue: u.ClabeInterbancaria, maxLength: 18, keyboard: Keyboard.Numeric);

            if (string.IsNullOrWhiteSpace(clabe)) return;
            clabe = clabe.Trim();

            if (clabe.Length != 18 || !clabe.All(char.IsDigit))
            {
                await DisplayAlertAsync(T("cfg.atencion"), T("cfg.clabe.invalida"), "OK");
                return;
            }

            bool confirmar = await DisplayAlertAsync(
                T("cfg.retirar"),
                string.Format(T("cfg.retirar.conf"), u.SaldoLiberado),
                T("cfg.si"), T("cfg.cancelar"));
            if (!confirmar) return;

            bool ok = await _cloud.SolicitarRetiroTecnicoAsync(u.Id, clabe, u.Email);
            if (!ok)
            {
                await DisplayAlertAsync(T("cfg.atencion"), T("cfg.error"), "OK");
                return;
            }

            u.SaldoLiberado = 0m;
            u.ClabeInterbancaria = clabe;
            await DisplayAlertAsync(T("cfg.retirar"), T("cfg.retirar.ok"), "OK");
            await ReconstruirAsync();
        }

        private async Task VerHistorialTecnicoAsync(Usuario u)
        {
            var citas = await _cloud.ObtenerSolicitudesPorUsuarioAsync(u.Id, esTecnico: true);

            // Se filtra por técnico por seguridad: el servicio devuelve todas las citas
            // cuando el técnico aún no tiene ninguna propia.
            var historial = citas
                .Where(c => (c.TecnicoId == u.Id ||
                             string.Equals(c.TecnicoEmail, u.Email, StringComparison.OrdinalIgnoreCase))
                            && (c.Estado == "Finalizada" || c.Estado == "Rechazada"))
                .ToList();

            if (historial.Count == 0)
            {
                await DisplayAlertAsync(T("cfg.historial"), T("cfg.historial.vacio"), "OK");
                return;
            }

            string resumen = string.Join("\n\n", historial.Select(c =>
                $"{T("cfg.cita")} #{c.Id} ({c.Estado})\n{T("cfg.falla")}: {c.DescripcionFalla}\n{T("cfg.traslado")}: ${c.TarifaTraslado:F2}"));

            await DisplayAlertAsync(T("cfg.historial"), resumen, T("cfg.cerrarventana"));
        }

        private async Task ConfirmarCerrarSesionAsync()
        {
            bool confirmar = await DisplayAlertAsync(T("cfg.cerrar"), T("cfg.cerrar.conf"), T("cfg.si"), T("cfg.cancelar"));
            if (confirmar) CerrarSesion();
        }

        private async Task EliminarCuentaAsync(Usuario u)
        {
            if (u.TipoPerfil == "Técnico" && (u.SaldoEnCustodia > 0 || u.SaldoLiberado > 0))
            {
                await DisplayAlertAsync(T("cfg.eliminar"), T("cfg.eliminar.saldo"), "OK");
                return;
            }

            var valores = await PedirClavesAsync(
                T("cfg.eliminar"), T("cfg.eliminar.msg"),
                new[] { T("cfg.pass.actual") },
                v => string.IsNullOrEmpty(v[0]) ? T("cfg.pass.vacia") : null);

            if (valores == null) return;

            bool confirmar = await DisplayAlertAsync(T("cfg.eliminar"), T("cfg.eliminar.msg"), T("cfg.eliminar"), T("cfg.cancelar"));
            if (!confirmar) return;

            bool ok = await _svc.EliminarCuentaAsync(u.Email, valores[0]);
            if (!ok)
            {
                await DisplayAlertAsync(T("cfg.atencion"), T("cfg.pass.mal"), "OK");
                return;
            }

            CerrarSesion();
        }

        private static void CerrarSesion()
        {
            NotificacionRealtimeService.DetenerEscucha();
            Preferences.Default.Remove("SesionEmail");
            LoginPage.UsuarioActual = null;
            Application.Current!.Windows[0].Page = new NavigationPage(new LoginPage());
        }

        private static void IrALogin() => CerrarSesion();

        // ------------------------------------------------------------------
        // VENTANA PARA CONTRASEÑAS (los campos ocultan lo que se escribe)
        // ------------------------------------------------------------------

        private async Task<string[]?> PedirClavesAsync(
            string titulo, string? mensaje, string[] etiquetas, Func<string[], string?> validar)
        {
            var resultado = new TaskCompletionSource<string[]?>();

            var campos = etiquetas.Select(e => new Entry
            {
                Placeholder = e,
                IsPassword = true,
                TextColor = CTexto,
                PlaceholderColor = CSecundario,
                BackgroundColor = CSuperficie,
                HeightRequest = 48
            }).ToList();

            var lblError = new Label { TextColor = CPeligro, FontSize = 13, IsVisible = false };

            var btnGuardar = new Button
            {
                Text = T("cfg.guardar"),
                BackgroundColor = CAcento,
                TextColor = Colors.White,
                CornerRadius = 8,
                HeightRequest = 48
            };
            var btnCancelar = new Button
            {
                Text = T("cfg.cancelar"),
                BackgroundColor = Colors.Transparent,
                TextColor = CSecundario
            };

            var pila = new VerticalStackLayout { Padding = 24, Spacing = 14 };
            if (!string.IsNullOrWhiteSpace(mensaje))
                pila.Children.Add(new Label { Text = mensaje, TextColor = CSecundario, FontSize = 14 });
            foreach (var c in campos) pila.Children.Add(c);
            pila.Children.Add(lblError);
            pila.Children.Add(btnGuardar);
            pila.Children.Add(btnCancelar);

            var ventana = new ContentPage
            {
                Title = titulo,
                BackgroundColor = Color.FromArgb("#121212"),
                Content = new ScrollView { Content = pila }
            };

            btnGuardar.Clicked += async (_, _) =>
            {
                var valores = campos.Select(c => (c.Text ?? "").Trim()).ToArray();
                string? error = validar(valores);
                if (error != null)
                {
                    lblError.Text = error;
                    lblError.IsVisible = true;
                    return;
                }

                resultado.TrySetResult(valores);
                await Navigation.PopModalAsync();
            };

            btnCancelar.Clicked += async (_, _) =>
            {
                resultado.TrySetResult(null);
                await Navigation.PopModalAsync();
            };

            ventana.Disappearing += (_, _) => resultado.TrySetResult(null);

            await Navigation.PushModalAsync(ventana);
            return await resultado.Task;
        }

        // ------------------------------------------------------------------
        // UTILIDADES
        // ------------------------------------------------------------------

        private static string Vacio(string? valor) =>
            string.IsNullOrWhiteSpace(valor) ? "—" : valor;

        private static readonly (string Codigo, string Nombre)[] Idiomas =
        {
            ("es", "Español"),
            ("en", "English"),
            ("fr", "Français"),
            ("pt", "Português"),
            ("de", "Deutsch"),
            ("it", "Italiano"),
            ("zh", "中文")
        };

        private static string NombreIdioma() =>
            Idiomas.FirstOrDefault(i => i.Codigo == Textos.IdiomaActual).Nombre ?? "Español";

        private static string TextoModalidad(Usuario u) => u.Modalidad switch
        {
            "Local" => T("mod.local"),
            "Ambos" => T("mod.ambos"),
            _ => T("mod.domicilio")
        };

        private static string TextoVerificacion(Usuario u) => u.EstadoVerificacion switch
        {
            "Verificado" => T("ver.verificado"),
            "En Revisión" => T("ver.revision"),
            "Rechazado" => T("ver.rechazado"),
            _ => T("ver.no")
        };

        private static string TextoRol(Usuario u) => u.TipoPerfil switch
        {
            "Admin" => T("rol.admin"),
            "Técnico" => string.IsNullOrWhiteSpace(u.Especialidad)
                ? T("rol.tecnico")
                : $"{T("rol.tecnico")}, {u.Especialidad}",
            _ => T("rol.cliente")
        };

        private static string Iniciales(string nombre)
        {
            var partes = nombre.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (partes.Length == 0) return "?";
            if (partes.Length == 1) return partes[0][..1].ToUpper();
            return (partes[0][..1] + partes[1][..1]).ToUpper();
        }

        private static ImageSource? FotoDe(Usuario u)
        {
            if (string.IsNullOrWhiteSpace(u.FotoPerfil)) return null;

            try
            {
                string limpio = u.FotoPerfil.Contains(',') ? u.FotoPerfil.Split(',')[1] : u.FotoPerfil;
                byte[] bytes = Convert.FromBase64String(limpio.Trim());
                return ImageSource.FromStream(() => new MemoryStream(bytes));
            }
            catch
            {
                return null;
            }
        }
    }
}