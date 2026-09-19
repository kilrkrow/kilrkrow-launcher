using System.IO.Compression;
using KilrkrowLauncher.Catalog;
using KilrkrowLauncher.Detection;
using KilrkrowLauncher.Models;

namespace KilrkrowLauncher.Install;

public interface IHttpDownloader
{
    Task DownloadAsync(
        string url,
        string destinationPath,
        IProgress<InstallProgress>? progress,
        CancellationToken cancellationToken);
}

public interface IInstallerRunner
{
    /// <summary>
    /// Starts an installer with a visible process. Never sets Verb=runas.
    /// MSI/admin elevation is the installer's own UAC prompt.
    /// </summary>
    Task<int> RunVisibleAsync(string fileName, string? arguments, CancellationToken cancellationToken);
}

public sealed class GitHubReleaseInstallProvider : IInstallProvider
{
    public const string ProviderId = "github-release";

    private readonly IHttpDownloader _downloader;
    private readonly IInstallerRunner _runner;
    private readonly ISpecialFolders _folders;
    private readonly IFileProbe _files;

    public GitHubReleaseInstallProvider(
        IHttpDownloader downloader,
        IInstallerRunner runner,
        ISpecialFolders folders,
        IFileProbe files)
    {
        _downloader = downloader;
        _runner = runner;
        _folders = folders;
        _files = files;
    }

    public string Id => ProviderId;
    public string DisplayName => "GitHub release";

    public bool CanInstall(CatalogTool tool)
        => tool.WindowsAsset.Asset.BrowserDownloadUrl.Length > 0;

    public async Task<InstallResult> InstallAsync(
        CatalogTool tool,
        IProgress<InstallProgress>? progress,
        CancellationToken cancellationToken)
    {
        var asset = tool.WindowsAsset.Asset;
        var staging = Path.Combine(Path.GetTempPath(), PortableRootName(), tool.Repo);
        Directory.CreateDirectory(staging);
        var dest = Path.Combine(staging, asset.Name);

        try
        {
            progress?.Report(new InstallProgress { Phase = "Downloading...", Percent = 0, TotalBytes = asset.Size });
            await _downloader.DownloadAsync(asset.BrowserDownloadUrl, dest, progress, cancellationToken).ConfigureAwait(false);

            var name = asset.Name;
            if (name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase))
                return await RunMsiAsync(dest, progress, cancellationToken).ConfigureAwait(false);

            if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                if (LooksLikeInstaller(name))
                    return await RunSetupExeAsync(dest, progress, cancellationToken).ConfigureAwait(false);
                return StagePortableExe(tool, dest, progress);
            }

            if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                return ExtractZip(tool, dest, progress);

            return new InstallResult { Succeeded = false, Message = "Unsupported asset type." };
        }
        catch (OperationCanceledException)
        {
            TryDelete(dest);
            return new InstallResult { Succeeded = false, Cancelled = true, Message = "Cancelled." };
        }
        catch (Exception ex)
        {
            return new InstallResult { Succeeded = false, Message = ex.Message };
        }
    }

    private async Task<InstallResult> RunMsiAsync(string msiPath, IProgress<InstallProgress>? progress, CancellationToken cancellationToken)
    {
        progress?.Report(new InstallProgress { Phase = "Starting MSI (UAC if required)...", Percent = 100 });
        // Visible msiexec. No /qn /quiet /passive. No Verb=runas.
        var code = await _runner.RunVisibleAsync("msiexec.exe", "/i \"" + msiPath + "\"", cancellationToken).ConfigureAwait(false);
        if (code is 0 or 3010)
            return new InstallResult { Succeeded = true, Message = "MSI finished." };
        if (code == 1602)
            return new InstallResult { Succeeded = false, Cancelled = true, Message = "MSI cancelled." };
        return new InstallResult { Succeeded = false, Message = "MSI exited with code " + code + "." };
    }

    private async Task<InstallResult> RunSetupExeAsync(string exePath, IProgress<InstallProgress>? progress, CancellationToken cancellationToken)
    {
        progress?.Report(new InstallProgress { Phase = "Starting installer (UAC if required)...", Percent = 100 });
        var code = await _runner.RunVisibleAsync(exePath, arguments: null, cancellationToken).ConfigureAwait(false);
        if (code == 0)
            return new InstallResult { Succeeded = true, Message = "Installer finished." };
        return new InstallResult { Succeeded = false, Message = "Installer exited with code " + code + "." };
    }

    private InstallResult StagePortableExe(CatalogTool tool, string downloadedExe, IProgress<InstallProgress>? progress)
    {
        progress?.Report(new InstallProgress { Phase = "Copying portable exe...", Percent = 100 });
        var dir = Path.Combine(InstallDetector.PortableRepoDir(_folders, tool.Repo), tool.TagName);
        Directory.CreateDirectory(dir);
        var dest = Path.Combine(dir, Path.GetFileName(downloadedExe));
        File.Copy(downloadedExe, dest, overwrite: true);
        return new InstallResult { Succeeded = true, LaunchPath = dest, Message = "Portable exe staged." };
    }

    private InstallResult ExtractZip(CatalogTool tool, string zipPath, IProgress<InstallProgress>? progress)
    {
        progress?.Report(new InstallProgress { Phase = "Extracting...", Percent = 90 });
        using (var stream = File.OpenRead(zipPath))
        {
            var names = ZipExeInspector.ListEntries(stream);
            if (!ZipExeInspector.ContainsWindowsExe(names))
                return new InstallResult { Succeeded = false, Message = "Zip has no .exe (source-only archive)." };
        }

        var dir = Path.Combine(InstallDetector.PortableRepoDir(_folders, tool.Repo), tool.TagName);
        if (Directory.Exists(dir))
            Directory.Delete(dir, recursive: true);
        Directory.CreateDirectory(dir);
        ZipFile.ExtractToDirectory(zipPath, dir);

        var preferred = tool.WindowsAsset.ExeEntryNames.Select(Path.GetFileName).ToArray();
        var launch = Directory.EnumerateFiles(dir, "*.exe", SearchOption.AllDirectories)
            .FirstOrDefault(p => preferred.Any(n => string.Equals(n, Path.GetFileName(p), StringComparison.OrdinalIgnoreCase)))
            ?? Directory.EnumerateFiles(dir, "*.exe", SearchOption.AllDirectories).FirstOrDefault();

        return new InstallResult
        {
            Succeeded = launch is not null,
            LaunchPath = launch,
            Message = launch is null ? "Extracted but no exe found." : "Extracted portable build."
        };
    }

    internal static bool LooksLikeInstaller(string fileName)
    {
        var n = fileName.ToLowerInvariant();
        return n.Contains("setup") || n.Contains("install") || n.Contains("-msi");
    }

    internal static bool MsiArgumentsAreInteractive(string? arguments)
        => arguments is not null
           && arguments.Contains("/i", StringComparison.OrdinalIgnoreCase)
           && !arguments.Contains("/qn", StringComparison.OrdinalIgnoreCase)
           && !arguments.Contains("/quiet", StringComparison.OrdinalIgnoreCase)
           && !arguments.Contains("/passive", StringComparison.OrdinalIgnoreCase);

    private static string PortableRootName() => InstallDetector.PortableRootName;

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // best-effort cleanup after cancel
        }
    }
}
