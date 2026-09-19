using System.Net;
using System.Text;
using KilrkrowLauncher.Catalog;
using KilrkrowLauncher.Http;

namespace KilrkrowLauncher.Tests;

public sealed class GitHubCatalogClientTests
{
    [Fact]
    public async Task SkipsPrivateForksArchived_NoRelease_EmptyAssets_SourceZip_AndSelfRepo()
    {
        var appZip = FixtureZips.WithExe("WinServiceBuddy.App.exe");
        var sourceZip = FixtureZips.SourceOnly();
        var handler = new ScriptedHandler
        {
            ["https://api.github.com/users/kilrkrow/repos?type=public&per_page=100&page=1"] = ReposJson(),
            ["https://api.github.com/repos/kilrkrow/sideclip/releases/latest"] = NotFound(),
            ["https://api.github.com/repos/kilrkrow/voltdesk/releases/latest"] = ReleaseJson("v1.1.0", "[]"),
            ["https://api.github.com/repos/kilrkrow/obsidian-bible-verse/releases/latest"] =
                ReleaseJson("1.9.2", """[{"name":"main.js","browser_download_url":"https://example.test/main.js","size":10}]"""),
            ["https://api.github.com/repos/kilrkrow/sourcey/releases/latest"] =
                ReleaseJson("v1", """[{"name":"source-win-x64.zip","browser_download_url":"https://example.test/source-win-x64.zip","size":800}]"""),
            ["https://example.test/source-win-x64.zip"] = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(sourceZip)
            },
            ["https://api.github.com/repos/kilrkrow/win-service-buddy/releases/latest"] =
                ReleaseJson("v0.2.0", """[{"name":"wsbuddy-app-win-x64-v0.2.0.zip","browser_download_url":"https://example.test/app.zip","size":1200},{"name":"wsbuddy.0.2.0.nupkg","browser_download_url":"https://example.test/p.nupkg","size":40}]"""),
            ["https://example.test/app.zip"] = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(appZip)
            }
        };

        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") };
        var client = GitHubCatalogClient.Create(http);
        var tools = await client.LoadPublicWindowsToolsAsync();

        Assert.Single(tools);
        Assert.Equal("win-service-buddy", tools[0].Repo);
        Assert.Equal("v0.2.0", tools[0].TagName);
        Assert.Contains(tools[0].WindowsAsset.ExeEntryNames, n => n.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RateLimit_SurfacesUserMessage()
    {
        var handler = new ScriptedHandler
        {
            ["https://api.github.com/users/kilrkrow/repos?type=public&per_page=100&page=1"] =
                new HttpResponseMessage((HttpStatusCode)429)
                {
                    Content = new StringContent("{\"message\":\"rate limit\"}", Encoding.UTF8, "application/json")
                }
        };
        handler.Messages["https://api.github.com/users/kilrkrow/repos?type=public&per_page=100&page=1"]
            .Headers.TryAddWithoutValidation("Retry-After", "60");

        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") };
        var client = GitHubCatalogClient.Create(http);
        var ex = await Assert.ThrowsAsync<GitHubRateLimitException>(() => client.LoadPublicWindowsToolsAsync());
        Assert.Contains("rate limit", ex.UserMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("token", ex.UserMessage, StringComparison.OrdinalIgnoreCase);
    }

    private static HttpResponseMessage ReposJson()
    {
        const string json = """
            [
              {"name":"kilrkrow-launcher","private":false,"fork":false,"archived":false,"html_url":"https://github.com/kilrkrow/kilrkrow-launcher"},
              {"name":"secret","private":true,"fork":false,"archived":false,"html_url":"https://github.com/kilrkrow/secret"},
              {"name":"forked","private":false,"fork":true,"archived":false,"html_url":"https://github.com/kilrkrow/forked"},
              {"name":"old","private":false,"fork":false,"archived":true,"html_url":"https://github.com/kilrkrow/old"},
              {"name":"sideclip","private":false,"fork":false,"archived":false,"description":"clipboard","html_url":"https://github.com/kilrkrow/sideclip"},
              {"name":"voltdesk","private":false,"fork":false,"archived":false,"html_url":"https://github.com/kilrkrow/voltdesk"},
              {"name":"obsidian-bible-verse","private":false,"fork":false,"archived":false,"html_url":"https://github.com/kilrkrow/obsidian-bible-verse"},
              {"name":"sourcey","private":false,"fork":false,"archived":false,"html_url":"https://github.com/kilrkrow/sourcey"},
              {"name":"win-service-buddy","private":false,"fork":false,"archived":false,"description":"Windows services","html_url":"https://github.com/kilrkrow/win-service-buddy"}
            ]
            """;
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private static HttpResponseMessage ReleaseJson(string tag, string assetsJson)
    {
        var json = "{\"tag_name\":\"" + tag + "\",\"html_url\":\"https://github.com/x\",\"assets\":" + assetsJson + "}";
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private static HttpResponseMessage NotFound()
        => new(HttpStatusCode.NotFound) { Content = new StringContent("{\"message\":\"Not Found\"}", Encoding.UTF8, "application/json") };

    private sealed class ScriptedHandler : HttpMessageHandler
    {
        public Dictionary<string, HttpResponseMessage> Messages { get; } = new(StringComparer.Ordinal);

        public HttpResponseMessage this[string url]
        {
            set => Messages[url] = value;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var key = request.RequestUri!.ToString();
            if (!Messages.TryGetValue(key, out var response))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent("no script for " + key)
                });
            return Task.FromResult(Clone(response));
        }

        private static HttpResponseMessage Clone(HttpResponseMessage source)
        {
            var copy = new HttpResponseMessage(source.StatusCode);
            foreach (var header in source.Headers)
                copy.Headers.TryAddWithoutValidation(header.Key, header.Value);
            if (source.Content is ByteArrayContent bytes)
            {
                var data = bytes.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                copy.Content = new ByteArrayContent(data);
            }
            else
            {
                var text = source.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                copy.Content = new StringContent(text, Encoding.UTF8, "application/json");
            }

            return copy;
        }
    }
}
