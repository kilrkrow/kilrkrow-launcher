using System.Text.Json;
using System.Text.Json.Serialization;
using KilrkrowLauncher.Models;

namespace KilrkrowLauncher.Catalog;

public sealed class CatalogManifest
{
    public DateTimeOffset GeneratedAt { get; set; }
    public CatalogLauncherInfo? Launcher { get; set; }
    public List<CatalogManifestTool> Tools { get; set; } = [];

    public IReadOnlyList<CatalogTool> ToCatalogTools()
        => Tools.Select(t => t.ToCatalogTool()).ToArray();
}

public sealed class CatalogLauncherInfo
{
    public string Tag { get; set; } = "";
    public string AssetUrl { get; set; } = "";
    public long Size { get; set; }
}

public sealed class CatalogManifestTool
{
    public string Owner { get; set; } = "";
    public string Repo { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string? Description { get; set; }
    public string TagName { get; set; } = "";
    public string HtmlUrl { get; set; } = "";
    public CatalogManifestAsset Asset { get; set; } = new();

    public CatalogTool ToCatalogTool()
    {
        var name = Asset.Name ?? "";
        var kind = string.IsNullOrWhiteSpace(Asset.Kind) ? AssetKind.FromFileName(name) : Asset.Kind;
        IReadOnlyList<string> exes = kind.Equals("exe", StringComparison.OrdinalIgnoreCase)
            ? [name]
            : [];

        return new CatalogTool
        {
            Owner = Owner,
            Repo = Repo,
            DisplayName = string.IsNullOrWhiteSpace(DisplayName) ? Repo : DisplayName,
            Description = Description,
            TagName = TagName,
            HtmlUrl = HtmlUrl,
            WindowsAsset = new WindowsAssetPick
            {
                Asset = new ReleaseAsset
                {
                    Name = name,
                    BrowserDownloadUrl = Asset.BrowserDownloadUrl,
                    Size = Asset.Size,
                    ContentType = Asset.ContentType
                },
                ExeEntryNames = exes
            }
        };
    }
}

public sealed class CatalogManifestAsset
{
    public string Name { get; set; } = "";
    public string BrowserDownloadUrl { get; set; } = "";
    public long Size { get; set; }
    public string? ContentType { get; set; }
    public string Kind { get; set; } = "";
}

public static class AssetKind
{
    public static string FromFileName(string fileName)
    {
        if (fileName.EndsWith(".msi", StringComparison.OrdinalIgnoreCase))
            return "msi";
        if (fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            return "exe";
        if (fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            return "zip";
        return "unknown";
    }
}

public static class CatalogJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static CatalogManifest Parse(string json)
    {
        var manifest = JsonSerializer.Deserialize<CatalogManifest>(json, Options);
        if (manifest is null)
            throw new InvalidOperationException("catalog.json was empty.");
        return manifest;
    }

    public static string Serialize(CatalogManifest manifest)
        => JsonSerializer.Serialize(manifest, Options);
}
