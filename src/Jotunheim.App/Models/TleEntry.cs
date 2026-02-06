namespace Jotunheim.App.Models;

internal sealed record TleEntry(string? Name, string Line1, string Line2)
{
    public int? NoradId { get; } = TryParseNoradId(Line1);

    private static int? TryParseNoradId(string line1)
    {
        if (string.IsNullOrWhiteSpace(line1) || line1.Length < 7)
        {
            return null;
        }

        var token = line1.Substring(2, 5).Trim();
        return int.TryParse(token, out var id) ? id : null;
    }
}
