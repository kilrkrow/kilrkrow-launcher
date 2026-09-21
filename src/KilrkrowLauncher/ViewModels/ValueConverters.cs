using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace KilrkrowLauncher.ViewModels;

public sealed class BoolToVis : IValueConverter
{
    public static readonly BoolToVis Instance = new();
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class InverseBool : IValueConverter
{
    public static readonly InverseBool Instance = new();
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is not true;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class InverseBoolToVis : IValueConverter
{
    public static readonly InverseBoolToVis Instance = new();
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
