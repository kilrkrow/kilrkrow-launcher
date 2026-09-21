using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KilrkrowLauncher.Models;

namespace KilrkrowLauncher.Catalog;

/// <summary>
/// On-disk zip entry listing keyed by asset download URL so fallback catalog
/// scans do not re-download central directories.
/// </summary>
public sealed class CachingZipEntryReader : IZipEntryReader
{
    private readonly IZipEntryReader _inner;
    private readonly string _directory;

    public CachingZipEntryReader(IZipEntryReader inner, string directory)
    {
        _inner = inner;
        _directory = directory;
    }

    public async Task<IReadOnlyList<string>?> TryListEntriesAsync(ReleaseAsset asset, CancellationToken cancellationToken)
    {
        if (TryRead(asset.BrowserDownloadUrl, out var cached))
            return cached;

        var names = await _inner.TryListEntriesAsync(asset, cancellationToken).ConfigureAwait(false);
        if (names is not null)
            Write(asset.BrowserDownloadUrl, names);
        return names;
    }

    private bool TryRead(string url, out IReadOnlyList<string>? names)
    {
        names = null;
        if (string.IsNullOrWhiteSpace(url))
            return false;
        var path = PathFor(url);
        if (!File.Exists(path))
            return false;
        try
        {
            var entry = JsonSerializer.Deserialize<CacheEntry>(File.ReadAllText(path));
            if (entry?.Entries is null)
                return false;
            names = entry.Entries;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void Write(string url, IReadOnlyList<string> names)
    {
        Directory.CreateDirectory(_directory);
        var json = JsonSerializer.Serialize(new CacheEntry { Url = url, Entries = names.ToArray() });
        File.WriteAllText(PathFor(url), json);
    }

    private string PathFor(string url)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url)));
        return Path.Combine(_directory, hash + ".json");
    }

    private sealed class CacheEntry
    {
        public string? Url { get; set; }
        public string[]? Entries { get; set; }
    }
}
