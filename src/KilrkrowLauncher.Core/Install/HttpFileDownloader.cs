namespace KilrkrowLauncher.Install;

public sealed class HttpFileDownloader : IHttpDownloader
{
    private readonly HttpClient _http;

    public HttpFileDownloader(HttpClient http)
    {
        _http = http;
    }

    public async Task DownloadAsync(
        string url,
        string destinationPath,
        IProgress<InstallProgress>? progress,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? ".");
        using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var total = response.Content.Headers.ContentLength;

        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var output = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
        var buffer = new byte[81920];
        long received = 0;
        while (true)
        {
            var read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
                break;
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            received += read;
            var percent = total is > 0 ? (int)Math.Min(99, received * 100 / total.Value) : 0;
            progress?.Report(new InstallProgress
            {
                Phase = "Downloading...",
                BytesReceived = received,
                TotalBytes = total,
                Percent = percent
            });
        }

        progress?.Report(new InstallProgress
        {
            Phase = "Downloaded.",
            BytesReceived = received,
            TotalBytes = total ?? received,
            Percent = 100
        });
    }
}
