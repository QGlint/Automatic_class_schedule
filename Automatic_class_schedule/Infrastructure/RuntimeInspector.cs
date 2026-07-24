using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Automatic_class_schedule.Infrastructure;

public static class RuntimeInspector
{
    public static RuntimeSnapshot Inspect(Page page)
    {
        var snapshot = new RuntimeSnapshot
        {
            Page = page.GetType().Name,
            Loaded = page.IsLoaded,
            Timestamp = DateTime.UtcNow.ToString("O"),
            BindingErrors = BindingErrorCollector.Capture(page),
        };

        WalkVisualTree(page, snapshot.Controls, 0);

        snapshot.LayoutWarnings = ValidateLayout(snapshot.Controls);

        return snapshot;
    }

    private static void WalkVisualTree(DependencyObject parent, List<ControlSnapshot> controls, int depth)
    {
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);

            if (child is FrameworkElement fe)
            {
                bool isEnabled = fe is Control c ? c.IsEnabled : true;

                controls.Add(new ControlSnapshot
                {
                    Type = fe.GetType().Name,
                    Name = fe.Name ?? string.Empty,
                    AutomationId = (string)fe.GetValue(AutomationProperties.AutomationIdProperty) ?? string.Empty,
                    Visibility = fe.Visibility.ToString(),
                    IsLoaded = fe.IsLoaded,
                    IsEnabled = isEnabled,
                    Width = fe.ActualWidth,
                    Height = fe.ActualHeight,
                    X = fe.ActualOffset.X,
                    Y = fe.ActualOffset.Y,
                });
            }

            if (child is DependencyObject dp)
                WalkVisualTree(dp, controls, depth + 1);
        }
    }

    public static List<LayoutWarning> ValidateLayout(List<ControlSnapshot> controls)
    {
        var warnings = new List<LayoutWarning>();

        foreach (var c in controls)
        {
            if (c.Visibility != "Visible" && IsRequired(c))
            {
                warnings.Add(new LayoutWarning
                {
                    Target = c.Name,
                    Expected = "Visible",
                    Actual = c.Visibility,
                });
            }

            if (c.Width < 1 && c.Visibility == "Visible" && IsRequired(c))
            {
                warnings.Add(new LayoutWarning
                {
                    Target = c.Name,
                    Expected = "width>=1",
                    Actual = c.Width.ToString("F1"),
                });
            }

            if (c.Height < 1 && c.Visibility == "Visible" && IsRequired(c))
            {
                warnings.Add(new LayoutWarning
                {
                    Target = c.Name,
                    Expected = "height>=1",
                    Actual = c.Height.ToString("F1"),
                });
            }
        }

        return warnings;
    }

    private static bool IsRequired(ControlSnapshot c)
    {
        return !string.IsNullOrWhiteSpace(c.Name);
    }

    public static VerificationResult VerifyLayout(RuntimeSnapshot snapshot, RequirementSpec spec)
    {
        var result = new VerificationResult();

        foreach (var (name, req) in spec.Controls)
        {
            var ctl = snapshot.Controls.Find(c =>
                c.Name == name ||
                c.AutomationId == name);

            if (req.Visible == true && (ctl == null || ctl.Visibility != "Visible"))
            {
                result.LayoutWarnings.Add(new LayoutWarning
                {
                    Target = name,
                    Expected = "visible:true",
                    Actual = ctl?.Visibility ?? "not found",
                });
            }

            if (req.Enabled == true && ctl != null && !ctl.IsEnabled)
            {
                result.LayoutWarnings.Add(new LayoutWarning
                {
                    Target = name,
                    Expected = "enabled:true",
                    Actual = "false",
                });
            }
        }

        foreach (var (name, req) in spec.Layout)
        {
            var ctl = snapshot.Controls.Find(c =>
                c.Name == name ||
                c.AutomationId == name);

            if (req.MinWidth.HasValue && ctl != null && ctl.Width < req.MinWidth.Value)
            {
                result.LayoutWarnings.Add(new LayoutWarning
                {
                    Target = name,
                    Expected = $"width>={req.MinWidth.Value}",
                    Actual = ctl.Width.ToString("F0"),
                });
            }
        }

        if (snapshot.BindingErrors.Count > 0)
        {
            result.BindingErrors.AddRange(snapshot.BindingErrors);
        }

        result.Status = result.BindingErrors.Count == 0
            && result.LayoutWarnings.Count == 0
            && result.AutomationErrors.Count == 0
            ? "PASS" : "FAIL";

        return result;
    }
}
