using System.Runtime.InteropServices;

namespace Jotunheim.App.Interop;

internal static class Sgp4Native
{
    [DllImport("Tle.dll", CallingConvention = CallingConvention.Cdecl)]
    internal static extern long TleAddSatFrLines(string line1, string line2);

    [DllImport("Tle.dll", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int TleRemoveSat(long satKey);

    [DllImport("Sgp4Prop.dll", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int Sgp4InitSat(long satKey);

    [DllImport("Sgp4Prop.dll", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int Sgp4RemoveSat(long satKey);

    [DllImport("Sgp4Prop.dll", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int Sgp4PropDs50UTC(
        long satKey,
        double ds50UTC,
        out double mse,
        double[] pos,
        double[] vel,
        double[] llh);

    [DllImport("Sgp4Prop.dll", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int Sgp4PropMse(
        long satKey,
        double mse,
        out double ds50UTC,
        double[] pos,
        double[] vel,
        double[] llh);

    [DllImport("Sgp4Prop.dll", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void Sgp4SetLicFilePath(string licFilePath);
}
