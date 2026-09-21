using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using KilrkrowLauncher.Catalog;
using KilrkrowLauncher.Install;

namespace KilrkrowLauncher.Native;

internal sealed class LauncherSelfUpdater : ILauncherSelfUpdater
{
    private readonly IHttpDownloader _downloader;
    private readonly IInstallerRunner _runner;

    public LauncherSelfUpdater(IHttpDownloader downloader, IInstallerRunner runner)
    {
        _downloader = downloader;
        _runner = runner;
    }

    public async Task UpdateAsync(CatalogLauncherInfo info, IProgress<InstallProgress>? progress, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(info.AssetUrl))
            throw new InvalidOperationException("Launcher update has no asset URL.");

        var name = GuessFileName(info.AssetUrl);
        var staging = Path.Combine(Path.GetTempPath(), "KilrkrowLauncher", "self-update");
        Directory.CreateDirectory(staging);
        var dest = Path.Combine(staging, name);
        await _downloader.DownloadAsync(info.AssetUrl, dest, progress, cancellationToken).ConfigureAwait(false);

        if (name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase))
        {
            await _runner.RunVisibleAsync("msiexec.exe", "/i \"" + dest + "\"", cancellationToken).ConfigureAwait(false);
            return;
        }

        var current = Environment.ProcessPath
                      ?? Path.Combine(AppContext.BaseDirectory, "KilrkrowLauncher.exe");
        string replacement = dest;
        if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            var extract = Path.Combine(staging, "extract");
            if (Directory.Exists(extract))
                Directory.Delete(extract, recursive: true);
            ZipFile.ExtractToDirectory(dest, extract);
            replacement = Directory.EnumerateFiles(extract, "KilrkrowLauncher.exe", SearchOption.AllDirectories).FirstOrDefault()
                          ?? Directory.EnumerateFiles(extract, "*.exe", SearchOption.AllDirectories).FirstOrDefault()
                          ?? throw new InvalidOperationException("Update zip has no exe.");
        }

        var cmd = Path.Combine(staging, "swap.cmd");
        var pid = Environment.ProcessId;
        File.WriteAllText(cmd,
            "@echo off" + Environment.NewLine
            + ":wait" + Environment.NewLine
            + "timeout /t 1 /nobreak >nul" + Environment.NewLine
            + "tasklist /FI \"PID eq " + pid + "\" | find \"" + pid + "\" >nul && goto wait" + Environment.NewLine
            + "copy /y \"" + replacement + "\" \"" + current + "\"" + Environment.NewLine
            + "start \"\" \"" + current + "\"" + Environment.NewLine);
        Process.Start(new ProcessStartInfo
        {
            FileName = cmd,
            UseShellExecute = true,
            WorkingDirectory = staging
        });
    }

    private static string GuessFileName(string url)
    {
        var name = Path.GetFileName(new Uri(url).AbsolutePath);
        return string.IsNullOrWhiteSpace(name) ? "launcher-update.bin" : name;
    }
}
