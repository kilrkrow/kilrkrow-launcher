using KilrkrowLauncher.Catalog;
using KilrkrowLauncher.Models;

namespace KilrkrowLauncher.Tests;

public sealed class WindowsAssetFilterTests
{
    [Fact]
    public void DirectExe_Qualifies()
    {
        Assert.True(WindowsAssetFilter.IsDirectWindowsBinary("Sideclip.exe"));
        Assert.True(WindowsAssetFilter.IsDirectWindowsBinary("setup.MSI"));
        Assert.False(WindowsAssetFilter.IsDirectWindowsBinary("main.js"));
        Assert.False(WindowsAssetFilter.IsDirectWindowsBinary("wsbuddy.0.2.0.nupkg"));
    }

    [Fact]
    public async Task EmptyAssets_AreRejected()
    {
        var filter = new WindowsAssetFilter(new MapZipReader());
        var pick = await filter.PickAsync([]);
        Assert.Null(pick);
    }

    [Fact]
    public async Task SourceOnlyZip_IsRejected_EvenWhenNamedWindows()
    {
        var source = FixtureZips.WindowsNamedSourceOnly();
        var reader = new MapZipReader
        {
            ["voltdesk-win-x64-source.zip"] = ZipExeInspector.ListEntries(new MemoryStream(source))
        };
        var filter = new WindowsAssetFilter(reader);
        var pick = await filter.PickAsync(
        [
            Asset("voltdesk-win-x64-source.zip", 1200)
        ]);
        Assert.Null(pick);
    }

    [Fact]
    public async Task SourceZip_WithExeConfigOnly_IsRejected()
    {
        var source = FixtureZips.SourceOnly();
        var reader = new MapZipReader
        {
            ["sideclip-source.zip"] = ZipExeInspector.ListEntries(new MemoryStream(source))
        };
        var filter = new WindowsAssetFilter(reader);
        var pick = await filter.PickAsync([Asset("sideclip-source.zip", 800)]);
        Assert.Null(pick);
    }

    [Fact]
    public async Task ZipContainingExe_IsAccepted()
    {
        var bytes = FixtureZips.WithExe("payload/WinServiceBuddy.App.exe");
        var reader = new MapZipReader
        {
            ["wsbuddy-app-win-x64-v0.2.0.zip"] = ZipExeInspector.ListEntries(new MemoryStream(bytes))
        };
        var filter = new WindowsAssetFilter(reader);
        var pick = await filter.PickAsync([Asset("wsbuddy-app-win-x64-v0.2.0.zip", 44_000_000)]);
        Assert.NotNull(pick);
        Assert.Equal("wsbuddy-app-win-x64-v0.2.0.zip", pick!.Asset.Name);
        Assert.Contains(pick.ExeEntryNames, n => n.EndsWith("WinServiceBuddy.App.exe", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task UninspectableZip_IsRejected()
    {
        var filter = new WindowsAssetFilter(new MapZipReader()); // no listing => null
        var pick = await filter.PickAsync([Asset("mystery-win-x64.zip", 50_000_000)]);
        Assert.Null(pick);
    }

    [Fact]
    public async Task PrefersAppZipOverCliZip()
    {
        var app = FixtureZips.WithExe("WinServiceBuddy.App.exe");
        var cli = FixtureZips.WithExe("wsbuddy.exe");
        var reader = new MapZipReader
        {
            ["wsbuddy-app-win-x64-v0.2.0.zip"] = ZipExeInspector.ListEntries(new MemoryStream(app)),
            ["wsbuddy-cli-win-x64-v0.2.0.zip"] = ZipExeInspector.ListEntries(new MemoryStream(cli))
        };
        var filter = new WindowsAssetFilter(reader);
        var pick = await filter.PickAsync(
        [
            Asset("wsbuddy-cli-win-x64-v0.2.0.zip", 32_000_000),
            Asset("wsbuddy.0.2.0.nupkg", 4000),
            Asset("wsbuddy-app-win-x64-v0.2.0.zip", 44_000_000)
        ]);
        Assert.NotNull(pick);
        Assert.Equal("wsbuddy-app-win-x64-v0.2.0.zip", pick!.Asset.Name);
    }

    [Fact]
    public async Task NupkgAndJsAssets_NeverQualify()
    {
        var filter = new WindowsAssetFilter(new MapZipReader());
        var pick = await filter.PickAsync(
        [
            Asset("main.js", 80),
            Asset("manifest.json", 40),
            Asset("styles.css", 20),
            Asset("wsbuddy.0.2.0.nupkg", 4000)
        ]);
        Assert.Null(pick);
    }

    [Fact]
    public async Task ZipballIsNotAnAsset_EmptyAssetsStayEmpty()
    {
        // GitHub latest-release zipball_url is not in assets[]. voltdesk is the live example.
        var filter = new WindowsAssetFilter(new MapZipReader());
        var pick = await filter.PickAsync([]);
        Assert.Null(pick);
    }

    private static ReleaseAsset Asset(string name, long size) => new()
    {
        Name = name,
        BrowserDownloadUrl = "https://example.test/" + name,
        Size = size
    };

    private sealed class MapZipReader : Dictionary<string, IReadOnlyList<string>>, IZipEntryReader
    {
        public Task<IReadOnlyList<string>?> TryListEntriesAsync(ReleaseAsset asset, CancellationToken cancellationToken)
        {
            if (TryGetValue(asset.Name, out var names))
                return Task.FromResult<IReadOnlyList<string>?>(names);
            return Task.FromResult<IReadOnlyList<string>?>(null);
        }
    }
}
