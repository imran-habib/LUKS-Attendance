#nullable enable
using System;
using System.IO;

namespace LuksAttendance;

/// <summary>
/// Simple file-based logger. Writes to luks_app.log next to the executable.
/// Thread-safe via lock.
/// </summary>
public static class AppLogger
{
    private static readonly object _lock = new();
    private static readonly string _logPath = Path.Combine(AppContext.BaseDirectory, "luks_app.log");
    private const long MaxLogSize = 5 * 1024 * 1024; // 5 MB

    public static void Log(string message)
    {
        try
        {
            lock (_lock)
            {
                RotateIfNeeded();
                File.AppendAllText(_logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
            }
        }
        catch { /* Last resort: don't crash the app for logging */ }
    }

    public static void Log(string context, Exception ex)
    {
        Log($"[{context}] {ex.GetType().Name}: {ex.Message}");
    }

    public static void LogWarning(string message)
    {
        Log($"WARN: {message}");
    }

    private static void RotateIfNeeded()
    {
        try
        {
            if (File.Exists(_logPath) && new FileInfo(_logPath).Length > MaxLogSize)
            {
                var backup = _logPath + ".old";
                if (File.Exists(backup)) File.Delete(backup);
                File.Move(_logPath, backup);
            }
        }
        catch { }
    }
}
