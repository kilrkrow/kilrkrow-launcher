using System.IO;
using System.Windows.Input;
using KilrkrowLauncher.Detection;
using KilrkrowLauncher.Install;
using KilrkrowLauncher.Launch;
using KilrkrowLauncher.Models;

namespace KilrkrowLauncher.ViewModels;

public sealed class ToolRowViewModel : ObservableObject
{
    private readonly InstallDetector _detector;
    private readonly LaunchService _launch;
    private readonly IInstallProvider _install;
    private readonly IProcessHost _processes;
    private readonly Action<string> _status;
    private bool _isChecked = true;
    private ToolInstallState _state = ToolInstallState.Missing;
    private string? _launchPath;
    private string? _error;
    private bool _busy;
    private int _progress;
    private string _progressText = "";
    private CancellationTokenSource? _installCts;

    public ToolRowViewModel(
        CatalogTool tool,
        InstallDetector detector,
        LaunchService launch,
        IInstallProvider install,
        IProcessHost processes,
        Action<string> status)
    {
        Tool = tool;
        _detector = detector;
        _launch = launch;
        _install = install;
        _processes = processes;
        _status = status;
        ActionCommand = new RelayCommand(OnActionAsync, () => !_busy);
        CancelCommand = new RelayCommand(CancelInstall, () => _busy);
        RefreshState();
    }

    public CatalogTool Tool { get; }
    public string Name => Tool.DisplayName;
    public string Repo => Tool.Repo;
    public string Tag => Tool.TagName;
    public string Description => Tool.Description ?? Tool.WindowsAsset.Asset.Name;
    public ICommand ActionCommand { get; }
    public ICommand CancelCommand { get; }

    public bool IsChecked
    {
        get => _isChecked;
        set => Set(ref _isChecked, value);
    }

    public ToolInstallState State
    {
        get => _state;
        private set
        {
            Set(ref _state, value);
            Raise(nameof(StateLabel));
            Raise(nameof(ActionLabel));
            Raise(nameof(IsInstalled));
            Raise(nameof(StateBrushKey));
        }
    }

    public bool IsInstalled => State is ToolInstallState.Installed or ToolInstallState.Running;
    public bool IsBusy => _busy;
    public int Progress { get => _progress; private set => Set(ref _progress, value); }
    public string ProgressText { get => _progressText; private set => Set(ref _progressText, value); }
    public bool ShowProgress => _busy;

    public string StateLabel => State switch
    {
        ToolInstallState.Installed => "Installed",
        ToolInstallState.Missing => "Missing",
        ToolInstallState.Running => "Running",
        ToolInstallState.Error => "Error",
        _ => "Unknown"
    };

    public string StateBrushKey => State switch
    {
        ToolInstallState.Installed => "OkBrush",
        ToolInstallState.Running => "TealBrush",
        ToolInstallState.Missing => "GoldBrush",
        _ => "BadBrush"
    };

    public string ActionLabel => State switch
    {
        ToolInstallState.Running => "Focus",
        ToolInstallState.Installed => "Launch",
        _ => "Download & install"
    };

    public string? Error => _error;

    public void RefreshState()
    {
        if (_busy)
            return;

        var lookup = _detector.Detect(Tool);
        _launchPath = lookup.LaunchPath;
        if (!lookup.IsInstalled)
        {
            State = string.IsNullOrWhiteSpace(_error) ? ToolInstallState.Missing : ToolInstallState.Error;
            return;
        }

        var names = ProcessNames();
        State = _processes.TryFindRunning(names, out _)
            ? ToolInstallState.Running
            : ToolInstallState.Installed;
    }

    public async Task LaunchAsync()
    {
        if (string.IsNullOrWhiteSpace(_launchPath))
        {
            _error = "Not installed.";
            State = ToolInstallState.Error;
            return;
        }

        var result = _launch.Launch(_launchPath, ProcessNames());
        if (result.Kind == LaunchKind.Failed)
        {
            _error = result.Message;
            State = ToolInstallState.Error;
            _status(Name + ": " + (result.Message ?? "Launch failed."));
            return;
        }

        _error = null;
        _status(Name + ": " + (result.Message ?? "OK"));
        RefreshState();
        await Task.CompletedTask;
    }

    private async Task OnActionAsync()
    {
        if (IsInstalled)
            await LaunchAsync();
        else
            await InstallAsync();
    }

    private async Task InstallAsync()
    {
        _installCts = new CancellationTokenSource();
        SetBusy(true);
        _error = null;
        try
        {
            var progress = new Progress<InstallProgress>(p =>
            {
                Progress = p.Percent;
                ProgressText = p.Phase;
            });
            var result = await _install.InstallAsync(Tool, progress, _installCts.Token);
            if (result.Cancelled)
            {
                _status(Name + ": cancelled.");
                return;
            }

            if (!result.Succeeded)
            {
                _error = result.Message;
                State = ToolInstallState.Error;
                _status(Name + ": " + (result.Message ?? "Install failed."));
                return;
            }

            _error = null;
            _status(Name + ": " + (result.Message ?? "Installed."));
        }
        catch (Exception ex)
        {
            _error = ex.Message;
            State = ToolInstallState.Error;
            _status(Name + ": " + ex.Message);
        }
        finally
        {
            SetBusy(false);
            RefreshState();
        }
    }

    private void CancelInstall() => _installCts?.Cancel();

    private void SetBusy(bool value)
    {
        _busy = value;
        Raise(nameof(IsBusy));
        Raise(nameof(ShowProgress));
        ((RelayCommand)ActionCommand).RaiseCanExecuteChanged();
        ((RelayCommand)CancelCommand).RaiseCanExecuteChanged();
    }

    private IReadOnlyList<string> ProcessNames()
    {
        var names = new List<string>();
        if (!string.IsNullOrWhiteSpace(_launchPath) && !PrimaryExePicker.IsNoiseName(_launchPath))
            names.Add(Path.GetFileNameWithoutExtension(_launchPath));
        foreach (var exe in Tool.WindowsAsset.ExeEntryNames)
        {
            if (PrimaryExePicker.IsNoiseName(exe))
                continue;
            names.Add(Path.GetFileNameWithoutExtension(exe));
        }

        return names.Where(n => n.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
