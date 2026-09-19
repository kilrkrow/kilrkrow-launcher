using System.Net.Http;
using System.Windows;
using KilrkrowLauncher.Catalog;
using KilrkrowLauncher.Detection;
using KilrkrowLauncher.Install;
using KilrkrowLauncher.Launch;
using KilrkrowLauncher.Native;
using KilrkrowLauncher.Settings;
using KilrkrowLauncher.Tray;
using KilrkrowLauncher.ViewModels;

namespace KilrkrowLauncher;

public partial class App : System.Windows.Application
{
    private HttpClient? _http;
    private TrayService? _tray;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var folders = new WindowsSpecialFolders();
        var files = new PhysicalFileProbe();
        var store = new LauncherSettingsStore(LauncherSettingsStore.DefaultPath(folders.LocalAppData));
        var settings = store.Load();

        _http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        var catalog = GitHubCatalogClient.Create(_http, () => settings.GitHubToken);
        var detector = new InstallDetector(
            files,
            folders,
            new WindowsStartMenuProbe(folders, files),
            new WindowsUninstallRegistryProbe());
        var processes = new WindowsProcessHost();
        var launch = new LaunchService(processes);
        var install = new GitHubReleaseInstallProvider(
            new HttpFileDownloader(_http),
            new WindowsInstallerRunner(),
            folders,
            files);
        _ = InstallProviders.CreateV1(install);

        var vm = new MainViewModel(catalog, detector, launch, install, processes, store, settings);
        var window = new MainWindow(vm);
        _tray = new TrayService(
            window,
            () => window.Dispatcher.InvokeAsync(() => vm.RefreshAsync()),
            () => window.Dispatcher.Invoke(Shutdown));
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _http?.Dispose();
        base.OnExit(e);
    }
}
