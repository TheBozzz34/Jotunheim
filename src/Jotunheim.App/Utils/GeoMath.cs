namespace Jotunheim.App.Utils;

internal static class GeoMath
{
    private const double Wgs84A = 6378137.0; // meters
    private const double Wgs84F = 1.0 / 298.257223563;
    private const double Wgs84E2 = Wgs84F * (2 - Wgs84F);
    private const double Wgs84EPrime2 = Wgs84E2 / (1 - Wgs84E2);

    public static (double X, double Y, double Z) GeodeticToEcef(double latDeg, double lonDeg, double altKm)
    {
        var lat = DegreesToRadians(latDeg);
        var lon = DegreesToRadians(lonDeg);
        var sinLat = Math.Sin(lat);
        var cosLat = Math.Cos(lat);
        var cosLon = Math.Cos(lon);
        var sinLon = Math.Sin(lon);

        var aKm = Wgs84A / 1000.0;
        var e2 = Wgs84E2;
        var n = aKm / Math.Sqrt(1 - e2 * sinLat * sinLat);
        var x = (n + altKm) * cosLat * cosLon;
        var y = (n + altKm) * cosLat * sinLon;
        var z = (n * (1 - e2) + altKm) * sinLat;

        return (x, y, z);
    }

    public static (double East, double North, double Up) EcefToEnu(
        double dx,
        double dy,
        double dz,
        double latDeg,
        double lonDeg)
    {
        var lat = DegreesToRadians(latDeg);
        var lon = DegreesToRadians(lonDeg);
        var sinLat = Math.Sin(lat);
        var cosLat = Math.Cos(lat);
        var sinLon = Math.Sin(lon);
        var cosLon = Math.Cos(lon);

        var east = -sinLon * dx + cosLon * dy;
        var north = -sinLat * cosLon * dx - sinLat * sinLon * dy + cosLat * dz;
        var up = cosLat * cosLon * dx + cosLat * sinLon * dy + sinLat * dz;

        return (east, north, up);
    }

    public static bool TryLatLonToUtm(double latDeg, double lonDeg, out UtmResult result)
    {
        result = default;

        if (latDeg < -80.0 || latDeg > 84.0)
        {
            return false;
        }

        var zone = (int)Math.Floor((lonDeg + 180.0) / 6.0) + 1;
        if (latDeg >= 56.0 && latDeg < 64.0 && lonDeg >= 3.0 && lonDeg < 12.0)
        {
            zone = 32;
        }

        if (latDeg >= 72.0 && latDeg < 84.0)
        {
            if (lonDeg >= 0.0 && lonDeg < 9.0) zone = 31;
            else if (lonDeg >= 9.0 && lonDeg < 21.0) zone = 33;
            else if (lonDeg >= 21.0 && lonDeg < 33.0) zone = 35;
            else if (lonDeg >= 33.0 && lonDeg < 42.0) zone = 37;
        }

        var lat = DegreesToRadians(latDeg);
        var lon = DegreesToRadians(lonDeg);
        var lon0 = DegreesToRadians((zone - 1) * 6 - 180 + 3);

        var sinLat = Math.Sin(lat);
        var cosLat = Math.Cos(lat);
        var tanLat = Math.Tan(lat);

        var n = Wgs84A / Math.Sqrt(1 - Wgs84E2 * sinLat * sinLat);
        var t = tanLat * tanLat;
        var c = Wgs84EPrime2 * cosLat * cosLat;
        var a = cosLat * (lon - lon0);

        var m = Wgs84A * ((1 - Wgs84E2 / 4 - 3 * Wgs84E2 * Wgs84E2 / 64 - 5 * Math.Pow(Wgs84E2, 3) / 256) * lat
            - (3 * Wgs84E2 / 8 + 3 * Wgs84E2 * Wgs84E2 / 32 + 45 * Math.Pow(Wgs84E2, 3) / 1024) * Math.Sin(2 * lat)
            + (15 * Wgs84E2 * Wgs84E2 / 256 + 45 * Math.Pow(Wgs84E2, 3) / 1024) * Math.Sin(4 * lat)
            - (35 * Math.Pow(Wgs84E2, 3) / 3072) * Math.Sin(6 * lat));

        const double k0 = 0.9996;
        var easting = k0 * n * (a + (1 - t + c) * Math.Pow(a, 3) / 6 + (5 - 18 * t + t * t + 72 * c - 58 * Wgs84EPrime2) * Math.Pow(a, 5) / 120) + 500000.0;
        var northing = k0 * (m + n * tanLat * (a * a / 2 + (5 - t + 9 * c + 4 * c * c) * Math.Pow(a, 4) / 24 + (61 - 58 * t + t * t + 600 * c - 330 * Wgs84EPrime2) * Math.Pow(a, 6) / 720));

        var hemisphere = latDeg >= 0 ? "N" : "S";
        if (latDeg < 0)
        {
            northing += 10000000.0;
        }

        result = new UtmResult(zone, hemisphere, easting, northing);
        return true;
    }

