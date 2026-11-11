using System.Globalization;

namespace PHANTEL.Helpers
{
    // --- 1. CONVERTIDOR DE BASE64 / RUTA LOCAL A IMAGEN ---
    public class Base64ToImageConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string rawString && !string.IsNullOrWhiteSpace(rawString))
            {
                try
                {
                    // Si viene como una ruta local válida de archivo
                    if (File.Exists(rawString))
                    {
                        return ImageSource.FromFile(rawString);
                    }

                    // Limpieza por si la cadena contiene prefijos Data URI
                    string cleanBase64 = rawString;
                    if (cleanBase64.Contains(","))
                    {
                        cleanBase64 = cleanBase64.Split(',')[1];
                    }

                    cleanBase64 = cleanBase64.Trim();

                    // Si es una cadena Base64 con longitud válida
                    if (cleanBase64.Length > 50)
                    {
                        byte[] imageBytes = System.Convert.FromBase64String(cleanBase64);
                        return ImageSource.FromStream(() => new MemoryStream(imageBytes));
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[CONVERTER BASE64 ERROR]: {ex.Message}");
                }
            }
            return null;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    // --- 2. ALINEACIÓN DEL GLOBO DE CHAT (DERECHA / IZQUIERDA) ---
    public class ChatAlignmentConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool esMio)
            {
                return esMio ? LayoutOptions.End : LayoutOptions.Start;
            }
            return LayoutOptions.Start;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    // --- 3. COLOR DEL GLOBO DE CHAT (AZUL PARA PROPIOS / GRIS PARA RECIBIDOS) ---
    public class ChatColorConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool esMio)
            {
                return esMio ? Color.FromArgb("#007ACC") : Color.FromArgb("#333337");
            }
            return Color.FromArgb("#333337");
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    // --- 4. CONVERTIDOR DE AVATAR CON IMAGEN POR DEFECTO ---
    public class AvatarConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string base64 && !string.IsNullOrWhiteSpace(base64))
            {
                try
                {
                    string cleanBase64 = base64.Contains(",") ? base64.Split(',')[1].Trim() : base64.Trim();
                    if (cleanBase64.Length > 50)
                    {
                        byte[] imageBytes = System.Convert.FromBase64String(cleanBase64);
                        return ImageSource.FromStream(() => new MemoryStream(imageBytes));
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[AVATAR CONVERTER ERROR]: {ex.Message}");
                }
            }
            return "dotnet_bot.png"; // Imagen por defecto
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}