using KilrkrowLauncher.Catalog;

namespace KilrkrowLauncher.Tests;

public sealed class CatalogManifestTests
{
    private const string Sample = """
        {
          "generatedAt": "2026-09-19T08:00:00Z",
          "launcher": { "tag": "v0.2.0", "assetUrl": "https://example.test/KilrkrowLauncher.exe", "size": 12 },
          "tools": [
            {
              "owner": "kilrkrow",
              "repo": "win-service-buddy",
              "displayName": "Win Service Buddy",
              "description": "Windows services",
              "tagName": "v0.2.0",
              "htmlUrl": "https://github.com/kilrkrow/win-service-buddy",
              "asset": {
                "name": "wsbuddy-app-win-x64-v0.2.0.zip",
                "browserDownloadUrl": "https://example.test/app.zip",
                "size": 1200,
                "contentType": "application/zip",
                "kind": "zip"
              }
            }
          ]
        }
        """;

    [Fact]
    public void Parse_MapsToolsAndLauncher()
    {
        var manifest = CatalogJson.Parse(Sample);
        Assert.Equal("v0.2.0", manifest.Launcher?.Tag);
        Assert.Equal("https://example.test/KilrkrowLauncher.exe", manifest.Launcher?.AssetUrl);
        Assert.Equal(12, manifest.Launcher?.Size);

        var tools = manifest.ToCatalogTools();
        Assert.Single(tools);
        Assert.Equal("win-service-buddy", tools[0].Repo);
        Assert.Equal("v0.2.0", tools[0].TagName);
        Assert.Equal("wsbuddy-app-win-x64-v0.2.0.zip", tools[0].WindowsAsset.Asset.Name);
        Assert.Equal("https://example.test/app.zip", tools[0].WindowsAsset.Asset.BrowserDownloadUrl);
    }

    [Fact]
    public void RoundTrip_PreservesKind()
    {
        var original = CatalogJson.Parse(Sample);
        var again = CatalogJson.Parse(CatalogJson.Serialize(original));
        Assert.Equal("zip", again.Tools[0].Asset.Kind);
        Assert.Equal(original.Tools[0].Asset.BrowserDownloadUrl, again.Tools[0].Asset.BrowserDownloadUrl);
    }
}

public sealed class LauncherVersionComparerTests
{
    [Theory]
    [InlineData("v1.2.0", "1.1.0", true)]
    [InlineData("1.0.0", "1.0.0", false)]
    [InlineData("v0.1.0", "0.1.0.0", false)]
    [InlineData("v0.2.0", "0.1.0", true)]
    [InlineData("0.1.0", "0.2.0", false)]
    [InlineData("", "1.0.0", false)]
    public void IsRemoteNewer(string remote, string local, bool expected)
    {
        Assert.Equal(expected, LauncherVersionComparer.IsRemoteNewer(remote, Version.Parse(local)));
    }
}
