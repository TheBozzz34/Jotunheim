using System.Net;

namespace Jotunheim.App.Services;

internal sealed class SpaceTrackClient : IDisposable
{
    private readonly CookieContainer _cookies = new();
    private readonly HttpClient _client;
    private bool _loggedIn;

    public SpaceTrackClient()
    {
        var handler = new HttpClientHandler
        {
            CookieContainer = _cookies,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        };

        _client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://www.space-track.org/")
        };
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("Jotunheim/0.1");
    }

    public async Task LoginAsync(string username, string password, CancellationToken ct)
    {
        if (_loggedIn)
        {
            return;
        }

        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("identity", username),
            new KeyValuePair<string, string>("password", password)
        });

        using var response = await _client.PostAsync("ajaxauth/login", content, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode || body.Contains("failed", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Space-Track login failed. Check your credentials and account status.");
        }

        _loggedIn = true;
    }

    public async Task<string> FetchLatestTleByNoradAsync(int noradId, CancellationToken ct)
    {
        var query = $"basicspacedata/query/class/gp/NORAD_CAT_ID/{noradId}/orderby/EPOCH%20desc/limit/1/format/tle";
        using var response = await _client.GetAsync(query, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }

    public async Task<string> FetchLatestTlesByNoradAsync(IEnumerable<int> noradIds, CancellationToken ct)
    {
        var builder = new System.Text.StringBuilder();
        foreach (var noradId in noradIds)
        {
            var result = await FetchLatestTleByNoradAsync(noradId, ct);
            if (!string.IsNullOrWhiteSpace(result))
            {
                builder.AppendLine(result.Trim());
            }
        }

        return builder.ToString();
    }

    public void Dispose() => _client.Dispose();
}
