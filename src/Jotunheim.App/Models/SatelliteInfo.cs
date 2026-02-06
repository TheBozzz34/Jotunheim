namespace Jotunheim.App.Models;

public sealed class SatelliteInfo
{
    public SatelliteInfo(long satKey, string name, string line1, string line2, int? noradId)
    {
        SatKey = satKey;
        Name = name;
        Line1 = line1;
        Line2 = line2;
        NoradId = noradId;
    }

    public long SatKey { get; }
    public string Name { get; }
    public string Line1 { get; }
    public string Line2 { get; }
    public int? NoradId { get; }
    public string DisplayId => NoradId.HasValue ? $"NORAD {NoradId}" : "NORAD —";
}
