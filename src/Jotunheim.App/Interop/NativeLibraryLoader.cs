using System.Runtime.InteropServices;

namespace Jotunheim.App.Interop;

internal static class NativeLibraryLoader
{
    private static int _initialized;

    [DllImport("kernel32", SetLastError = true)]
    private static extern bool SetDllDirectory(string lpPathName);

    internal static string NativeDirectory { get; private set; } = string.Empty;

    public static void Initialize()
    {
        if (Interlocked.Exchange(ref _initialized, 1) == 1)
        {
            return;
        }

        var nativeDir = Path.Combine(AppContext.BaseDirectory, "Native");
        if (!Directory.Exists(nativeDir))
        {
            return;
        }

        NativeDirectory = nativeDir;
        _ = SetDllDirectory(nativeDir);
        try
        {
            Sgp4Native.Sgp4SetLicFilePath(nativeDir);
        }
        catch (DllNotFoundException)
        {
        }
    }
}
