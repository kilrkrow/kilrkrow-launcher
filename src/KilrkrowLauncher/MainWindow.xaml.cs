using System.Windows;
using System.Windows.Input;
using KilrkrowLauncher.Native;
using KilrkrowLauncher.ViewModels;

namespace KilrkrowLauncher;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += OnLoaded;
    }

    public MainViewModel ViewModel => (MainViewModel)DataContext;

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        DwmGlass.TryApply(this);
        if (!string.IsNullOrWhiteSpace(ViewModel.TokenDraft))
            TokenBox.Password = ViewModel.TokenDraft;
        await ViewModel.RefreshAsync();
    }

    private void MinimizeClick(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void CloseClick(object sender, RoutedEventArgs e) => Close();

    private void SaveTokenClick(object sender, RoutedEventArgs e)
    {
        ViewModel.TokenDraft = TokenBox.Password;
        ViewModel.SaveTokenCommand.Execute(null);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (e.ButtonState == MouseButtonState.Pressed && e.GetPosition(this).Y < 48)
            DragMove();
    }
}