    public static (double LatDeg, double LonDeg, double AltKm) EciTemeToGeodetic(double xKm, double yKm, double zKm, DateTime utc)
    {
        var gmst = GreenwichMeanSiderealTime(utc);
        var cosGmst = Math.Cos(gmst);
        var sinGmst = Math.Sin(gmst);

        // TEME (ECI) to ECEF via rotation about Z by -GMST
        var x = cosGmst * xKm + sinGmst * yKm;
        var y = -sinGmst * xKm + cosGmst * yKm;
        var z = zKm;

        return EcefToGeodetic(x, y, z);
    }

    public static (double LatDeg, double LonDeg, double AltKm) EcefToGeodetic(double xKm, double yKm, double zKm)
    {
        var aKm = Wgs84A / 1000.0;
        var e2 = Wgs84E2;

        var lon = Math.Atan2(yKm, xKm);
        var p = Math.Sqrt(xKm * xKm + yKm * yKm);
        var lat = Math.Atan2(zKm, p * (1 - e2));

        double alt = 0;
        for (var i = 0; i < 5; i++)
        {
            var sinLat = Math.Sin(lat);
            var n = aKm / Math.Sqrt(1 - e2 * sinLat * sinLat);
            alt = p / Math.Cos(lat) - n;
            lat = Math.Atan2(zKm, p * (1 - e2 * (n / (n + alt))));
        }

        var latDeg = RadiansToDegrees(lat);
        var lonDeg = NormalizeLongitudeDegrees(RadiansToDegrees(lon));
        return (latDeg, lonDeg, alt);
    }

    private static double DegreesToRadians(double degrees)
        => degrees * Math.PI / 180.0;

    private static double RadiansToDegrees(double radians)
        => radians * 180.0 / Math.PI;

    private static double NormalizeLongitudeDegrees(double lonDeg)
    {
        var normalized = lonDeg % 360.0;
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

    private static double GreenwichMeanSiderealTime(DateTime utc)
    {
        var jd = JulianDate(utc);
        var t = (jd - 2451545.0) / 36525.0;

        var gmstDeg = 280.46061837
                      + 360.98564736629 * (jd - 2451545.0)
                      + 0.000387933 * t * t
                      - (t * t * t) / 38710000.0;

        gmstDeg %= 360.0;
        if (gmstDeg < 0)
        {
            gmstDeg += 360.0;
        }

        return DegreesToRadians(gmstDeg);
    }

    private static double JulianDate(DateTime utc)
    {
        var dt = utc.ToUniversalTime();
        var year = dt.Year;
        var month = dt.Month;
        var day = dt.Day;
        var hour = dt.Hour;
        var minute = dt.Minute;
        var second = dt.Second + dt.Millisecond / 1000.0;

        if (month <= 2)
        {
            year -= 1;
            month += 12;
        }

        var a = year / 100;
        var b = 2 - a + a / 4;
        var dayFraction = (hour + (minute + second / 60.0) / 60.0) / 24.0;

        var jd = Math.Floor(365.25 * (year + 4716))
                 + Math.Floor(30.6001 * (month + 1))
                 + day + dayFraction + b - 1524.5;

        return jd;
    }
}

internal readonly record struct UtmResult(int Zone, string Hemisphere, double EastingMeters, double NorthingMeters);
