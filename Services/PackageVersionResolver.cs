using System.Text.Json;
using System.Text.RegularExpressions;

namespace dotkit.Services;

public class PackageVersionResolver
{
    private static readonly HttpClient DefaultHttpClient = new() { Timeout = TimeSpan.FromSeconds(10) };

    private static readonly Regex PackageIdRegex = new(
        "^[A-Za-z0-9](?:[A-Za-z0-9_.-]{0,98}[A-Za-z0-9_-])?$",
        RegexOptions.Compiled);

    private static readonly Dictionary<int, string> KnownVersions = new()
    {
        [6] = "6.0.36",
        [7] = "7.0.20",
        [8] = "8.0.29",
        [9] = "9.0.18",
        [10] = "10.0.10"
    };

    private readonly HttpClient _httpClient;

    public PackageVersionResolver(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? DefaultHttpClient;
    }

    public async Task<string> ResolveAsync(string packageId, int major)
    {
        if (major <= 0 || string.IsNullOrWhiteSpace(packageId) || !PackageIdRegex.IsMatch(packageId))
            return string.Empty;

        try
        {
            var url = $"https://api.nuget.org/v3-flatcontainer/{packageId.ToLowerInvariant()}/index.json";
            using var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync();
            using var document = await JsonDocument.ParseAsync(stream);

            var latest = document.RootElement.GetProperty("versions").EnumerateArray()
                .Select(v => v.GetString())
                .Where(v => v is not null)
                .Select(v => v!)
                .Where(v => v.StartsWith($"{major}.") && !v.Contains('-'))
                .Select(Version.Parse)
                .OrderBy(v => v)
                .LastOrDefault();

            if (latest is not null)
                return latest.ToString();
        }
        catch
        {
            // fall through to known versions
        }

        return KnownVersions.TryGetValue(major, out var known) ? known : string.Empty;
    }
}
