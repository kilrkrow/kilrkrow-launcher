namespace KilrkrowLauncher.Detection;

/// <summary>
/// Chooses the real app exe from a staged folder or zip extract.
/// Never returns helpers such as <c>createdump.exe</c> and never uses
/// un-scored <c>EnumerateFiles().FirstOrDefault()</c>.
/// </summary>
public static class PrimaryExePicker
{
    private static readonly string[] ExactNoiseStems =
    [
        "createdump", "dump", "crashpad", "crashpad_handler", "werfault",
        "vcredist", "uninstall", "unins000", "setup", "update"
    ];

    private static readonly string[] NoiseContains =
    [
        "createdump", "crashpad", "uninstall", "unins000", "setup",
        "vcredist", "werfault", "crash"
    ];

    public static bool IsNoiseName(string pathOrName)
    {
        if (string.IsNullOrWhiteSpace(pathOrName))
            return true;

        var file = Path.GetFileName(pathOrName.Trim());
        if (file.Length == 0)
            return true;
        if (!file.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            return true;
        if (file.EndsWith(".exe.config", StringComparison.OrdinalIgnoreCase)
            || file.EndsWith(".exe.manifest", StringComparison.OrdinalIgnoreCase))
            return true;

        var stem = Path.GetFileNameWithoutExtension(file);
        if (stem.Length == 0)
            return true;

        foreach (var exact in ExactNoiseStems)
        {
            if (stem.Equals(exact, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        foreach (var token in NoiseContains)
        {
            if (stem.Contains(token, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        if (stem.StartsWith("cef", StringComparison.OrdinalIgnoreCase))
            return true;
        if (stem.StartsWith("report", StringComparison.OrdinalIgnoreCase))
            return true;
        if (stem.Contains("update", StringComparison.OrdinalIgnoreCase))
            return true;
        if (stem.EndsWith("dump", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    public static string? Pick(
        IEnumerable<string> exePaths,
        string? repo,
        string? displayName,
        IReadOnlyList<string>? preferredFileNames = null,
        Func<string, long>? getLength = null)
    {
        var preferred = NormalizePreferred(preferredFileNames);
        var hints = BuildHints(repo, displayName, preferred);
        var ranked = new List<(string Path, int Score, long Size)>();

        foreach (var raw in exePaths)
        {
            if (string.IsNullOrWhiteSpace(raw))
                continue;
            var path = raw.Trim();
            if (!path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                continue;
            if (IsNoiseName(path))
                continue;

            var size = 0L;
            if (getLength is not null)
            {
                try
                {
                    size = Math.Max(0, getLength(path));
                }
                catch
                {
                    size = 0;
                }
            }

            ranked.Add((path, Score(path, repo, displayName, preferred, hints, size), size));
        }

        if (ranked.Count == 0)
            return null;

        return ranked
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Size)
            .Select(x => x.Path)
            .First();
    }

    internal static int Score(
        string path,
        string? repo,
        string? displayName,
        IReadOnlyList<string> preferred,
        IReadOnlyList<string> hints,
        long size)
    {
        var file = Path.GetFileName(path);
        var stem = Path.GetFileNameWithoutExtension(file);
        var score = 0;

        foreach (var name in preferred)
        {
            if (string.Equals(file, name, StringComparison.OrdinalIgnoreCase))
                score += 1000;
            else if (string.Equals(stem, Path.GetFileNameWithoutExtension(name), StringComparison.OrdinalIgnoreCase))
                score += 800;
        }

        var repoStem = Compact(repo);
        var displayStem = Compact(displayName);
        var compact = Compact(stem);

        if (repoStem.Length >= 4 && compact.Equals(repoStem, StringComparison.OrdinalIgnoreCase))
            score += 500;
        if (displayStem.Length >= 4 && compact.Equals(displayStem, StringComparison.OrdinalIgnoreCase))
            score += 500;

        if (repoStem.Length >= 4
            && (compact.Contains(repoStem, StringComparison.OrdinalIgnoreCase)
                || repoStem.Contains(compact, StringComparison.OrdinalIgnoreCase)))
            score += 200;

        if (AssetNameHints.NameMatches(path, hints))
            score += 120;

        // Soft size bonus: prefer the larger UI exe when names are still tied
        // (Sideclip ~180KB vs createdump ~72KB — createdump is already noise).
        if (size > 0)
            score += (int)Math.Min(40, size / 8_000);

        return score;
    }

    private static IReadOnlyList<string> NormalizePreferred(IReadOnlyList<string>? preferredFileNames)
    {
        if (preferredFileNames is null || preferredFileNames.Count == 0)
            return [];

        return preferredFileNames
            .Select(Path.GetFileName)
            .Where(n => !string.IsNullOrWhiteSpace(n) && !IsNoiseName(n!))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> BuildHints(string? repo, string? displayName, IReadOnlyList<string> preferred)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddHint(set, repo);
        AddHint(set, displayName);
        if (!string.IsNullOrWhiteSpace(repo))
        {
            AddHint(set, repo.Replace("-", ""));
            AddHint(set, repo.Replace("-", " "));
        }

        foreach (var name in preferred)
        {
            AddHint(set, name);
            AddHint(set, Path.GetFileNameWithoutExtension(name));
        }

        return set.Where(h => h.Length >= 4).ToArray();
    }

    private static void AddHint(HashSet<string> set, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            set.Add(value.Trim());
    }

    private static string Compact(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";
        return new string(value.Where(char.IsLetterOrDigit).ToArray());
    }
}
