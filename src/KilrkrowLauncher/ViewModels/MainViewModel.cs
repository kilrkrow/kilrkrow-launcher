using System.Collections.ObjectModel;
using System.Reflection;
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
    private readonly ManifestCatalogClient _manifest;
    private readonly GitHubCatalogClient _apiFallback;
    private readonly InstallDetector _detector;
    private readonly LaunchService _launch;
    private readonly IInstallProvider _install;
    private readonly IProcessHost _processes;
    private readonly ILauncherSelfUpdater _selfUpdate;
    private readonly LauncherSettingsStore _store;
    private readonly LauncherSettings _settings;
    private string _status = "Ready.";
    private bool _busy;
    private string _tokenDraft = "";
    private bool _updateAvailable;
    private CatalogLauncherInfo? _pendingLauncher;

    public MainViewModel(
        ManifestCatalogClient manifest,
        GitHubCatalogClient apiFallback,
        InstallDetector detector,
        LaunchService launch,
        IInstallProvider install,
        IProcessHost processes,
        ILauncherSelfUpdater selfUpdate,
        LauncherSettingsStore store,
        LauncherSettings settings)
    {
        _manifest = manifest;
        _apiFallback = apiFallback;
        _detector = detector;
        _launch = launch;
        _install = install;
        _processes = processes;
        _selfUpdate = selfUpdate;
        _store = store;
        _settings = settings;
        _tokenDraft = settings.GitHubToken ?? "";
        RefreshCommand = new RelayCommand(RefreshAsync, () => !_busy);
        LaunchAllCommand = new RelayCommand(LaunchAllAsync, () => !_busy);
        SaveTokenCommand = new RelayCommand(SaveToken);
        UpdateLauncherCommand = new RelayCommand(UpdateLauncherAsync, () => _updateAvailable && !_busy);
        ApplyCache();
    }

    public ObservableCollection<ToolRowViewModel> Tools { get; } = [];
    public ICommand RefreshCommand { get; }
    public ICommand LaunchAllCommand { get; }
    public ICommand SaveTokenCommand { get; }
    public ICommand UpdateLauncherCommand { get; }
    public bool IsBusy { get => _busy; private set => Set(ref _busy, value); }
    public bool HasTools => Tools.Count > 0;
    public string Status { get => _status; private set => Set(ref _status, value); }
    public bool UpdateAvailable { get => _updateAvailable; private set => Set(ref _updateAvailable, value); }

    public string TokenDraft
    {
        get => _tokenDraft;
        set => Set(ref _tokenDraft, value);
    }

    public string TokenHint =>
        string.IsNullOrWhiteSpace(_settings.GitHubToken)
            ? "No token (manifest catalog). Token is only for API fallback."
            : "Token saved locally (API fallback).";

    public void ApplyCache()
    {
        var cached = _manifest.TryLoadCache();
        if (cached is null)
            return;
        ReplaceTools(cached.ToCatalogTools());
        ApplyLauncher(cached.Launcher);
        Status = "Cached catalog (" + cached.GeneratedAt.ToLocalTime().ToString("g") + "). Refreshing...";
    }

    public async Task RefreshAsync()
    {
        if (_busy)
            return;
        IsBusy = true;
        RaiseCommands();
        Status = "Loading catalog manifest...";
        try
        {
            var result = await _manifest.LoadAsync();
            ReplaceTools(result.Tools);
            ApplyLauncher(result.Manifest.Launcher);
            Status = result.Tools.Count == 0
                ? "Manifest has no Windows tools yet."
                : "Catalog: " + result.Tools.Count + " Windows tool(s)"
                  + (result.NotModified ? " (not modified)." : ".")
                  + " Launch all starts checked + installed only.";
        }
        catch (Exception manifestEx)
        {
            Status = "Manifest unavailable (" + Short(manifestEx.Message) + "). Trying GitHub API fallback...";
            try
            {
                var tools = await _apiFallback.LoadPublicWindowsToolsAsync(_settings.Owner);
                ReplaceTools(tools);
                Status = tools.Count == 0
                    ? "API fallback: no Windows tools."
                    : "API fallback: " + tools.Count + " Windows tool(s). Launch all starts checked + installed only.";
            }
            catch (GitHubRateLimitException ex)
            {
                Status = ex.UserMessage;
            }
            catch (Exception ex)
            {
                Status = "Catalog error: " + ex.Message;
            }
        }
        finally
        {
            Raise(nameof(HasTools));
            IsBusy = false;
            RaiseCommands();
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

    private async Task UpdateLauncherAsync()
    {
        if (_pendingLauncher is null)
            return;
        IsBusy = true;
        RaiseCommands();
        try
        {
            var progress = new Progress<InstallProgress>(p => Status = "Updating launcher... " + p.Phase);
            await _selfUpdate.UpdateAsync(_pendingLauncher, progress, CancellationToken.None);
            Status = "Launcher update started. The app will restart...";
            ExitRequested?.Invoke();
        }
        catch (Exception ex)
        {
            Status = "Update failed: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
            RaiseCommands();
        }
    }

    public event Action? ExitRequested;

    private void ApplyLauncher(CatalogLauncherInfo? info)
    {
        _pendingLauncher = info;
        var running = Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 1, 0);
        UpdateAvailable = info is not null
                          && !string.IsNullOrWhiteSpace(info.AssetUrl)
                          && LauncherVersionComparer.IsRemoteNewer(info.Tag, running);
        ((RelayCommand)UpdateLauncherCommand).RaiseCanExecuteChanged();
    }

    private void ReplaceTools(IReadOnlyList<KilrkrowLauncher.Models.CatalogTool> tools)
    {
        var checkedByRepo = Tools.ToDictionary(t => t.Repo, t => t.IsChecked, StringComparer.OrdinalIgnoreCase);
        Tools.Clear();
        foreach (var tool in tools)
        {
            var row = new ToolRowViewModel(tool, _detector, _launch, _install, _processes, msg => Status = msg);
            if (checkedByRepo.TryGetValue(tool.Repo, out var wasChecked))
                row.IsChecked = wasChecked;
            Tools.Add(row);
        }

        Raise(nameof(HasTools));
    }

    private void SaveToken()
    {
        _settings.GitHubToken = string.IsNullOrWhiteSpace(TokenDraft) ? null : TokenDraft.Trim();
        _store.Save(_settings);
        Raise(nameof(TokenHint));
        Status = string.IsNullOrWhiteSpace(_settings.GitHubToken)
            ? "Token cleared. Manifest catalog still works."
            : "Token saved to local settings (not in git).";
    }

    private void RaiseCommands()
    {
        ((RelayCommand)RefreshCommand).RaiseCanExecuteChanged();
        ((RelayCommand)LaunchAllCommand).RaiseCanExecuteChanged();
        ((RelayCommand)UpdateLauncherCommand).RaiseCanExecuteChanged();
    }

    private static string Short(string message)
    {
        var one = message.Replace('\n', ' ').Trim();
        return one.Length <= 80 ? one : one[..80] + "...";
    }
}
