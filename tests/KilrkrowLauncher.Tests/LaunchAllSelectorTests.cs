using KilrkrowLauncher.Launch;

namespace KilrkrowLauncher.Tests;

public sealed class LaunchAllSelectorTests
{
    [Fact]
    public void LaunchAll_IsCheckedIntersectInstalled()
    {
        var rows = new[]
        {
            new Row("a", Checked: true, Installed: true),
            new Row("b", Checked: true, Installed: false),
            new Row("c", Checked: false, Installed: true),
            new Row("d", Checked: false, Installed: false)
        };

        var selected = LaunchAllSelector.Select(rows, r => r.Checked, r => r.Installed);
        Assert.Single(selected);
        Assert.Equal("a", selected[0].Id);
    }

    [Fact]
    public void LaunchAll_EmptyWhenNothingCheckedAndInstalled()
    {
        var rows = new[]
        {
            new Row("missing", Checked: true, Installed: false),
            new Row("idle", Checked: false, Installed: true)
        };
        Assert.Empty(LaunchAllSelector.Select(rows, r => r.Checked, r => r.Installed));
    }

    private sealed record Row(string Id, bool Checked, bool Installed);
}
