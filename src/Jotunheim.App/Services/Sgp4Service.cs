using System.Globalization;
using Jotunheim.App.Interop;
using Jotunheim.App.Models;

namespace Jotunheim.App.Services;

internal sealed class Sgp4Service
{
    private static readonly DateTime Ds50Epoch = new(1950, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private readonly Dictionary<int, SatelliteInfo> _byNorad = new();
    private readonly Dictionary<long, SatelliteInfo> _byKey = new();
    private readonly Dictionary<long, DateTime> _epochCache = new();

    public void Initialize() => NativeLibraryLoader.Initialize();

    public IReadOnlyCollection<SatelliteInfo> Satellites => _byKey.Values;

    public SatelliteInfo Upsert(TleEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.Line1) || string.IsNullOrWhiteSpace(entry.Line2))
        {
            throw new InvalidOperationException("TLE entry is missing line data.");
        }

        if (entry.NoradId.HasValue && _byNorad.TryGetValue(entry.NoradId.Value, out var existing))
        {
            Remove(existing);
        }

        var satKey = Sgp4Native.TleAddSatFrLines(entry.Line1, entry.Line2);
        if (satKey < 0)
        {
            throw new InvalidOperationException($"TLE load failed (satKey={satKey}).");
        }

        var initCode = Sgp4Native.Sgp4InitSat(satKey);
        if (initCode != 0)
        {
            _ = Sgp4Native.TleRemoveSat(satKey);
            throw new InvalidOperationException($"SGP4 initialization failed (error={initCode}).");
        }

        var displayName = !string.IsNullOrWhiteSpace(entry.Name)
            ? entry.Name.Trim()
            : entry.NoradId.HasValue
                ? $"NORAD {entry.NoradId}"
                : $"SAT {satKey}";

        var info = new SatelliteInfo(satKey, displayName, entry.Line1, entry.Line2, entry.NoradId);
        _byKey[satKey] = info;
        if (entry.NoradId.HasValue)
        {
            _byNorad[entry.NoradId.Value] = info;
        }

        return info;
    }

    public void Remove(SatelliteInfo info)
    {
        _ = Sgp4Native.TleRemoveSat(info.SatKey);
        _ = Sgp4Native.Sgp4RemoveSat(info.SatKey);

        _byKey.Remove(info.SatKey);
        if (info.NoradId.HasValue)
        {
            _byNorad.Remove(info.NoradId.Value);
        }
    }

    public Sgp4State Propagate(SatelliteInfo info, DateTime timestampUtc)
    {
        var pos = new double[3];
        var vel = new double[3];
        var llh = new double[3];

        var utc = timestampUtc.ToUniversalTime();
        double ds50Utc;
        double mse;
        int errCode;

        if (TryGetEpochUtc(info, out var epochUtc))
        {
            mse = (utc - epochUtc).TotalMinutes;
            errCode = Sgp4Native.Sgp4PropMse(info.SatKey, mse, out ds50Utc, pos, vel, llh);
        }
        else
        {
            ds50Utc = ToDs50Utc(utc);
            errCode = Sgp4Native.Sgp4PropDs50UTC(info.SatKey, ds50Utc, out mse, pos, vel, llh);
        }

        if (errCode != 0)
        {
            throw new InvalidOperationException($"SGP4 propagation failed (error={errCode}).");
        }

        var lat = llh[0];
        var lon = NormalizeLongitude(llh[1]);
        var alt = llh[2];

        return new Sgp4State(
            utc,
            ds50Utc,
            mse,
            lat,
            lon,
            alt,
            pos[0],
            pos[1],
            pos[2],
            vel[0],
            vel[1],
            vel[2]);
    }

    private static double ToDs50Utc(DateTime utc)
    {
        var dt = utc.ToUniversalTime();
        try
        {
            var dtg = dt.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
            var ds50 = TimeFuncNative.DTGToUTCExt(dtg);
            if (ds50 > 0)
            {
                return ds50;
            }
        }
        catch
        {
        }

        return (dt - Ds50Epoch).TotalDays;
    }

    private bool TryGetEpochUtc(SatelliteInfo info, out DateTime epochUtc)
    {
        if (_epochCache.TryGetValue(info.SatKey, out epochUtc))
        {
            return true;
        }

        if (TryParseTleEpochUtc(info.Line1, out epochUtc))
        {
            _epochCache[info.SatKey] = epochUtc;
            return true;
        }

        return false;
    }

    private static bool TryParseTleEpochUtc(string line1, out DateTime epochUtc)
    {
        epochUtc = default;
        if (string.IsNullOrWhiteSpace(line1) || line1.Length < 32)
        {
            return false;
        }

        try
        {
            var epochField = line1.Substring(18, 14);
            var yearPart = epochField[..2];
            var dayPart = epochField[2..];

            if (!int.TryParse(yearPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out var yy))
            {
                return false;
            }

            if (!double.TryParse(dayPart, NumberStyles.Float, CultureInfo.InvariantCulture, out var dayOfYear))
            {
                return false;
            }

            var year = yy < 57 ? 2000 + yy : 1900 + yy;
            var wholeDay = (int)Math.Floor(dayOfYear);
            var fractionalDay = dayOfYear - wholeDay;

            epochUtc = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                .AddDays(wholeDay - 1)
                .AddSeconds(fractionalDay * 86400.0);

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static double NormalizeLongitude(double lon)
    {
        var normalized = lon % 360.0;
        if (normalized > 180.0)
        {
            normalized -= 360.0;
        }
        else if (normalized < -180.0)
        {
            normalized += 360.0;
        }

        return normalized;
    }

    public void Clear()
    {
        var all = _byKey.Values.ToList();
        foreach (var sat in all)
        {
            Remove(sat);
        }
    }
}
