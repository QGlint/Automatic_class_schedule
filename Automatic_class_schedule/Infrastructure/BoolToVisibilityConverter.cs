using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace Automatic_class_schedule.Infrastructure;

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        bool invert = parameter is string s && s == "Inverse";
        bool boolValue = value is true;
        if (invert) boolValue = !boolValue;
        return boolValue ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return value is Visibility.Visible;
    }
}

public sealed class StringMatchToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value?.ToString() == parameter?.ToString() ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (parameter is string s && value is string v)
            return !string.Equals(v, s, StringComparison.Ordinal) ? Visibility.Visible : Visibility.Collapsed;
        return value is false ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

/// <summary>bool 转画刷：true=深色(#FF214E78)，false=浅色(#FFF0F5FF)</summary>
public sealed class BoolToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        bool selected = value is true;
        var brush = new SolidColorBrush(selected
            ? Windows.UI.Color.FromArgb(255, 0x21, 0x4E, 0x78)
            : Windows.UI.Color.FromArgb(255, 0xF0, 0xF5, 0xFF));
        return brush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

/// <summary>bool 转前景色：true=白色，false=深色(#FF214E78)</summary>
public sealed class BoolToForegroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        bool selected = value is true;
        var brush = new SolidColorBrush(selected
            ? Windows.UI.Color.FromArgb(255, 0xFF, 0xFF, 0xFF)
            : Windows.UI.Color.FromArgb(255, 0x21, 0x4E, 0x78));
        return brush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
