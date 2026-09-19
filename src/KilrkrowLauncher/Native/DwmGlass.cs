using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace KilrkrowLauncher.Native;

internal static class DwmGlass
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaSystemBackdropType = 38;
    private const int BackdropAcrylic = 3;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int value, int size);

    public static void TryApply(Window window)
    {
        var hwnd = new WindowInteropHelper(window).EnsureHandle();
        var dark = 1;
        _ = DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref dark, sizeof(int));
        var backdrop = BackdropAcrylic;
        _ = DwmSetWindowAttribute(hwnd, DwmwaSystemBackdropType, ref backdrop, sizeof(int));
    }
}
