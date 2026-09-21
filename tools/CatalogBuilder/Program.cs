using KilrkrowLauncher.Catalog;

var outPath = "catalog.json";
for (var i = 0; i < args.Length; i++)
{
    if (args[i] is "--out" && i + 1 < args.Length)
        outPath = args[++i];
}

var token = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
var client = GitHubCatalogClient.Create(http, () => token);

var tools = await client.LoadPublicWindowsToolsAsync();
var launcher = await client.LoadSelfLauncherAsync();

var manifest = new CatalogManifest
{
    GeneratedAt = DateTimeOffset.UtcNow,
    Launcher = launcher,
    Tools = tools.Select(t => new CatalogManifestTool
    {
        Owner = t.Owner,
        Repo = t.Repo,
        DisplayName = t.DisplayName,
        Description = t.Description,
        TagName = t.TagName,
        HtmlUrl = t.HtmlUrl,
        Asset = new CatalogManifestAsset
        {
            Name = t.WindowsAsset.Asset.Name,
            BrowserDownloadUrl = t.WindowsAsset.Asset.BrowserDownloadUrl,
            Size = t.WindowsAsset.Asset.Size,
            ContentType = t.WindowsAsset.Asset.ContentType,
            Kind = AssetKind.FromFileName(t.WindowsAsset.Asset.Name)
        }
    }).ToList()
};

var dir = Path.GetDirectoryName(Path.GetFullPath(outPath));
if (!string.IsNullOrWhiteSpace(dir))
    Directory.CreateDirectory(dir);
File.WriteAllText(outPath, CatalogJson.Serialize(manifest));
Console.WriteLine("Wrote " + outPath + " tools=" + manifest.Tools.Count + " launcher=" + (launcher?.Tag ?? "none"));
