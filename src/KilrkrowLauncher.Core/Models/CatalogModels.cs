namespace KilrkrowLauncher.Models;

public sealed class ReleaseAsset
{
    public required string Name { get; init; }
    public required string BrowserDownloadUrl { get; init; }
    public long Size { get; init; }
    public string? ContentType { get; init; }
}

public sealed class WindowsAssetPick
{
    public required ReleaseAsset Asset { get; init; }
    public required IReadOnlyList<string> ExeEntryNames { get; init; }
}

public sealed class CatalogTool
{
    public required string Owner { get; init; }
    public required string Repo { get; init; }
    public required string DisplayName { get; init; }
    public string? Description { get; init; }
    public required string TagName { get; init; }
    public required string HtmlUrl { get; init; }
    public required WindowsAssetPick WindowsAsset { get; init; }
}

public enum ToolInstallState
{
    Missing,
    Installed,
    Running,
    Error
}

public sealed class InstallLookup
{
    public bool IsInstalled => !string.IsNullOrWhiteSpace(LaunchPath);
    public string? LaunchPath { get; init; }
    public string? DetectedBy { get; init; }
}

public sealed class UninstallRecord
{
    public required string DisplayName { get; init; }
    public string? InstallLocation { get; init; }
    public string? DisplayIcon { get; init; }
}

public sealed class ShortcutHit
{
    public required string LinkPath { get; init; }
    public required string LinkName { get; init; }
    public string? TargetPath { get; init; }
}
