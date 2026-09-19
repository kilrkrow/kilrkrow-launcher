using System.Net;
using System.Text;
using KilrkrowLauncher.Catalog;

namespace KilrkrowLauncher.Tests;

public sealed class ManifestCatalogClientTests
{
    [Fact]
    public async Task LoadAsync_WritesCache_And304UsesCache()
    {
        var dir = Path.Combine(Path.GetTempPath(), "kl-manifest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var cache = Path.Combine(dir, "catalog.json");
        var json = """
            {"generatedAt":"2026-09-19T08:00:00Z","tools":[{"owner":"kilrkrow","repo":"win-service-buddy","displayName":"Win Service Buddy","tagName":"v0.2.0","htmlUrl":"https://github.com/kilrkrow/win-service-buddy","asset":{"name":"app.zip","browserDownloadUrl":"https://example.test/app.zip","size":1,"kind":"zip"}}]}
            """;

        var first = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        first.Headers.ETag = new System.Net.Http.Headers.EntityTagHeaderValue("\"abc\"");

        var handler = new QueueHandler();
        handler.Enqueue(first);
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.NotModified));

        using var http = new HttpClient(handler);
        var client = new ManifestCatalogClient(http, cache, "https://example.test/catalog.json");

        Assert.Null(client.TryLoadCache());
        var loaded = await client.LoadAsync();
        Assert.False(loaded.FromCache);
        Assert.Single(loaded.Tools);
        Assert.Equal("win-service-buddy", loaded.Tools[0].Repo);

        var cached = client.TryLoadCache();
        Assert.NotNull(cached);

        var again = await client.LoadAsync();
        Assert.True(again.NotModified);
        Assert.Single(again.Tools);
    }

    private sealed class QueueHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _queue = new();
        public void Enqueue(HttpResponseMessage response) => _queue.Enqueue(response);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_queue.Dequeue());
    }
}
