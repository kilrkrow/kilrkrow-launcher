using System.Windows.Input;

namespace KilrkrowLauncher.ViewModels;

public sealed class RelayCommand : ICommand
{
    private readonly Func<Task>? _async;
    private readonly Action? _sync;
    private readonly Func<bool>? _can;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _sync = execute;
        _can = canExecute;
    }

    public RelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
    {
        _async = execute;
        _can = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _can?.Invoke() ?? true;

    public async void Execute(object? parameter)
    {
        if (_sync is not null)
            _sync();
        else if (_async is not null)
            await _async();
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
