using System.Drawing;
using System.Windows;
using Forms = System.Windows.Forms;

namespace KilrkrowLauncher.Tray;

internal sealed class TrayService : IDisposable
{
    private readonly Forms.NotifyIcon _icon;

    public TrayService(Window window, Action refresh, Action exit)
    {
        var uri = new Uri("pack://application:,,,/Assets/launcher.ico");
        Icon? ico;
        using (var stream = Application.GetResourceStream(uri)?.Stream)
            ico = stream is null ? null : new Icon(stream);

        if (ico is null)
            throw new InvalidOperationException("Application icon resource is missing.");

        _icon = new Forms.NotifyIcon
        {
            Icon = ico,
            Visible = true,
            Text = "kilrkrow Launcher"
        };
        _icon.DoubleClick += (_, _) =>
        {
            window.Show();
            window.WindowState = WindowState.Normal;
            window.Activate();
        };

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open", null, (_, _) =>
        {
            window.Show();
            window.Activate();
        });
        menu.Items.Add("Refresh catalog", null, (_, _) => refresh());
        menu.Items.Add("Exit", null, (_, _) => exit());
        _icon.ContextMenuStrip = menu;
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
