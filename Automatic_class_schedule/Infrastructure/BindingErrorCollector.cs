using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace Automatic_class_schedule.Infrastructure;

public static class BindingErrorCollector
{
    private static readonly List<BindingErrorInfo> _capturedErrors = new();

    public static List<BindingErrorInfo> Capture(DependencyObject root)
    {
        var errors = new List<BindingErrorInfo>();

        try
        {
            var debugSettings = Application.Current.DebugSettings;
            if (debugSettings != null)
            {
                debugSettings.BindingFailed += OnBindingFailed;
            }
        }
        catch
        {
            // DebugSettings not available in all contexts
        }

        WalkForBindingErrors(root, errors, "");

        lock (_capturedErrors)
        {
            errors.AddRange(_capturedErrors);
            _capturedErrors.Clear();
        }

        return errors;
    }

    private static void WalkForBindingErrors(DependencyObject parent, List<BindingErrorInfo> errors, string path)
    {
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            string childPath = $"{path}/{child.GetType().Name}";

            if (child is FrameworkElement fe && !string.IsNullOrWhiteSpace(fe.Name))
                childPath = $"{path}/{fe.Name}";

            if (child is DependencyObject dp)
                WalkForBindingErrors(dp, errors, childPath);
        }
    }

    private static void OnBindingFailed(object sender, BindingFailedEventArgs e)
    {
        lock (_capturedErrors)
        {
            _capturedErrors.Add(new BindingErrorInfo
            {
                Path = e.Message,
                Reason = "XAML binding failure",
            });
        }

        System.Diagnostics.Debug.WriteLine($"[BindingError] {e.Message}");
    }
}
