using System;
using System.IO;

namespace MeuGestorVODs;

internal static class DiagnosticsLogger
{
    private static readonly object Sync = new();

    private static string LogDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MeuGestorVODs",
        "logs");

    private static string LogFilePath => Path.Combine(LogDirectory, $"app-{DateTime.Now:yyyy-MM-dd}.log");

    public static void Info(string component, string message)
    {
        Write("INFO", component, message, null);
    }

    public static void Warn(string component, string message)
    {
        Write("WARN", component, message, null);
    }

    public static void Error(string component, string message, Exception ex)
    {
        Write("ERROR", component, message, ex);
    }

    private static void Write(string level, string component, string message, Exception? ex)
    {
        try
        {
            lock (Sync)
            {
                Directory.CreateDirectory(LogDirectory);

                using var writer = new StreamWriter(LogFilePath, append: true);
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                writer.Write($"[{timestamp}] [{level}] [{component}] {message}");

                if (ex != null)
                {
                    writer.Write($" | {ex.GetType().Name}: {ex.Message}");
                }

                writer.WriteLine();
            }
        }
        catch
        {
        }
    }
}
