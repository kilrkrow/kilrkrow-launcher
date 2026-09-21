using KilrkrowLauncher.Http;
using KilrkrowLauncher.Models;

namespace KilrkrowLauncher.Catalog;

/// <summary>
/// Lists zip entry names via HTTP Range on the central directory. If Range is
/// unavailable and the file is larger than <see cref="FullDownloadLimitBytes"/>,
/// returns null so the filter can refuse the asset (no source-zip false positives).
/// </summary>
public sealed class HttpZipEntryReader : IZipEntryReader
{
    public const int TailBytes = 262144;
    public const long FullDownloadLimitBytes = 8 * 1024 * 1024;

    private readonly HttpClient _http;

    public HttpZipEntryReader(HttpClient http)
    {
        _http = http;
    }

    public async Task<IReadOnlyList<string>?> TryListEntriesAsync(ReleaseAsset asset, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(asset.BrowserDownloadUrl))
            return null;

        try
        {
            var length = asset.Size > 0 ? asset.Size : await TryHeadLengthAsync(asset.BrowserDownloadUrl, cancellationToken).ConfigureAwait(false);
            if (length > 0)
            {
                var fromRange = await TryListViaRangeAsync(asset.BrowserDownloadUrl, length, cancellationToken).ConfigureAwait(false);
                if (fromRange is not null)
                    return fromRange;
            }

            if (length > 0 && length <= FullDownloadLimitBytes)
                return await ListViaFullDownloadAsync(asset.BrowserDownloadUrl, cancellationToken).ConfigureAwait(false);

            if (length <= 0 && asset.Size <= FullDownloadLimitBytes)
                return await ListViaFullDownloadAsync(asset.BrowserDownloadUrl, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }

        return null;
    }

    private async Task<long> TryHeadLengthAsync(string url, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Head, url);
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            return 0;
        return response.Content.Headers.ContentLength ?? 0;
    }

    private async Task<IReadOnlyList<string>?> TryListViaRangeAsync(string url, long length, CancellationToken cancellationToken)
    {
        var start = Math.Max(0, length - TailBytes);
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(start, length - 1);
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            return null;

        var tail = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        if (!ZipCentralDirectory.TryLocate(tail, length, start, out var locator))
            return null;

        byte[] cd;
        var cdInTail = locator.CentralDirectoryOffset - start;
        if (cdInTail >= 0 && cdInTail + locator.CentralDirectorySize <= tail.Length)
        {
            cd = tail.AsSpan((int)cdInTail, locator.CentralDirectorySize).ToArray();
        }
        else
        {
            using var cdRequest = new HttpRequestMessage(HttpMethod.Get, url);
            var end = locator.CentralDirectoryOffset + locator.CentralDirectorySize - 1;
            cdRequest.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(locator.CentralDirectoryOffset, end);
            using var cdResponse = await _http.SendAsync(cdRequest, cancellationToken).ConfigureAwait(false);
            if (!cdResponse.IsSuccessStatusCode)
                return null;
            cd = await cdResponse.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        }

        return ZipCentralDirectory.ParseNames(cd);
    }

    private async Task<IReadOnlyList<string>?> ListViaFullDownloadAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync(url, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            return null;
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return ZipExeInspector.ListEntries(stream);
    }
}
