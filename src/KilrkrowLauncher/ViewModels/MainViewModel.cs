using System.Collections.ObjectModel;
using System.Windows.Input;
using KilrkrowLauncher.Catalog;
using KilrkrowLauncher.Detection;
using KilrkrowLauncher.Http;
using KilrkrowLauncher.Install;
using KilrkrowLauncher.Launch;
using KilrkrowLauncher.Settings;

namespace KilrkrowLauncher.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly GitHubCatalogClient _catalog;
    private readonly InstallDetector _detector;
    private readonly LaunchService _launch;
    private readonly IInstallProvider _install;
    private readonly IProcessHost _processes;
    private readonly LauncherSettingsStore _store;
    private readonly LauncherSettings _settings;
    private string _status = "Ready.";
    private bool _busy;
    private string _tokenDraft = "";

    public MainViewModel(
        GitHubCatalogClient catalog,
        InstallDetector detector,
        LaunchService launch,
        IInstallProvider install,
        IProcessHost processes,
        LauncherSettingsStore store,
        LauncherSettings settings)
    {
        _catalog = catalog;
        _detector = detector;
        _launch = launch;
        _install = install;
        _processes = processes;
        _store = store;
        _settings = settings;
        _tokenDraft = settings.GitHubToken ?? "";
        RefreshCommand = new RelayCommand(RefreshAsync, () => !_busy);
        LaunchAllCommand = new RelayCommand(LaunchAllAsync, () => !_busy);
        SaveTokenCommand = new RelayCommand(SaveToken);
    }

    public ObservableCollection<ToolRowViewModel> Tools { get; } = [];
    public ICommand RefreshCommand { get; }
    public ICommand LaunchAllCommand { get; }
    public ICommand SaveTokenCommand { get; }
    public bool IsBusy { get => _busy; private set => Set(ref _busy, value); }
    public bool HasTools => Tools.Count > 0;
    public string Status { get => _status; private set => Set(ref _status, value); }

    public string TokenDraft
    {
        get => _tokenDraft;
        set => Set(ref _tokenDraft, value);
    }

    public string TokenHint =>
        string.IsNullOrWhiteSpace(_settings.GitHubToken)
            ? "No token (public catalog)."
            : "Token saved locally.";

    public async Task RefreshAsync()
    {
        if (_busy)
            return;
        IsBusy = true;
        ((RelayCommand)RefreshCommand).RaiseCanExecuteChanged();
        ((RelayCommand)LaunchAllCommand).RaiseCanExecuteChanged();
        Status = "Loading public kilrkrow catalog...";
        try
        {
            var tools = await _catalog.LoadPublicWindowsToolsAsync(_settings.Owner);
            Tools.Clear();
            foreach (var tool in tools)
            {
                Tools.Add(new ToolRowViewModel(tool, _detector, _launch, _install, _processes, msg => Status = msg));
            }

            Raise(nameof(HasTools));
            Status = tools.Count == 0
                ? "No public repos with a Windows latest-release asset."
                : "Catalog: " + tools.Count + " Windows tool(s). Launch all starts checked + installed only.";
        }
        catch (GitHubRateLimitException ex)
        {
            Status = ex.UserMessage;
        }
        catch (Exception ex)
        {
            Status = "Catalog error: " + ex.Message;
        }
        finally
        {
            Raise(nameof(HasTools));
            IsBusy = false;
            ((RelayCommand)RefreshCommand).RaiseCanExecuteChanged();
            ((RelayCommand)LaunchAllCommand).RaiseCanExecuteChanged();
        }
    }

    public async Task LaunchAllAsync()
    {
        var selected = LaunchAllSelector.Select(Tools, t => t.IsChecked, t => t.IsInstalled);
        if (selected.Count == 0)
        {
            Status = "Launch all: nothing to start (need checked and installed).";
            return;
        }

        foreach (var row in selected)
            await row.LaunchAsync();
        Status = "Launch all finished (" + selected.Count + ").";
    }

    public void RefreshInstallStates()
    {
        foreach (var row in Tools)
            row.RefreshState();
    }

    private void SaveToken()
    {
        _settings.GitHubToken = string.IsNullOrWhiteSpace(TokenDraft) ? null : TokenDraft.Trim();
        _store.Save(_settings);
        Raise(nameof(TokenHint));
        Status = string.IsNullOrWhiteSpace(_settings.GitHubToken)
            ? "Token cleared. Public catalog still works."
            : "Token saved to local settings (not in git).";
    }
}
