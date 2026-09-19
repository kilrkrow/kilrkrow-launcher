using System.IO;
using KilrkrowLauncher.Detection;
using KilrkrowLauncher.Models;

namespace KilrkrowLauncher.Native;

internal sealed class WindowsStartMenuProbe : IStartMenuProbe
{
    private readonly ISpecialFolders _folders;
    private readonly IFileProbe _files;

    public WindowsStartMenuProbe(ISpecialFolders folders, IFileProbe files)
    {
        _folders = folders;
        _files = files;
    }

    public IReadOnlyList<ShortcutHit> FindShortcuts(IReadOnlyList<string> nameHints)
    {
        var hits = new List<ShortcutHit>();
        foreach (var root in _folders.StartMenuProgramRoots)
        {
            foreach (var lnk in _files.EnumerateFiles(root, "*.lnk", SearchOption.AllDirectories))
            {
                var name = Path.GetFileNameWithoutExtension(lnk);
                if (!AssetNameHints.NameMatches(name, nameHints))
                    continue;
                hits.Add(new ShortcutHit
                {
                    LinkPath = lnk,
                    LinkName = name,
                    TargetPath = LnkReader.TryReadTarget(lnk)
                });
            }
        }

        return hits;
    }
}
