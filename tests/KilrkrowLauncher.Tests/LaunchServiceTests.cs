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
    public void StartsWhenNotRunning_SetsWorkingDirectoryToExeFolder()
    {
        var host = new FakeHost();
        var svc = new LaunchService(host);
        var path = Path.Combine("C:", "apps", "sideclip", "v0.1.0", "Sideclip.exe");
        var result = svc.Launch(path, ["Sideclip.exe", "createdump.exe"]);
        Assert.Equal(LaunchKind.Started, result.Kind);
        Assert.True(host.Started);
        Assert.Equal(Path.GetDirectoryName(path), host.WorkingDirectory);
        Assert.DoesNotContain("createdump", host.LookedUpNames, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Failure_IncludesPathAndException_DoesNotClaimSuccess()
    {
        var host = new FakeHost { ThrowOnStart = new IOException("sharing violation") };
        var svc = new LaunchService(host);
        var path = @"C:\apps\Sideclip.exe";
        var result = svc.Launch(path, ["Sideclip.exe"]);
        Assert.Equal(LaunchKind.Failed, result.Kind);
        Assert.Contains(path, result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("IOException", result.Message, StringComparison.Ordinal);
        Assert.Contains("sharing violation", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Started", result.Message, StringComparison.Ordinal);
    }

    private sealed class FakeHost : IProcessHost
    {
        public RunningProcess? Running { get; init; }
        public bool Started { get; private set; }
        public bool Focused { get; private set; }
        public string? WorkingDirectory { get; private set; }
        public List<string> LookedUpNames { get; } = [];
        public Exception? ThrowOnStart { get; init; }

        public bool TryFindRunning(IEnumerable<string> processNames, out RunningProcess process)
        {
            LookedUpNames.AddRange(processNames);
            if (Running is { } hit && processNames.Any(n => n.Equals(hit.ProcessName, StringComparison.OrdinalIgnoreCase)))
            {
                process = hit;
                return true;
            }

            process = default;
            return false;
        }

        public void Focus(RunningProcess process) => Focused = true;

        public void Start(string path, bool useShellExecute, string? workingDirectory, string? arguments = null)
        {
            if (ThrowOnStart is not null)
                throw ThrowOnStart;
            Started = true;
            WorkingDirectory = workingDirectory;
        }
    }
}
