using Jotunheim.App.Models;

namespace Jotunheim.App.Utils;

internal static class TleParser
{
    public static IReadOnlyList<TleEntry> Parse(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Array.Empty<TleEntry>();
        }

        var lines = raw
            .Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .ToArray();

        var results = new List<TleEntry>();
        string? pendingName = null;

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];

            if (line.StartsWith("0 "))
            {
                pendingName = line[2..].Trim();
                continue;
            }

            if (!line.StartsWith("1 "))
            {
                pendingName = line;
                continue;
            }

            if (index + 1 >= lines.Length)
            {
                break;
            }

            var line1 = line;
            var line2 = lines[index + 1];
            if (!line2.StartsWith("2 "))
            {
                continue;
            }

            results.Add(new TleEntry(pendingName, line1, line2));
            pendingName = null;
            index++;
        }

        return results;
    }
}
