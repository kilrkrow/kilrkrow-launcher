using System.Net.Http.Headers;
using System.Text.Json;

namespace KilrkrowLauncher.Http;

public sealed class GitHubHttpClient
{
    public const string DefaultUserAgent = "kilrkrow-launcher";
    public const string ApiVersion = "2022-11-28";

    private readonly HttpClient _http;
    private readonly Func<string?> _tokenProvider;
    private readonly DiskEtagCache? _etagCache;

    public GitHubHttpClient(HttpClient http, Func<string?>? tokenProvider = null, DiskEtagCache? etagCache = null)
    {
        _http = http;
        _tokenProvider = tokenProvider ?? (() => null);
        _etagCache = etagCache;
        if (_http.BaseAddress is null)
            _http.BaseAddress = new Uri("https://api.github.com/");
        if (!_http.DefaultRequestHeaders.UserAgent.Any())
            _http.DefaultRequestHeaders.UserAgent.ParseAdd(DefaultUserAgent);
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        if (!_http.DefaultRequestHeaders.Contains("X-GitHub-Api-Version"))
            _http.DefaultRequestHeaders.TryAddWithoutValidation("X-GitHub-Api-Version", ApiVersion);
    }

    public async Task<T> GetJsonAsync<T>(string relativeOrAbsolute, CancellationToken cancellationToken)
    {
        var uri = Resolve(relativeOrAbsolute);
        var url = uri.ToString();
        string? cachedEtag = null;
        string? cachedBody = null;
        _etagCache?.TryGet(url, out cachedEtag, out cachedBody);

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        AttachAuth(request);
        if (!string.IsNullOrWhiteSpace(cachedEtag))
        {
            try
            {
                request.Headers.IfNoneMatch.Add(EntityTagHeaderValue.Parse(cachedEtag));
            }
            catch
            {
                // ignore malformed cache etag
            }
        }

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == System.Net.HttpStatusCode.NotModified && cachedBody is not null)
        {
            var cached = JsonSerializer.Deserialize<T>(cachedBody, GitHubJson.Options);
            if (cached is null)
                throw new GitHubApiException(304, "GitHub 304 with empty cached body.");
            return cached;
        }

        await EnsureSuccessAsync(response).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var etag = response.Headers.ETag?.ToString();
        if (_etagCache is not null && !string.IsNullOrWhiteSpace(body))
            _etagCache.Set(url, etag, body);

        var value = JsonSerializer.Deserialize<T>(body, GitHubJson.Options);
        if (value is null)
            throw new GitHubApiException((int)response.StatusCode, "GitHub returned an empty JSON body.");
        return value;
    }

    public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string relativeOrAbsolute, CancellationToken cancellationToken, HttpCompletionOption completion = HttpCompletionOption.ResponseContentRead)
    {
        var uri = Resolve(relativeOrAbsolute);
        using var request = new HttpRequestMessage(method, uri);
        AttachAuth(request);
        return await _http.SendAsync(request, completion, cancellationToken).ConfigureAwait(false);
    }

    private Uri Resolve(string relativeOrAbsolute)
        => Uri.TryCreate(relativeOrAbsolute, UriKind.Absolute, out var abs)
            ? abs
            : new Uri(_http.BaseAddress ?? new Uri("https://api.github.com/"), relativeOrAbsolute);

    private void AttachAuth(HttpRequestMessage request)
    {
        var token = _tokenProvider();
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Trim());
    }

    public static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
            return;

        var status = (int)response.StatusCode;
        if (status is 403 or 429)
        {
            DateTimeOffset? reset = null;
            if (response.Headers.RetryAfter?.Delta is { } delta)
                reset = DateTimeOffset.UtcNow + delta;
            else if (response.Headers.RetryAfter?.Date is { } date)
                reset = date;
            else if (response.Headers.TryGetValues("X-RateLimit-Reset", out var values)
                     && long.TryParse(values.FirstOrDefault(), out var epoch))
                reset = DateTimeOffset.FromUnixTimeSeconds(epoch);

            throw new GitHubRateLimitException(reset, "GitHub API rate limit or secondary limit.");
        }

        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        var snippet = body.Length > 180 ? body[..180] + "..." : body;
        throw new GitHubApiException(status, "GitHub API " + status + (string.IsNullOrWhiteSpace(snippet) ? "" : ": " + snippet));
    }
}

internal static class GitHubJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };
}
