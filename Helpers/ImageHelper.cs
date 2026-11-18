using Microsoft.Maui.Graphics.Platform;

namespace PHANTEL.Helpers
{
    public static class ImageHelper
    {
        public static async Task<string> ComprimirImagenABase64Async(FileResult archivo, float maxAncho = 650f, float calidad = 0.65f)
        {
            if (archivo == null) return string.Empty;

            try
            {
                using var stream = await archivo.OpenReadAsync();
                using var imagenOriginal = PlatformImage.FromStream(stream);
                if (imagenOriginal == null) return string.Empty;

                using var imagenRedimensionada = imagenOriginal.Width > maxAncho
                    ? imagenOriginal.Downsize(maxAncho, disposeOriginal: true)
                    : imagenOriginal;

                using var msSalida = new MemoryStream();
                await imagenRedimensionada.SaveAsync(msSalida, ImageFormat.Jpeg, calidad);

                return Convert.ToBase64String(msSalida.ToArray());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[IMAGE HELPER ERROR]: {ex.Message}");
                return string.Empty;
            }
        }
    }
}