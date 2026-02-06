using System.Text.Json;
using Jotunheim.App.Models;

namespace Jotunheim.App.Services;

internal sealed class GroundStationStore
{
    private const string KeySelected = "GroundStation.Selected";

    public IReadOnlyList<GroundStation> LoadAll()
    {
        try
        {
            if (!File.Exists(AppStorage.StationsPath))
            {
                return Array.Empty<GroundStation>();
            }

            var json = File.ReadAllText(AppStorage.StationsPath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return Array.Empty<GroundStation>();
            }

            var data = JsonSerializer.Deserialize<List<StationRecord>>(json);
            if (data is null)
            {
                return Array.Empty<GroundStation>();
            }

            return data
                .Where(item => !string.IsNullOrWhiteSpace(item.Name))
                .Select(item => new GroundStation(item.Name!, item.LatitudeDeg, item.LongitudeDeg, item.AltitudeMeters))
                .ToList();
        }
        catch
        {
            return Array.Empty<GroundStation>();
        }
    }

    public void SaveAll(IEnumerable<GroundStation> stations)
    {
        var list = stations
            .Select(item => new StationRecord
            {
                Name = item.Name,
                LatitudeDeg = item.LatitudeDeg,
                LongitudeDeg = item.LongitudeDeg,
                AltitudeMeters = item.AltitudeMeters
            })
            .ToList();

        var json = JsonSerializer.Serialize(list);
        File.WriteAllText(AppStorage.StationsPath, json);
    }

    public string? LoadSelectedName()
    {
        return LocalSettingsStore.GetString(KeySelected);
    }

    public void SaveSelectedName(string? name)
    {
        LocalSettingsStore.SetString(KeySelected, name);
    }

    private sealed class StationRecord
    {
        public string? Name { get; set; }
        public double LatitudeDeg { get; set; }
        public double LongitudeDeg { get; set; }
        public double AltitudeMeters { get; set; }
    }
}
