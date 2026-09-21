using System.IO.Compression;

namespace KilrkrowLauncher.Catalog;

/// <summary>
/// Decides whether a zip is a Windows binary package (contains a real .exe)
/// versus a source-only archive that happens to be named .zip.
/// </summary>
public static class ZipExeInspector
{
    public static bool ContainsWindowsExe(IEnumerable<string> entryNames)
        => EnumerateWindowsExes(entryNames).Count > 0;

    public static IReadOnlyList<string> EnumerateWindowsExes(IEnumerable<string> entryNames)
    {
        var hits = new List<string>();
        foreach (var raw in entryNames)
        {
            if (string.IsNullOrWhiteSpace(raw))
                continue;

            var path = raw.Replace('\\', '/');
            if (path.Contains("__MACOSX/", StringComparison.OrdinalIgnoreCase))
                continue;

            var file = Path.GetFileName(path);
            if (file.Length == 0)
                continue;

            // Exact .exe only. Source trees often include Foo.exe.config or Foo.exe.manifest.
            if (!file.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                continue;
            if (file.EndsWith(".exe.config", StringComparison.OrdinalIgnoreCase)
                || file.EndsWith(".exe.manifest", StringComparison.OrdinalIgnoreCase))
                continue;

            hits.Add(path);
        }

        return hits;
    }

    public static IReadOnlyList<string> ListEntries(Stream zipStream, bool leaveOpen = true)
    {
        using var zip = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen);
        return zip.Entries.Select(e => e.FullName).ToArray();
    }

    public static bool StreamContainsWindowsExe(Stream zipStream, bool leaveOpen = true)
        => ContainsWindowsExe(ListEntries(zipStream, leaveOpen));
}
