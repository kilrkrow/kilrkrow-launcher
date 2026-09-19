using System.Diagnostics;
using System.Runtime.InteropServices;
using KilrkrowLauncher.Launch;

namespace KilrkrowLauncher.Native;

internal sealed class WindowsProcessHost : IProcessHost
{
    private const int SwRestore = 9;

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(nint hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(nint hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(nint hWnd);

    public bool TryFindRunning(IEnumerable<string> processNames, out RunningProcess process)
    {
        foreach (var name in processNames)
        {
            Process[] found;
            try
            {
                found = Process.GetProcessesByName(name);
            }
            catch
            {
                continue;
            }

            foreach (var p in found)
            {
                try
                {
                    process = new RunningProcess(p.Id, p.ProcessName, p.MainWindowHandle);
                    return true;
                }
                catch
                {
                    // process exited while inspecting
                }
            }
        }

        process = default;
        return false;
    }

    public void Focus(RunningProcess process)
    {
        if (process.MainWindowHandle == 0)
            return;
        if (IsIconic(process.MainWindowHandle))
            ShowWindow(process.MainWindowHandle, SwRestore);
        SetForegroundWindow(process.MainWindowHandle);
    }

    public void Start(string path, bool useShellExecute, string? arguments = null)
    {
        var info = new ProcessStartInfo
        {
            FileName = path,
            Arguments = arguments ?? "",
            UseShellExecute = useShellExecute,
            WorkingDirectory = Path.GetDirectoryName(path) ?? Environment.CurrentDirectory
        };
        Process.Start(info);
    }
}
