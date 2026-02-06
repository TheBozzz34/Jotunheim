namespace Jotunheim.App.Models;

public sealed class GroundStation
{
    public GroundStation(string name, double latitudeDeg, double longitudeDeg, double altitudeMeters)
    {
        Name = name;
        LatitudeDeg = latitudeDeg;
        LongitudeDeg = longitudeDeg;
        AltitudeMeters = altitudeMeters;
    }

    public string Name { get; }
    public double LatitudeDeg { get; }
    public double LongitudeDeg { get; }
    public double AltitudeMeters { get; }

    public string Summary => $"{LatitudeDeg:0.0000} deg, {LongitudeDeg:0.0000} deg, {AltitudeMeters:0.0} m";
}
