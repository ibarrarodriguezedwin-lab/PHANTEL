namespace PHANTEL.Helpers
{
    public static class FileHelper
    {
        public static async Task<string> FileToBase64Async(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return string.Empty;

            try
            {
                byte[] bytes = await File.ReadAllBytesAsync(filePath);
                return Convert.ToBase64String(bytes);
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}