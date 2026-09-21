using KilrkrowLauncher.Detection;

namespace KilrkrowLauncher.Launch;

public enum AlreadyRunningPolicy
{
    /// <summary>
    /// Default: if a process matching the tool is already running, focus its
    /// main window and do not start a second instance.
    /// </summary>
    FocusExisting
}

public readonly record struct RunningProcess(int ProcessId, string ProcessName, nint MainWindowHandle);

public enum LaunchKind
{
    Started,
    FocusedExisting,
    Failed
}

public sealed class LaunchResult
{
    public required LaunchKind Kind { get; init; }
    public string? Message { get; init; }
}

public interface IProcessHost
{
    bool TryFindRunning(IEnumerable<string> processNames, out RunningProcess process);
    void Focus(RunningProcess process);
    void Start(string path, bool useShellExecute, string? workingDirectory, string? arguments = null);
}

public sealed class LaunchService
{
    public const AlreadyRunningPolicy DefaultPolicy = AlreadyRunningPolicy.FocusExisting;

    private readonly IProcessHost _host;

    public LaunchService(IProcessHost host)
    {
        _host = host;
    }

    public LaunchResult Launch(string path, IEnumerable<string> processNames)
    {
        if (string.IsNullOrWhiteSpace(path))
            return new LaunchResult { Kind = LaunchKind.Failed, Message = "No launch path." };

        var names = processNames
            .Select(n => Path.GetFileNameWithoutExtension(n))
            .Where(n => !string.IsNullOrWhiteSpace(n) && !PrimaryExePicker.IsNoiseName(n + ".exe"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (names.Length > 0 && _host.TryFindRunning(names, out var running))
        {
            _host.Focus(running);
            return new LaunchResult
            {
                Kind = LaunchKind.FocusedExisting,
                Message = "Already running... focused existing window."
            };
        }

        var workingDirectory = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(workingDirectory))
            workingDirectory = Environment.CurrentDirectory;

        try
        {
            _host.Start(path, useShellExecute: true, workingDirectory);
            return new LaunchResult { Kind = LaunchKind.Started, Message = "Started " + path + "." };
        }
        catch (Exception ex)
        {
            return new LaunchResult
            {
                Kind = LaunchKind.Failed,
                Message = "Launch failed: " + path + " (working directory " + workingDirectory + "): "
                          + ex.GetType().Name + ": " + ex.Message
            };
        }
    }
}
