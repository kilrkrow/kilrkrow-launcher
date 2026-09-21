namespace KilrkrowLauncher.Launch;

/// <summary>
/// Launch all = checked intersect installed. Missing/error rows are never started.
/// </summary>
public static class LaunchAllSelector
{
    public static IReadOnlyList<T> Select<T>(
        IEnumerable<T> rows,
        Func<T, bool> isChecked,
        Func<T, bool> isInstalled)
    {
        return rows.Where(row => isChecked(row) && isInstalled(row)).ToArray();
    }
}
