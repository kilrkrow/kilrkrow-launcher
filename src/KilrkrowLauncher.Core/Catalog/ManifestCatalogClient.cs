using System.Net;
using System.Net.Http.Headers;
using KilrkrowLauncher.Http;

namespace KilrkrowLauncher.Catalog;

public sealed class ManifestLoadResult
{
    public required CatalogManifest Manifest { get; init; }
    public required IReadOnlyList<KilrkrowLauncher.Models.CatalogTool> Tools { get; init; }
    public bool FromCache { get; init; }
    public bool NotModified { get; init; }
}

/// <summary>
/// Happy-path catalog: one static JSON GET (not counted against the GitHub API budget).
/// </summary>
public sealed class ManifestCatalogClient
{
    public const string DefaultManifestUrl = "https://kilrkrow.github.io/kilrkrow-launcher/catalog.json";

    private readonly HttpClient _http;
    private readonly string _cachePath;
    private readonly string _etagPath;
    private readonly string _manifestUrl;

    public ManifestCatalogClient(HttpClient http, string cachePath, string? manifestUrl = null)
    {
        _http = http;
        _cachePath = cachePath;
        _etagPath = cachePath + ".etag";
        _manifestUrl = string.IsNullOrWhiteSpace(manifestUrl) ? DefaultManifestUrl : manifestUrl;
    }

    public static string DefaultCachePath(string localAppData)
        => Path.Combine(localAppData, "KilrkrowLauncher", "catalog.json");

    public CatalogManifest? TryLoadCache()
    {
        if (!File.Exists(_cachePath))
            return null;
        try
        {
            return CatalogJson.Parse(File.ReadAllText(_cachePath));
        }
        catch
        {
            return null;
        }
    }

    public async Task<ManifestLoadResult> LoadAsync(CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, _manifestUrl);
        request.Headers.UserAgent.ParseAdd(GitHubHttpClient.DefaultUserAgent);
        if (File.Exists(_etagPath))
        {
            var etag = File.ReadAllText(_etagPath).Trim();
            if (etag.Length > 0)
            {
                try
                {
                    request.Headers.IfNoneMatch.Add(EntityTagHeaderValue.Parse(etag));
                }
                catch
                {
                    // ignore malformed sidecar
                }
            }
        }

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotModified)
        {
            var cached = TryLoadCache() ?? throw new InvalidOperationException("Manifest 304 without a local cache.");
            return new ManifestLoadResult { Manifest = cached, Tools = cached.ToCatalogTools(), FromCache = true, NotModified = true };
        }

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException("Manifest HTTP " + (int)response.StatusCode + " from " + _manifestUrl);

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var manifest = CatalogJson.Parse(json);
        WriteCache(json, response.Headers.ETag?.ToString());
        return new ManifestLoadResult { Manifest = manifest, Tools = manifest.ToCatalogTools(), FromCache = false };
    }

    private void WriteCache(string json, string? etag)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_cachePath) ?? ".");
        File.WriteAllText(_cachePath, json);
        if (!string.IsNullOrWhiteSpace(etag))
            File.WriteAllText(_etagPath, etag);
    }
}
