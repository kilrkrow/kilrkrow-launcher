using KilrkrowLauncher.Models;

namespace KilrkrowLauncher.Detection;

public sealed class InstallDetector
{
    public const string PortableRootName = "KilrkrowLauncher";

    private readonly IFileProbe _files;
    private readonly ISpecialFolders _folders;
    private readonly IStartMenuProbe _startMenu;
    private readonly IUninstallRegistryProbe _registry;

    public InstallDetector(
        IFileProbe files,
        ISpecialFolders folders,
        IStartMenuProbe startMenu,
        IUninstallRegistryProbe registry)
    {
        _files = files;
        _folders = folders;
        _startMenu = startMenu;
        _registry = registry;
    }

    public static string PortableAppsRoot(ISpecialFolders folders)
        => Path.Combine(folders.LocalAppData, PortableRootName, "apps");

    public static string PortableRepoDir(ISpecialFolders folders, string repo)
        => Path.Combine(PortableAppsRoot(folders), repo);

    public InstallLookup Detect(CatalogTool tool)
    {
        var hints = AssetNameHints.From(tool);
        var preferredExes = tool.WindowsAsset.ExeEntryNames
            .Select(Path.GetFileName)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Cast<string>()
            .ToArray();

        var portable = FindPreferredExe(PortableRepoDir(_folders, tool.Repo), tool, preferredExes);
        if (portable is not null)
            return new InstallLookup { LaunchPath = portable, DetectedBy = "portable" };

        foreach (var root in WellKnownRoots(tool, hints))
        {
            var hit = FindPreferredExe(root, tool, preferredExes);
            if (hit is not null)
                return new InstallLookup { LaunchPath = hit, DetectedBy = "well-known" };
        }

        foreach (var record in _registry.Find(hints))
        {
            var fromIcon = NormalizeIconPath(record.DisplayIcon);
            if (fromIcon is not null && _files.FileExists(fromIcon))
                return new InstallLookup { LaunchPath = fromIcon, DetectedBy = "registry" };

            if (!string.IsNullOrWhiteSpace(record.InstallLocation))
            {
                var hit = FindPreferredExe(record.InstallLocation, tool, preferredExes);
                if (hit is not null)
                    return new InstallLookup { LaunchPath = hit, DetectedBy = "registry" };
            }
        }

        foreach (var shortcut in _startMenu.FindShortcuts(hints))
        {
            if (!string.IsNullOrWhiteSpace(shortcut.TargetPath) && _files.FileExists(shortcut.TargetPath)
                && shortcut.TargetPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                return new InstallLookup { LaunchPath = shortcut.TargetPath, DetectedBy = "start-menu" };
            }
        }

        return new InstallLookup();
    }

    private IEnumerable<string> WellKnownRoots(CatalogTool tool, IReadOnlyList<string> hints)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            tool.Repo,
            tool.DisplayName,
            tool.Repo.Replace("-", "")
        };
        foreach (var hint in hints.Take(6))
            names.Add(hint);

        foreach (var name in names)
        {
            if (string.IsNullOrWhiteSpace(name))
                continue;
            yield return Path.Combine(_folders.LocalAppData, name);
            yield return Path.Combine(_folders.LocalAppData, "Programs", name);
            yield return Path.Combine(_folders.ProgramFiles, name);
            if (!string.IsNullOrWhiteSpace(_folders.ProgramFilesX86))
                yield return Path.Combine(_folders.ProgramFilesX86, name);
        }
    }

    private string? FindPreferredExe(string directory, CatalogTool tool, IReadOnlyList<string> preferredExes)
    {
        if (string.IsNullOrWhiteSpace(directory))
            return null;

        var matches = _files.EnumerateFiles(directory, "*.exe", SearchOption.AllDirectories);
        return PrimaryExePicker.Pick(
            matches,
            tool.Repo,
            tool.DisplayName,
            preferredExes,
            _files.FileLength);
    }

    private static string? NormalizeIconPath(string? displayIcon)
    {
        if (string.IsNullOrWhiteSpace(displayIcon))
            return null;
        var path = displayIcon.Trim().Trim('"');
        var comma = path.LastIndexOf(',');
        if (comma > 2 && path.AsSpan(comma + 1).Trim().ToString().All(c => c is '-' or '+' || char.IsDigit(c)))
            path = path[..comma];
        return path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? path : null;
    }
}
