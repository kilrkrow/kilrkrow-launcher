using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace KilrkrowLauncher.Http;

public sealed class DiskEtagCache
{
    private readonly string _directory;

    public DiskEtagCache(string directory)
    {
        _directory = directory;
    }

    public bool TryGet(string url, out string? etag, out string? body)
    {
        etag = null;
        body = null;
        var path = PathFor(url);
        if (!File.Exists(path))
            return false;

        try
        {
            var json = File.ReadAllText(path);
            var entry = JsonSerializer.Deserialize<Entry>(json);
            if (entry is null || string.IsNullOrWhiteSpace(entry.Body))
                return false;
            etag = entry.Etag;
            body = entry.Body;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Set(string url, string? etag, string body)
    {
        Directory.CreateDirectory(_directory);
        var entry = new Entry { Url = url, Etag = etag, Body = body };
        File.WriteAllText(PathFor(url), JsonSerializer.Serialize(entry));
    }

    private string PathFor(string url)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url)));
        return Path.Combine(_directory, hash + ".json");
    }

    private sealed class Entry
    {
        public string? Url { get; set; }
        public string? Etag { get; set; }
        public string? Body { get; set; }
    }
}
