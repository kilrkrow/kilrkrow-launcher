using KilrkrowLauncher.Models;

namespace KilrkrowLauncher.Install;

public sealed class InstallProgress
{
    public required string Phase { get; init; }
    public long BytesReceived { get; init; }
    public long? TotalBytes { get; init; }
    public int Percent { get; init; }
}

public sealed class InstallResult
{
    public bool Succeeded { get; init; }
    public string? LaunchPath { get; init; }
    public string? Message { get; init; }
    public bool Cancelled { get; init; }
}

/// <summary>
/// Install seam. v1 registers <see cref="GitHubReleaseInstallProvider"/> only.
/// A Chocolatey provider can implement this later; do not add choco in v1.
/// </summary>
public interface IInstallProvider
{
    string Id { get; }
    string DisplayName { get; }
    bool CanInstall(CatalogTool tool);
    Task<InstallResult> InstallAsync(
        CatalogTool tool,
        IProgress<InstallProgress>? progress,
        CancellationToken cancellationToken);
}

public static class InstallProviders
{
    public static IReadOnlyList<IInstallProvider> CreateV1(
        GitHubReleaseInstallProvider githubRelease)
    {
        // Chocolatey = future IInstallProvider. Not constructed in v1.
        return [githubRelease];
    }
}
