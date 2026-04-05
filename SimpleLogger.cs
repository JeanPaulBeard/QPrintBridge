namespace QPrintBridge;

public static class SimpleLogger
{
    private static readonly string LogDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
    private static readonly string LogFilePath = Path.Combine(LogDirectory, "error_log.txt");
    private static readonly object _lock = new object();

    static SimpleLogger()
    {
        if (!Directory.Exists(LogDirectory))
        {
            Directory.CreateDirectory(LogDirectory);
        }
    }

    public static void LogError(string message, Exception? ex = null)
    {
        lock (_lock)
        {
            try
            {
                using var writer = new StreamWriter(LogFilePath, true);
                writer.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ERROR: {message}");
                if (ex != null)
                {
                    writer.WriteLine($"Exception: {ex.Message}");
                    writer.WriteLine($"StackTrace: {ex.StackTrace}");
                    writer.WriteLine(new string('-', 50));
                }
            }
            catch
            {
                // Fallback: If we can't write to the log, ignore to prevent crashing the service
            }
        }
    }

    public static void LogInfo(string message)
    {
        lock (_lock)
        {
            try
            {
                using var writer = new StreamWriter(LogFilePath, true);
                writer.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] INFO: {message}");
            }
            catch
            {
            }
        }
    }
}
