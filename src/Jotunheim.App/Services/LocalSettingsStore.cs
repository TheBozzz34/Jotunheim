using System.Text.Json;

namespace Jotunheim.App.Services;

internal static class LocalSettingsStore
{
    private static readonly object Sync = new();
    private static Dictionary<string, string>? _cache;

    public static bool GetBool(string key, bool defaultValue = false)
    {
        var value = GetString(key);
        return bool.TryParse(value, out var result) ? result : defaultValue;
    }

    public static void SetBool(string key, bool value)
    {
        SetString(key, value.ToString());
    }

    public static string? GetString(string key)
    {
        EnsureLoaded();
        lock (Sync)
        {
            return _cache != null && _cache.TryGetValue(key, out var value) ? value : null;
        }
    }

    public static void SetString(string key, string? value)
    {
        EnsureLoaded();
        lock (Sync)
        {
            if (_cache is null)
            {
                _cache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                _cache.Remove(key);
            }
            else
            {
                _cache[key] = value;
            }
        }

        Save();
    }

    private static void EnsureLoaded()
    {
        lock (Sync)
        {
            if (_cache is not null)
            {
                return;
            }

            try
            {
                var path = AppStorage.SettingsPath;
                if (!File.Exists(path))
                {
                    _cache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    return;
                }

                var json = File.ReadAllText(path);
                _cache = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ??
                         new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                _cache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
        }
    }

    private static void Save()
    {
        try
        {
            Dictionary<string, string> snapshot;
            lock (Sync)
            {
                snapshot = _cache is null
                    ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, string>(_cache, StringComparer.OrdinalIgnoreCase);
            }

            var json = JsonSerializer.Serialize(snapshot);
            File.WriteAllText(AppStorage.SettingsPath, json);
        }
        catch
        {
        }
    }
}
