using KilrkrowLauncher.Catalog;

namespace KilrkrowLauncher.Tests;

public sealed class LiveCatalogTests
{
    [Fact]
    public async Task ManifestUrl_DeserializesWhenPublished()
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        using var request = new HttpRequestMessage(HttpMethod.Get, ManifestCatalogClient.DefaultManifestUrl);
        request.Headers.UserAgent.ParseAdd("kilrkrow-launcher-tests");
        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request);
        }
        catch (HttpRequestException)
        {
            return;
        }

        if (response.StatusCode is System.Net.HttpStatusCode.NotFound or System.Net.HttpStatusCode.Forbidden)
            return;

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        var manifest = CatalogJson.Parse(json);
        var tools = manifest.ToCatalogTools();
        Assert.NotEmpty(tools);
        Assert.All(tools, t =>
        {
            Assert.False(string.IsNullOrWhiteSpace(t.WindowsAsset.Asset.Name));
            Assert.False(string.IsNullOrWhiteSpace(t.WindowsAsset.Asset.BrowserDownloadUrl));
        });
    }
}
