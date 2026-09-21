using System.Net;
using System.Text;
using KilrkrowLauncher.Catalog;
using KilrkrowLauncher.Http;

namespace KilrkrowLauncher.Tests;

public sealed class GitHubCatalogClientTests
{
    [Fact]
    public async Task WalksRecentReleases_OlderAssetWins_NoReleaseExcluded_LatestPreferred()
    {
        var appZip = FixtureZips.WithExe("WinServiceBuddy.App.exe");
        var sourceZip = FixtureZips.SourceOnly();
        var handler = new ScriptedHandler
        {
            ["https://api.github.com/users/kilrkrow/repos?type=public&per_page=100&page=1"] = ReposJson(),
            ["https://api.github.com/repos/kilrkrow/sideclip/releases?per_page=30"] = EmptyList(),
            ["https://api.github.com/repos/kilrkrow/netpulse/releases?per_page=30"] = EmptyList(),
            ["https://api.github.com/repos/kilrkrow/voltdesk/releases?per_page=30"] = ReleasesJson(
                """
                [
                  {"tag_name":"v1.1.0","html_url":"https://github.com/kilrkrow/voltdesk/releases/tag/v1.1.0","draft":false,"assets":[]},
                  {"tag_name":"v1.0.3","html_url":"https://github.com/kilrkrow/voltdesk/releases/tag/v1.0.3","draft":false,"assets":[]},
                  {"tag_name":"v1.0.2","html_url":"https://github.com/kilrkrow/voltdesk/releases/tag/v1.0.2","draft":false,"assets":[{"name":"VoltDesk.exe","browser_download_url":"https://example.test/VoltDesk.exe","size":4096}]}
                ]
                """),
            ["https://api.github.com/repos/kilrkrow/obsidian-bible-verse/releases?per_page=30"] =
                ReleaseList("1.9.2", """[{"name":"main.js","browser_download_url":"https://example.test/main.js","size":10}]"""),
            ["https://api.github.com/repos/kilrkrow/sourcey/releases?per_page=30"] =
                ReleaseList("v1", """[{"name":"source-win-x64.zip","browser_download_url":"https://example.test/source-win-x64.zip","size":800}]"""),
            ["https://example.test/source-win-x64.zip"] = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(sourceZip)
            },
            ["https://api.github.com/repos/kilrkrow/win-service-buddy/releases?per_page=30"] =
                ReleasesJson(
                    """
                    [
                      {"tag_name":"v0.2.0","html_url":"https://github.com/kilrkrow/win-service-buddy/releases/tag/v0.2.0","draft":false,"assets":[{"name":"wsbuddy-app-win-x64-v0.2.0.zip","browser_download_url":"https://example.test/app.zip","size":1200},{"name":"wsbuddy.0.2.0.nupkg","browser_download_url":"https://example.test/p.nupkg","size":40}]},
                      {"tag_name":"v0.1.0","html_url":"https://github.com/kilrkrow/win-service-buddy/releases/tag/v0.1.0","draft":false,"assets":[{"name":"old.exe","browser_download_url":"https://example.test/old.exe","size":10}]}
                    ]
                    """),
            ["https://example.test/app.zip"] = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(appZip)
            }
        };

        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") };
        var client = GitHubCatalogClient.Create(http);
        var tools = await client.LoadPublicWindowsToolsAsync();

        Assert.Equal(2, tools.Count);
        Assert.Equal("voltdesk", tools[0].Repo);
        Assert.Equal("v1.0.2", tools[0].TagName);
        Assert.Equal("VoltDesk.exe", tools[0].WindowsAsset.Asset.Name);
        Assert.Equal("win-service-buddy", tools[1].Repo);
        Assert.Equal("v0.2.0", tools[1].TagName);
        Assert.Contains(tools[1].WindowsAsset.ExeEntryNames, n => n.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SkipsDraftEvenWhenItHasWindowsAsset()
    {
        var handler = new ScriptedHandler
        {
            ["https://api.github.com/users/kilrkrow/repos?type=public&per_page=100&page=1"] =
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """[{"name":"drafty","private":false,"fork":false,"archived":false,"html_url":"https://github.com/kilrkrow/drafty"}]""",
                        Encoding.UTF8, "application/json")
                },
            ["https://api.github.com/repos/kilrkrow/drafty/releases?per_page=30"] = ReleasesJson(
                """
                [
                  {"tag_name":"v2.0.0","html_url":"https://github.com/x","draft":true,"assets":[{"name":"Draft.exe","browser_download_url":"https://example.test/Draft.exe","size":1}]},
                  {"tag_name":"v1.0.0","html_url":"https://github.com/x","draft":false,"assets":[{"name":"Ship.exe","browser_download_url":"https://example.test/Ship.exe","size":2}]}
                ]
                """)
        };

        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") };
        var client = GitHubCatalogClient.Create(http);
        var tools = await client.LoadPublicWindowsToolsAsync();
        Assert.Single(tools);
        Assert.Equal("v1.0.0", tools[0].TagName);
        Assert.Equal("Ship.exe", tools[0].WindowsAsset.Asset.Name);
    }

    [Fact]
    public async Task SelfLauncher_UsesNewestNonDraftWithWindowsAsset()
    {
        var handler = new ScriptedHandler
        {
            ["https://api.github.com/repos/kilrkrow/kilrkrow-launcher/releases?per_page=30"] = ReleasesJson(
                """
                [
                  {"tag_name":"v0.2.0","html_url":"https://github.com/x","draft":false,"assets":[]},
                  {"tag_name":"v0.1.0","html_url":"https://github.com/x","draft":false,"assets":[{"name":"KilrkrowLauncher.exe","browser_download_url":"https://example.test/KilrkrowLauncher.exe","size":99}]}
                ]
                """)
        };
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") };
        var client = GitHubCatalogClient.Create(http);
        var launcher = await client.LoadSelfLauncherAsync();
        Assert.NotNull(launcher);
        Assert.Equal("v0.1.0", launcher.Tag);
        Assert.Equal("https://example.test/KilrkrowLauncher.exe", launcher.AssetUrl);
        Assert.Equal(99, launcher.Size);
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
              {"name":"netpulse","private":false,"fork":false,"archived":false,"html_url":"https://github.com/kilrkrow/netpulse"},
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

    private static HttpResponseMessage ReleaseList(string tag, string assetsJson)
        => ReleasesJson("[{\"tag_name\":\"" + tag + "\",\"html_url\":\"https://github.com/x\",\"draft\":false,\"assets\":" + assetsJson + "}]");

    private static HttpResponseMessage ReleasesJson(string json)
        => new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static HttpResponseMessage EmptyList()
        => ReleasesJson("[]");

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
