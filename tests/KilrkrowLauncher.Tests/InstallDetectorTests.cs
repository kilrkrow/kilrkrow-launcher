using KilrkrowLauncher.Detection;
using KilrkrowLauncher.Models;

namespace KilrkrowLauncher.Tests;

public sealed class InstallDetectorTests
{
    [Fact]
    public void DetectsPortableStagedExe()
    {
        var folders = new FakeFolders();
        var files = new FakeFiles();
        var portable = Path.Combine(InstallDetector.PortableRepoDir(folders, "win-service-buddy"), "v0.2.0", "WinServiceBuddy.App.exe");
        files.AddFile(portable);

        var detector = new InstallDetector(files, folders, new FakeStartMenu(), new FakeRegistry());
        var hit = detector.Detect(Buddy());
        Assert.True(hit.IsInstalled);
        Assert.Equal(portable, hit.LaunchPath);
        Assert.Equal("portable", hit.DetectedBy);
    }

    [Fact]
    public void DetectsWellKnownProgramFilesPath()
    {
        var folders = new FakeFolders();
        var files = new FakeFiles();
        var path = Path.Combine(folders.ProgramFiles, "WinServiceBuddy", "WinServiceBuddy.App.exe");
        files.AddFile(path);

        var detector = new InstallDetector(files, folders, new FakeStartMenu(), new FakeRegistry());
        var hit = detector.Detect(Buddy());
        Assert.True(hit.IsInstalled);
        Assert.Equal("well-known", hit.DetectedBy);
    }

    [Fact]
    public void DetectsUninstallRegistryIcon()
    {
        var folders = new FakeFolders();
        var files = new FakeFiles();
        var path = Path.Combine(folders.ProgramFiles, "Elsewhere", "WinServiceBuddy.App.exe");
        files.AddFile(path);

        var registry = new FakeRegistry
        {
            Records =
            [
                new UninstallRecord
                {
                    DisplayName = "Win Service Buddy",
                    DisplayIcon = path + ",0",
                    InstallLocation = Path.GetDirectoryName(path)
                }
            ]
        };

        var detector = new InstallDetector(files, folders, new FakeStartMenu(), registry);
        var hit = detector.Detect(Buddy());
        Assert.True(hit.IsInstalled);
        Assert.Equal(path, hit.LaunchPath);
        Assert.Equal("registry", hit.DetectedBy);
    }

    [Fact]
    public void DetectsStartMenuShortcut()
    {
        var folders = new FakeFolders();
        var files = new FakeFiles();
        var path = Path.Combine(folders.LocalAppData, "Programs", "Buddy", "WinServiceBuddy.App.exe");
        files.AddFile(path);

        var menu = new FakeStartMenu
        {
            Hits =
            [
                new ShortcutHit
                {
                    LinkPath = Path.Combine(folders.StartMenuProgramRoots[0], "Win Service Buddy.lnk"),
                    LinkName = "Win Service Buddy",
                    TargetPath = path
                }
            ]
        };

        var detector = new InstallDetector(files, folders, menu, new FakeRegistry());
        var hit = detector.Detect(Buddy());
        Assert.True(hit.IsInstalled);
        Assert.Equal("start-menu", hit.DetectedBy);
    }

    [Fact]
    public void Missing_WhenNothingMatches()
    {
        var detector = new InstallDetector(new FakeFiles(), new FakeFolders(), new FakeStartMenu(), new FakeRegistry());
        var hit = detector.Detect(Buddy());
        Assert.False(hit.IsInstalled);
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

    private sealed class FakeFolders : ISpecialFolders
    {
        public string LocalAppData { get; } = Path.Combine(Path.GetTempPath(), "kl-detect", "local");
        public string ProgramFiles { get; } = Path.Combine(Path.GetTempPath(), "kl-detect", "pf");
        public string ProgramFilesX86 { get; } = Path.Combine(Path.GetTempPath(), "kl-detect", "pf86");
        public IReadOnlyList<string> StartMenuProgramRoots { get; } =
        [
            Path.Combine(Path.GetTempPath(), "kl-detect", "sm")
        ];
    }

    private sealed class FakeFiles : IFileProbe
    {
        private readonly HashSet<string> _files = new(StringComparer.OrdinalIgnoreCase);

        public void AddFile(string path) => _files.Add(path);

        public bool FileExists(string path) => _files.Contains(path);

        public IEnumerable<string> EnumerateFiles(string directory, string searchPattern, SearchOption option)
        {
            return _files.Where(f =>
                f.StartsWith(directory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || string.Equals(Path.GetDirectoryName(f), directory, StringComparison.OrdinalIgnoreCase));
        }
    }

    private sealed class FakeStartMenu : IStartMenuProbe
    {
        public List<ShortcutHit> Hits { get; init; } = [];

        public IReadOnlyList<ShortcutHit> FindShortcuts(IReadOnlyList<string> nameHints)
            => Hits.Where(h => AssetNameHints.NameMatches(h.LinkName, nameHints)
                               || (h.TargetPath is not null && AssetNameHints.NameMatches(h.TargetPath, nameHints)))
                .ToArray();
    }

    private sealed class FakeRegistry : IUninstallRegistryProbe
    {
        public List<UninstallRecord> Records { get; init; } = [];

        public IReadOnlyList<UninstallRecord> Find(IReadOnlyList<string> nameHints)
            => Records.Where(r => AssetNameHints.NameMatches(r.DisplayName, nameHints)).ToArray();
    }
}
