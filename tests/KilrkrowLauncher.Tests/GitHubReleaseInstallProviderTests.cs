using KilrkrowLauncher.Detection;
using KilrkrowLauncher.Install;
using KilrkrowLauncher.Models;

namespace KilrkrowLauncher.Tests;

public sealed class GitHubReleaseInstallProviderTests
{
    [Fact]
    public async Task ZipExtractsPortableExe_AndCancelDeletesPartial()
    {
        var folders = new TempFolders();
        var provider = new GitHubReleaseInstallProvider(
            new MemoryDownloader { Bytes = FixtureZips.WithExe("WinServiceBuddy.App.exe") },
            new RecordingRunner(),
            folders,
            new PhysicalFileProbe());

        var result = await provider.InstallAsync(Buddy(), progress: null, CancellationToken.None);
        Assert.True(result.Succeeded);
        Assert.NotNull(result.LaunchPath);
        Assert.True(File.Exists(result.LaunchPath));
        Assert.Equal("WinServiceBuddy.App.exe", Path.GetFileName(result.LaunchPath));
    }

    [Fact]
    public async Task SourceOnlyZip_FailsWithoutFalseInstall()
    {
        var folders = new TempFolders();
        var provider = new GitHubReleaseInstallProvider(
            new MemoryDownloader { Bytes = FixtureZips.SourceOnly() },
            new RecordingRunner(),
            folders,
            new PhysicalFileProbe());

        var tool = Buddy();
        tool = new CatalogTool
        {
            Owner = tool.Owner,
            Repo = tool.Repo,
            DisplayName = tool.DisplayName,
            TagName = tool.TagName,
            HtmlUrl = tool.HtmlUrl,
            WindowsAsset = new WindowsAssetPick
            {
                Asset = new ReleaseAsset
                {
                    Name = "source.zip",
                    BrowserDownloadUrl = "https://example.test/source.zip",
                    Size = 10
                },
                ExeEntryNames = []
            }
        };

        var result = await provider.InstallAsync(tool, null, CancellationToken.None);
        Assert.False(result.Succeeded);
        Assert.Contains("no .exe", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CancelDuringDownload_IsCancelled()
    {
        using var cts = new CancellationTokenSource();
        var provider = new GitHubReleaseInstallProvider(
            new CancellingDownloader(cts),
            new RecordingRunner(),
            new TempFolders(),
            new PhysicalFileProbe());

        var result = await provider.InstallAsync(Buddy(), null, cts.Token);
        Assert.True(result.Cancelled);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Msi_UsesInteractiveMsiexec_NoSilentFlags()
    {
        var runner = new RecordingRunner { ExitCode = 0 };
        var provider = new GitHubReleaseInstallProvider(
            new MemoryDownloader { Bytes = [1, 2, 3] },
            runner,
            new TempFolders(),
            new PhysicalFileProbe());

        var tool = BuddyMsi();
        var result = await provider.InstallAsync(tool, null, CancellationToken.None);
        Assert.True(result.Succeeded);
        Assert.Equal("msiexec.exe", runner.LastFileName);
        Assert.True(GitHubReleaseInstallProvider.MsiArgumentsAreInteractive(runner.LastArguments));
    }

    [Fact]
    public void LooksLikeInstaller_ForSetupNamesOnly()
    {
        Assert.True(GitHubReleaseInstallProvider.LooksLikeInstaller("Sideclip-Setup.exe"));
        Assert.False(GitHubReleaseInstallProvider.LooksLikeInstaller("Sideclip.exe"));
    }

    private static CatalogTool Buddy() => new()
    {
        Owner = "kilrkrow",
        Repo = "win-service-buddy",
        DisplayName = "Win Service Buddy",
        TagName = "v0.2.0",
        HtmlUrl = "https://github.com/kilrkrow/win-service-buddy",
        WindowsAsset = new WindowsAssetPick
        {
            Asset = new ReleaseAsset
            {
                Name = "wsbuddy-app-win-x64-v0.2.0.zip",
                BrowserDownloadUrl = "https://example.test/app.zip",
                Size = 10
            },
            ExeEntryNames = ["WinServiceBuddy.App.exe"]
        }
    };

    private static CatalogTool BuddyMsi() => new()
    {
        Owner = "kilrkrow",
        Repo = "win-service-buddy",
        DisplayName = "Win Service Buddy",
        TagName = "v0.2.0",
        HtmlUrl = "https://github.com/kilrkrow/win-service-buddy",
        WindowsAsset = new WindowsAssetPick
        {
            Asset = new ReleaseAsset
            {
                Name = "buddy.msi",
                BrowserDownloadUrl = "https://example.test/buddy.msi",
                Size = 10
            },
            ExeEntryNames = []
        }
    };

    private sealed class TempFolders : ISpecialFolders
    {
        public string LocalAppData { get; } = Path.Combine(Path.GetTempPath(), "kl-install-" + Guid.NewGuid().ToString("N"));
        public string ProgramFiles { get; } = Path.Combine(Path.GetTempPath(), "kl-pf");
        public string ProgramFilesX86 { get; } = Path.Combine(Path.GetTempPath(), "kl-pf86");
        public IReadOnlyList<string> StartMenuProgramRoots { get; } = [Path.Combine(Path.GetTempPath(), "kl-sm")];
    }

    private sealed class MemoryDownloader : IHttpDownloader
    {
        public byte[] Bytes { get; init; } = [];

        public async Task DownloadAsync(string url, string destinationPath, IProgress<InstallProgress>? progress, CancellationToken cancellationToken)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            await File.WriteAllBytesAsync(destinationPath, Bytes, cancellationToken);
            progress?.Report(new InstallProgress { Phase = "Downloaded.", Percent = 100, BytesReceived = Bytes.Length, TotalBytes = Bytes.Length });
        }
    }

    private sealed class CancellingDownloader : IHttpDownloader
    {
        private readonly CancellationTokenSource _cts;
        public CancellingDownloader(CancellationTokenSource cts) => _cts = cts;

        public Task DownloadAsync(string url, string destinationPath, IProgress<InstallProgress>? progress, CancellationToken cancellationToken)
        {
            _cts.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingRunner : IInstallerRunner
    {
        public int ExitCode { get; init; }
        public string? LastFileName { get; private set; }
        public string? LastArguments { get; private set; }

        public Task<int> RunVisibleAsync(string fileName, string? arguments, CancellationToken cancellationToken)
        {
            LastFileName = fileName;
            LastArguments = arguments;
            return Task.FromResult(ExitCode);
        }
    }
}
