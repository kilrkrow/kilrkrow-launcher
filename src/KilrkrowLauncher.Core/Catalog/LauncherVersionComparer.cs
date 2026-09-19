namespace KilrkrowLauncher.Catalog;

public static class LauncherVersionComparer
{
    public static bool IsRemoteNewer(string? remoteTag, Version? running)
    {
        if (running is null || !TryParseTag(remoteTag, out var remote))
            return false;
        return Normalize(remote) > Normalize(running);
    }

    public static bool TryParseTag(string? tag, out Version version)
    {
        version = new Version(0, 0, 0, 0);
        if (string.IsNullOrWhiteSpace(tag))
            return false;

        var t = tag.Trim();
        if (t.StartsWith('v') || t.StartsWith('V'))
            t = t[1..];
        var plus = t.IndexOf('+');
        if (plus >= 0)
            t = t[..plus];
        var dash = t.IndexOf('-');
        if (dash >= 0)
            t = t[..dash];

        if (!Version.TryParse(t, out var parsed))
            return false;
        version = parsed;
        return true;
    }

    public static Version Normalize(Version version)
        => new(
            Math.Max(0, version.Major),
            Math.Max(0, version.Minor),
            version.Build < 0 ? 0 : version.Build,
            version.Revision < 0 ? 0 : version.Revision);
}
