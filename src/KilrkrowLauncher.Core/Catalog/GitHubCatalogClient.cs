using KilrkrowLauncher.Http;
using KilrkrowLauncher.Models;

namespace KilrkrowLauncher.Catalog;

public sealed class GitHubCatalogClient
{
    public const string DefaultOwner = "kilrkrow";
    public const string SelfRepo = "kilrkrow-launcher";

    private readonly GitHubHttpClient _api;
    private readonly WindowsAssetFilter _filter;

    public GitHubCatalogClient(GitHubHttpClient api, WindowsAssetFilter filter)
    {
        _api = api;
        _filter = filter;
    }

    public static GitHubCatalogClient Create(HttpClient http, Func<string?>? tokenProvider = null, string? cacheDirectory = null)
    {
        DiskEtagCache? etag = null;
        IZipEntryReader zip = new HttpZipEntryReader(http);
        if (!string.IsNullOrWhiteSpace(cacheDirectory))
        {
            etag = new DiskEtagCache(Path.Combine(cacheDirectory, "etag"));
            zip = new CachingZipEntryReader(zip, Path.Combine(cacheDirectory, "zip-index"));
        }

        var api = new GitHubHttpClient(http, tokenProvider, etag);
        return new GitHubCatalogClient(api, new WindowsAssetFilter(zip));
    }

    /// <summary>
    /// Latest Windows asset for this launcher repo. Used for the manifest
    /// <c>launcher</c> field; SelfRepo is not skipped here.
    /// </summary>
    public async Task<CatalogLauncherInfo?> LoadSelfLauncherAsync(
        string owner = DefaultOwner,
        CancellationToken cancellationToken = default)
    {
        GitHubReleaseDto release;
        try
        {
            release = await _api.GetJsonAsync<GitHubReleaseDto>(
                "repos/" + owner + "/" + SelfRepo + "/releases/latest",
                cancellationToken).ConfigureAwait(false);
        }
        catch (GitHubApiException ex) when (ex.StatusCode == 404)
        {
            return null;
        }

        var assets = (release.Assets ?? []).Select(a => new ReleaseAsset
        {
            Name = a.Name,
            BrowserDownloadUrl = a.BrowserDownloadUrl,
            Size = a.Size,
            ContentType = a.ContentType
        }).ToArray();

        var pick = await _filter.PickAsync(assets, cancellationToken).ConfigureAwait(false);
        if (pick is null)
            return null;

        return new CatalogLauncherInfo
        {
            Tag = release.TagName,
            AssetUrl = pick.Asset.BrowserDownloadUrl,
            Size = pick.Asset.Size
        };
    }

    public async Task<IReadOnlyList<CatalogTool>> LoadPublicWindowsToolsAsync(
        string owner = DefaultOwner,
        CancellationToken cancellationToken = default)
    {
        var repos = await ListPublicReposAsync(owner, cancellationToken).ConfigureAwait(false);
        var tools = new List<CatalogTool>();

        foreach (var repo in repos)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (repo.Private || repo.Fork || repo.Archived)
                continue;
            if (string.Equals(repo.Name, SelfRepo, StringComparison.OrdinalIgnoreCase))
                continue;

            GitHubReleaseDto release;
            try
            {
                release = await _api.GetJsonAsync<GitHubReleaseDto>(
                    "repos/" + owner + "/" + repo.Name + "/releases/latest",
                    cancellationToken).ConfigureAwait(false);
            }
            catch (GitHubApiException ex) when (ex.StatusCode == 404)
            {
                continue;
            }

            var assets = (release.Assets ?? []).Select(a => new ReleaseAsset
            {
                Name = a.Name,
                BrowserDownloadUrl = a.BrowserDownloadUrl,
                Size = a.Size,
                ContentType = a.ContentType
            }).ToArray();

            var pick = await _filter.PickAsync(assets, cancellationToken).ConfigureAwait(false);
            if (pick is null)
                continue;

            tools.Add(new CatalogTool
            {
                Owner = owner,
                Repo = repo.Name,
                DisplayName = Humanize(repo.Name),
                Description = string.IsNullOrWhiteSpace(repo.Description) ? null : repo.Description.Trim(),
                TagName = release.TagName,
                HtmlUrl = string.IsNullOrWhiteSpace(release.HtmlUrl) ? repo.HtmlUrl : release.HtmlUrl,
                WindowsAsset = pick
            });
        }

        return tools
            .OrderBy(t => t.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private async Task<List<GitHubRepoDto>> ListPublicReposAsync(string owner, CancellationToken cancellationToken)
    {
        var all = new List<GitHubRepoDto>();
        var page = 1;
        while (page <= 10)
        {
            var batch = await _api.GetJsonAsync<List<GitHubRepoDto>>(
                "users/" + Uri.EscapeDataString(owner) + "/repos?type=public&per_page=100&page=" + page,
                cancellationToken).ConfigureAwait(false);
            if (batch.Count == 0)
                break;
            all.AddRange(batch);
            if (batch.Count < 100)
                break;
            page++;
        }

        return all;
    }

    internal static string Humanize(string repo)
    {
        if (string.IsNullOrWhiteSpace(repo))
            return repo;
        var parts = repo.Replace('_', '-').Split('-', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" ", parts.Select(p =>
            p.Length <= 2 ? p.ToUpperInvariant() : char.ToUpperInvariant(p[0]) + p[1..]));
    }
}
