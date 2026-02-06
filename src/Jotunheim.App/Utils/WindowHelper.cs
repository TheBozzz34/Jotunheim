namespace Jotunheim.App.Utils;

internal static class WindowHelper
{
    public static IntPtr GetMainWindowHandle()
    {
        if (App.MainWindow is null)
        {
            throw new InvalidOperationException("Main window is not initialized.");
        }

        return WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
    }
}
