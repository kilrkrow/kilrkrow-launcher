using KilrkrowLauncher.Detection;
using KilrkrowLauncher.Models;
using Microsoft.Win32;

namespace KilrkrowLauncher.Native;

internal sealed class WindowsUninstallRegistryProbe : IUninstallRegistryProbe
{
    private static readonly string[] Roots =
    [
        @"Software\Microsoft\Windows\CurrentVersion\Uninstall",
        @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
    ];

    public IReadOnlyList<UninstallRecord> Find(IReadOnlyList<string> nameHints)
    {
        var hits = new List<UninstallRecord>();
        ReadHive(Registry.CurrentUser, nameHints, hits);
        ReadHive(Registry.LocalMachine, nameHints, hits);
        return hits;
    }

    private static void ReadHive(RegistryKey hive, IReadOnlyList<string> nameHints, List<UninstallRecord> hits)
    {
        foreach (var root in Roots)
        {
            using var key = hive.OpenSubKey(root);
            if (key is null)
                continue;

            foreach (var name in key.GetSubKeyNames())
            {
                using var sub = key.OpenSubKey(name);
                if (sub is null)
                    continue;
                var display = sub.GetValue("DisplayName") as string;
                if (string.IsNullOrWhiteSpace(display) || !AssetNameHints.NameMatches(display, nameHints))
                    continue;
                hits.Add(new UninstallRecord
                {
                    DisplayName = display,
                    InstallLocation = sub.GetValue("InstallLocation") as string,
                    DisplayIcon = sub.GetValue("DisplayIcon") as string
                });
            }
        }
    }
}
