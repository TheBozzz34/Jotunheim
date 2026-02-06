using Windows.Storage;

namespace Jotunheim.App.Services;

internal static class AppLogger
{
    private static readonly object Sync = new();
    private static string? _logPath;
    private static readonly List<string> Memory = new();
    private const int MaxMemoryLines = 1000;

    public static string LogPath
    {
        get
        {
            if (_logPath is not null)
            {
                return _logPath;
            }

            var folder = AppStorage.BasePath;
            _logPath = Path.Combine(folder, "jotunheim.log");
            return _logPath;
        }
    }

    public static void Info(string message) => Write("INFO", message);
    public static void Warn(string message) => Write("WARN", message);
    public static void Error(string message) => Write("ERROR", message);

    public static void Write(string level, string message)
    {
        var line = $"{DateTime.UtcNow:O} [{level}] {message}";
        lock (Sync)
        {
            Memory.Add(line);
            if (Memory.Count > MaxMemoryLines)
            {
                Memory.RemoveRange(0, Memory.Count - MaxMemoryLines);
            }
        }

        try
        {
            EnsureExists();
            lock (Sync)
            {
                File.AppendAllText(LogPath, line + Environment.NewLine);
            }
        }
        catch
        {
            // Avoid throwing from logging paths.
        }

        System.Diagnostics.Debug.WriteLine(line);
    }

    public static string ReadTail(int maxLines = 200)
    {
        try
        {
            lock (Sync)
            {
                if (Memory.Count > 0)
                {
                    var start = Math.Max(0, Memory.Count - maxLines);
                    return string.Join(Environment.NewLine, Memory.GetRange(start, Memory.Count - start));
                }
            }

            if (!File.Exists(LogPath))
            {
                return string.Empty;
            }

            var lines = File.ReadAllLines(LogPath);
            if (lines.Length <= maxLines)
            {
                return string.Join(Environment.NewLine, lines);
            }

            return string.Join(Environment.NewLine, lines[^maxLines..]);
        }
        catch
        {
            return string.Empty;
        }
    }

    public static void Clear()
    {
        try
        {
            lock (Sync)
            {
                Memory.Clear();
            }

            EnsureExists();
            File.WriteAllText(LogPath, string.Empty);
        }
        catch
        {
        }
    }

    public static void EnsureExists()
    {
        try
        {
            var path = LogPath;
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (!File.Exists(path))
            {
                File.WriteAllText(path, string.Empty);
            }
        }
        catch
        {
        }
    }
}
