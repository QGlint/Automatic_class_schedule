using Microsoft.UI.Xaml;
using Automatic_class_schedule.ViewModels;

namespace Automatic_class_schedule;

public partial class App : Application
{
    public static Window? CurrentWindow { get; private set; }

    /// <summary>为新建窗口预置的项目路径或意图标记</summary>
    internal static string? PendingProjectPath { get; set; }

    /// <summary>新窗口意图：打开项目（传递此标记而非路径）</summary>
    internal const string OpenIntent = "\0OPEN";

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        CurrentWindow = new MainWindow();
        CurrentWindow.Activate();
        Automatic_class_schedule.Infrastructure.AppPaths.EnsureDirectories();
    }

    public static bool OpenNewWindow(string? projectPath = null)
    {
        // 如果指定了项目路径，检查是否已有窗口打开了该项目
        if (!string.IsNullOrEmpty(projectPath))
        {
            if (Infrastructure.WindowManager.TryBringToFront(projectPath))
                return false; // 已激活现有窗口，不创建新窗口
        }

        PendingProjectPath = projectPath;
        var window = new MainWindow();
        CurrentWindow = window;
        window.Activate();
        return true;
    }
}
