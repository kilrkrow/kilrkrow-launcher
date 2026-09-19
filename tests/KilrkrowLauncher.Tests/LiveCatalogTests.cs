using KilrkrowLauncher.Catalog;

namespace KilrkrowLauncher.Tests;

public sealed class LiveCatalogTests
{
    [Fact]
    public async Task PublicKilrkrowCatalog_IncludesAtLeastOneWindowsRelease()
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        var client = GitHubCatalogClient.Create(http);

        IReadOnlyList<KilrkrowLauncher.Models.CatalogTool> tools;
        try
        {
            tools = await client.LoadPublicWindowsToolsAsync();
        }
        catch (KilrkrowLauncher.Http.GitHubRateLimitException)
        {
            return; // anonymous 60/hr budget; fixture tests already cover the filter
        }

        Assert.NotEmpty(tools);
        Assert.Contains(tools, t => t.Repo.Equals("win-service-buddy", StringComparison.OrdinalIgnoreCase));
        Assert.All(tools, t =>
        {
            Assert.False(string.IsNullOrWhiteSpace(t.WindowsAsset.Asset.Name));
            Assert.True(
                t.WindowsAsset.Asset.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                || t.WindowsAsset.Asset.Name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase)
                || t.WindowsAsset.ExeEntryNames.Count > 0);
        });
    }
}
