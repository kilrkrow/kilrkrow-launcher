using System.Diagnostics;
using KilrkrowLauncher.Install;

namespace KilrkrowLauncher.Native;

internal sealed class WindowsInstallerRunner : IInstallerRunner
{
    public Task<int> RunVisibleAsync(string fileName, string? arguments, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            var info = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments ?? "",
                UseShellExecute = true
                // Do not set Verb = runas. MSI/setup show UAC themselves.
            };
            using var process = Process.Start(info);
            if (process is null)
                return 1;
            process.WaitForExit();
            return process.ExitCode;
        }, cancellationToken);
    }
}
