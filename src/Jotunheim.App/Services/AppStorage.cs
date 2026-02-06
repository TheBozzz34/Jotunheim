namespace Jotunheim.App.Services;

internal static class AppStorage
{
    private static readonly Lazy<string> BasePathLazy = new(() =>
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var path = Path.Combine(root, "Jotunheim");
        Directory.CreateDirectory(path);
        return path;
    });

    public static string BasePath => BasePathLazy.Value;

    public static string LogPath => Path.Combine(BasePath, "jotunheim.log");

    public static string SettingsPath => Path.Combine(BasePath, "settings.json");

    public static string StationsPath => Path.Combine(BasePath, "stations.json");

    public static string CredentialsPath => Path.Combine(BasePath, "credentials.dat");
}
