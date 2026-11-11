using System.Globalization;

namespace PHANTEL.Helpers
{
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
}