using System.Runtime.InteropServices;

namespace Jotunheim.App.Interop;

internal static class TimeFuncNative
{
    [DllImport("TimeFunc.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    internal static extern double DTGToUTCExt([MarshalAs(UnmanagedType.LPStr)] string dtg);
}
