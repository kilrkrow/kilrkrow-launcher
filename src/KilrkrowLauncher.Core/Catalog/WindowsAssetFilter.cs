using KilrkrowLauncher.Models;

namespace KilrkrowLauncher.Catalog;

public interface IZipEntryReader
{
    Task<IReadOnlyList<string>?> TryListEntriesAsync(ReleaseAsset asset, CancellationToken cancellationToken);
}

/// <summary>
/// Catalog rule: a repo is included only when its *latest release assets[]* contain
/// a Windows payload. GitHub source zipballs/tarballs are never assets and never qualify.
/// </summary>
public sealed class WindowsAssetFilter
{
    private readonly IZipEntryReader _zipEntries;

    public WindowsAssetFilter(IZipEntryReader zipEntries)
    {
        _zipEntries = zipEntries;
    }

    public static bool IsDirectWindowsBinary(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return false;

        var n = fileName.Trim();
        return n.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
               || n.EndsWith(".msi", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsZipCandidate(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return false;

        var n = fileName.Trim();
        if (!n.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            return false;

        // nupkg is a zip on disk but not a Windows app payload.
        if (n.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }

    public async Task<WindowsAssetPick?> PickAsync(
        IReadOnlyList<ReleaseAsset> assets,
        CancellationToken cancellationToken = default)
    {
        if (assets.Count == 0)
            return null;

        var scored = new List<(int Score, ReleaseAsset Asset, IReadOnlyList<string> Exes)>();

        foreach (var asset in assets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsDirectWindowsBinary(asset.Name))
            {
                var exe = asset.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                    ? new[] { asset.Name }
                    : Array.Empty<string>();
                scored.Add((Score(asset.Name, isMsi: asset.Name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase), isExe: exe.Length > 0, isZip: false), asset, exe));
                continue;
            }

            if (!IsZipCandidate(asset.Name))
                continue;

            var entries = await _zipEntries.TryListEntriesAsync(asset, cancellationToken).ConfigureAwait(false);
            if (entries is null)
                continue; // cannot inspect => do not include (avoids source-zip false positives)

            var exes = ZipExeInspector.EnumerateWindowsExes(entries);
            if (exes.Count == 0)
                continue;

            scored.Add((Score(asset.Name, isMsi: false, isExe: false, isZip: true), asset, exes));
        }

        if (scored.Count == 0)
            return null;

        var best = scored.OrderByDescending(s => s.Score).ThenBy(s => s.Asset.Name, StringComparer.OrdinalIgnoreCase).First();
        return new WindowsAssetPick { Asset = best.Asset, ExeEntryNames = best.Exes };
    }

    internal static int Score(string fileName, bool isMsi, bool isExe, bool isZip)
    {
        var n = fileName.ToLowerInvariant();
        var score = 0;
        if (isMsi) score += 30;
        if (isExe) score += 24;
        if (isZip) score += 16;

        if (n.Contains("setup") || n.Contains("install"))
            score += 6;
        if (n.Contains("app") || n.Contains("gui") || n.Contains("desktop"))
            score += 5;
        if (n.Contains("win-x64") || n.Contains("win64") || n.Contains("windows") || n.Contains("win-x86"))
            score += 4;
        if (n.Contains("cli") || n.Contains("console"))
            score -= 8;
        if (n.Contains("source") || n.Contains("src") || n.Contains("symbols"))
            score -= 12;

        return score;
    }
}
