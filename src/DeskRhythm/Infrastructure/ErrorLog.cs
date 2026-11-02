namespace DeskRhythm.Infrastructure;

/// <summary>A small local log for troubleshooting. Kept under 1 MB. Never sent anywhere.</summary>
internal static class ErrorLog
{
    private const long MaxBytes = 1024 * 1024;
    private static readonly object Gate = new();

    public static void Write(Exception? exception, string context)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(AppPaths.DataDirectory);
                var path = AppPaths.ErrorLog;
                if (File.Exists(path) && new FileInfo(path).Length > MaxBytes)
                {
                    File.Delete(path);
                }

                File.AppendAllText(path, $"{DateTimeOffset.Now:O} [{context}]{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}");
            }
        }
        catch
        {
            // Logging must never crash the app.
        }
    }
}
