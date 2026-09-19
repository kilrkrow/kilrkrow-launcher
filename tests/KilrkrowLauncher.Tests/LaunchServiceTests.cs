using KilrkrowLauncher.Launch;

namespace KilrkrowLauncher.Tests;

public sealed class LaunchServiceTests
{
    [Fact]
    public void FocusesExistingProcess_DoesNotStartAgain()
    {
        var host = new FakeHost { Running = new RunningProcess(9, "WinServiceBuddy.App", 42) };
        var svc = new LaunchService(host);
        var result = svc.Launch(@"C:\apps\WinServiceBuddy.App.exe", ["WinServiceBuddy.App.exe"]);
        Assert.Equal(LaunchKind.FocusedExisting, result.Kind);
        Assert.False(host.Started);
        Assert.True(host.Focused);
    }

    [Fact]
    public void StartsWhenNotRunning()
    {
        var host = new FakeHost();
        var svc = new LaunchService(host);
        var result = svc.Launch(@"C:\apps\Tool.exe", ["Tool.exe"]);
        Assert.Equal(LaunchKind.Started, result.Kind);
        Assert.True(host.Started);
    }

    private sealed class FakeHost : IProcessHost
    {
        public RunningProcess? Running { get; init; }
        public bool Started { get; private set; }
        public bool Focused { get; private set; }

        public bool TryFindRunning(IEnumerable<string> processNames, out RunningProcess process)
        {
            if (Running is { } hit && processNames.Any(n => n.Equals(hit.ProcessName, StringComparison.OrdinalIgnoreCase)))
            {
                process = hit;
                return true;
            }

            process = default;
            return false;
        }

        public void Focus(RunningProcess process) => Focused = true;

        public void Start(string path, bool useShellExecute, string? arguments = null) => Started = true;
    }
}
