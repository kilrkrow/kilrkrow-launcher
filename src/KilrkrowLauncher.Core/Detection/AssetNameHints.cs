using KilrkrowLauncher.Models;

namespace KilrkrowLauncher.Detection;

public static class AssetNameHints
{
    private static readonly HashSet<string> Noise = new(StringComparer.OrdinalIgnoreCase)
    {
        "win", "windows", "x64", "x86", "arm64", "amd64", "portable", "setup", "installer",
        "release", "latest", "bin", "dist", "app", "gui", "cli", "console", "self",
        "contained", "single", "file", "zip", "msi", "exe"
    };

    public static IReadOnlyList<string> From(CatalogTool tool)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Add(set, tool.Repo);
        Add(set, tool.DisplayName);
        Add(set, tool.Repo.Replace("-", ""));
        Add(set, tool.Repo.Replace("-", " "));

        Add(set, Path.GetFileNameWithoutExtension(tool.WindowsAsset.Asset.Name));
        foreach (var exe in tool.WindowsAsset.ExeEntryNames)
        {
            var file = Path.GetFileName(exe);
            Add(set, file);
            var stem = Path.GetFileNameWithoutExtension(file);
            Add(set, stem);
            var head = stem.Split('.')[0];
            Add(set, head);
        }

        // Tokenize asset stem: wsbuddy-app-win-x64-v0.2.0 -> wsbuddy
        foreach (var token in Tokenize(tool.WindowsAsset.Asset.Name))
            Add(set, token);

        return set.Where(h => h.Length >= 4).OrderByDescending(h => h.Length).ToArray();
    }

    public static bool NameMatches(string candidate, IEnumerable<string> hints)
    {
        if (string.IsNullOrWhiteSpace(candidate))
            return false;

        var name = Path.GetFileNameWithoutExtension(candidate);
        foreach (var hint in hints)
        {
            if (name.Contains(hint, StringComparison.OrdinalIgnoreCase))
                return true;
            var compact = new string(name.Where(char.IsLetterOrDigit).ToArray());
            var hintCompact = new string(hint.Where(char.IsLetterOrDigit).ToArray());
            if (hintCompact.Length >= 4 && compact.Contains(hintCompact, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static IEnumerable<string> Tokenize(string fileName)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName);
        foreach (var raw in stem.Split(['-', '_', '.', ' '], StringSplitOptions.RemoveEmptyEntries))
        {
            if (raw.StartsWith('v') && raw.Length > 1 && raw.Skip(1).All(c => char.IsDigit(c) || c == '.'))
                continue;
            if (raw.All(char.IsDigit))
                continue;
            if (Noise.Contains(raw))
                continue;
            yield return raw;
        }
    }

    private static void Add(HashSet<string> set, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;
        set.Add(value.Trim());
    }
}
