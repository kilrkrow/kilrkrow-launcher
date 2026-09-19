using KilrkrowLauncher.Models;

namespace KilrkrowLauncher.Detection;

public interface IFileProbe
{
    bool FileExists(string path);
    IEnumerable<string> EnumerateFiles(string directory, string searchPattern, SearchOption option);
    long FileLength(string path);
}

public interface ISpecialFolders
{
    string LocalAppData { get; }
    string ProgramFiles { get; }
    string ProgramFilesX86 { get; }
    IReadOnlyList<string> StartMenuProgramRoots { get; }
}

public interface IStartMenuProbe
{
    IReadOnlyList<ShortcutHit> FindShortcuts(IReadOnlyList<string> nameHints);
}

public interface IUninstallRegistryProbe
{
    IReadOnlyList<UninstallRecord> Find(IReadOnlyList<string> nameHints);
}

public sealed class PhysicalFileProbe : IFileProbe
{
    public bool FileExists(string path) => File.Exists(path);

    public long FileLength(string path)
    {
        try
        {
            return File.Exists(path) ? new FileInfo(path).Length : 0;
        }
        catch
        {
            return 0;
        }
    }

    public IEnumerable<string> EnumerateFiles(string directory, string searchPattern, SearchOption option)
    {
        if (!Directory.Exists(directory))
            yield break;

        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(directory, searchPattern, option);
        }
        catch
        {
            yield break;
        }

        foreach (var file in files)
            yield return file;
    }
}

public sealed class WindowsSpecialFolders : ISpecialFolders
{
    public string LocalAppData => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    public string ProgramFiles => Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
    public string ProgramFilesX86 => Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

    public IReadOnlyList<string> StartMenuProgramRoots =>
    [
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs")
    ];
}
