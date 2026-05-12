using System.Diagnostics;
using System.Text;

namespace SoundTracker.App.Diagnostics;

internal static class AppLog
{
    private static readonly object Sync = new();
    private static readonly string LogDirectory;
    private static readonly string LogPathValue;

    static AppLog()
    {
        try
        {
            LogDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SoundTracker",
                "logs");
            Directory.CreateDirectory(LogDirectory);
            LogPathValue = Path.Combine(LogDirectory, $"soundtracker-{DateTime.UtcNow:yyyyMMdd}.log");
            Write("INFO", "logger initialized");
        }
        catch
        {
            LogDirectory = string.Empty;
            LogPathValue = string.Empty;
        }
    }

    internal static string LogPath => LogPathValue;

    internal static void Info(string message)
    {
        Write("INFO", message);
    }

    internal static void Warn(string message)
    {
        Write("WARN", message);
    }

    internal static void Error(string message, Exception? exception = null)
    {
        if (exception is null)
        {
            Write("ERROR", message);
            return;
        }

        Write("ERROR", $"{message}{Environment.NewLine}{exception}");
    }

    private static void Write(string level, string message)
    {
        if (string.IsNullOrWhiteSpace(LogPathValue))
        {
            return;
        }

        try
        {
            var line = new StringBuilder()
                .Append(DateTime.UtcNow.ToString("O"))
                .Append(" [")
                .Append(level)
                .Append("] pid=")
                .Append(Environment.ProcessId)
                .Append(" tid=")
                .Append(Environment.CurrentManagedThreadId)
                .Append(' ')
                .Append(message)
                .AppendLine()
                .ToString();

            lock (Sync)
            {
                File.AppendAllText(LogPathValue, line, Encoding.UTF8);
            }

            Debug.WriteLine(line);
        }
        catch
        {
        }
    }
}
