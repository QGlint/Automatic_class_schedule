using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Automatic_class_schedule.Infrastructure;

public sealed class StringMatchToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is string str && parameter is string target)
        {
            return string.Equals(str, target, StringComparison.Ordinal)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
