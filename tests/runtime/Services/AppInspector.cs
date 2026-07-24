using FlaUI.UIA3;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using WinUIVerify.Models;

namespace WinUIVerify.Services;

public sealed class AppInspector : IDisposable
{
    private readonly UIA3Automation _automation;
    private AutomationElement? _appRoot;

    public AppInspector()
    {
        _automation = new UIA3Automation();
    }

    public bool ConnectToProcess(string processName, int timeoutMs = 10000)
    {
        var startTime = DateTime.UtcNow;

        while ((DateTime.UtcNow - startTime).TotalMilliseconds < timeoutMs)
        {
            var processes = System.Diagnostics.Process.GetProcessesByName(processName);
            foreach (var proc in processes)
            {
                try
                {
                    var app = _automation.GetDesktop().FindFirstByXPath($"//Window[starts-with(@Name, 'Automatic')]");
                    if (app != null)
                    {
                        _appRoot = app;
                        return true;
                    }
                }
                catch
                {
                    // process might not be ready yet
                }
            }

            Thread.Sleep(500);
        }

        return false;
    }

    public RuntimeSnapshot CaptureSnapshot()
    {
        var snapshot = new RuntimeSnapshot
        {
            Page = "MainPage",
            Loaded = _appRoot != null,
            Timestamp = DateTime.UtcNow.ToString("O"),
        };

        if (_appRoot == null)
            return snapshot;

        WalkTree(_appRoot, snapshot.Controls, "");

        return snapshot;
    }

    private void WalkTree(AutomationElement element, List<ControlSnapshot> controls, string indent)
    {
        try
        {
            var name = element.Name ?? "";
            var autoId = element.AutomationId ?? "";
            var type = element.ControlType.ToString() ?? "Unknown";

            if (!string.IsNullOrWhiteSpace(name) || !string.IsNullOrWhiteSpace(autoId))
            {
                controls.Add(new ControlSnapshot
                {
                    Type = type,
                    Name = name,
                    AutomationId = autoId,
                    Visibility = element.IsOffscreen ? "Collapsed" : "Visible",
                    IsEnabled = element.IsEnabled,
                    Width = element.BoundingRectangle.Width,
                    Height = element.BoundingRectangle.Height,
                    X = element.BoundingRectangle.X,
                    Y = element.BoundingRectangle.Y,
                });
            }

            var children = element.FindAllChildren();
            foreach (var child in children)
            {
                WalkTree(child, controls, indent + "  ");
            }
        }
        catch
        {
            // skip inaccessible elements
        }
    }

    public ScreenshotResult? CaptureScreenshot(string savePath)
    {
        if (_appRoot == null) return null;

        try
        {
            var rect = _appRoot.BoundingRectangle;
            using var bitmap = new System.Drawing.Bitmap((int)rect.Width, (int)rect.Height);
            using var g = System.Drawing.Graphics.FromImage(bitmap);
            g.CopyFromScreen((int)rect.X, (int)rect.Y, 0, 0, bitmap.Size);

            bitmap.Save(savePath, System.Drawing.Imaging.ImageFormat.Png);

            return new ScreenshotResult
            {
                Path = savePath,
                Width = bitmap.Width,
                Height = bitmap.Height,
            };
        }
        catch
        {
            return null;
        }
    }

    public bool ClickButton(string automationId)
    {
        if (_appRoot == null) return false;

        try
        {
            var btn = _appRoot.FindFirstDescendant(cf =>
                cf.ByAutomationId(automationId).Or(cf.ByName(automationId)));

            if (btn != null)
            {
                btn.Click();
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    public bool SetText(string automationId, string text)
    {
        if (_appRoot == null) return false;

        try
        {
            var ctl = _appRoot.FindFirstDescendant(cf =>
                cf.ByAutomationId(automationId).Or(cf.ByName(automationId)));

            if (ctl != null)
            {
                ctl.Focus();
                if (ctl.Patterns.Value.Pattern is { } valuePattern)
                    valuePattern.SetValue(text);
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        _automation?.Dispose();
    }
}

public sealed class ScreenshotResult
{
    public string Path { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
}
