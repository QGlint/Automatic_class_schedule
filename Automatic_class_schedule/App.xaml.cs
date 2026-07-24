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
        // 获取命令行参数（文件关联打开时会传入 .acsproj 文件路径）
        string? projectPath = GetProjectPathFromArgs();

        // 单实例检测：如果已有实例在运行，将项目路径发送给它并退出
        if (!Infrastructure.SingleInstanceService.TryAcquireLock())
        {
            Infrastructure.SingleInstanceService.SendToExistingInstance(projectPath);
            Exit();
            return;
        }

        // 当前是第一个实例，创建窗口
        PendingProjectPath = projectPath;
        CurrentWindow = new MainWindow();
        CurrentWindow.Activate();
        Infrastructure.AppPaths.EnsureDirectories();

        // 启动管道监听，接收后续实例发来的项目路径
        Infrastructure.SingleInstanceService.StartListening(OnProjectReceivedFromAnotherInstance);
    }

    /// <summary>从启动参数中提取项目文件路径</summary>
    private static string? GetProjectPathFromArgs()
    {
        var args = Environment.GetCommandLineArgs();
        if (args.Length > 1)
        {
            // 查找 .acsproj 文件路径参数
            for (int i = 1; i < args.Length; i++)
            {
                if (args[i].EndsWith(".acsproj", StringComparison.OrdinalIgnoreCase)
                    && System.IO.File.Exists(args[i]))
                {
                    return args[i];
                }
            }
        }
        return null;
    }

    /// <summary>处理从另一个实例接收到的项目路径</summary>
    private static void OnProjectReceivedFromAnotherInstance(string? projectPath)
    {
        if (!string.IsNullOrEmpty(projectPath))
        {
            // 尝试激活已打开该项目的窗口，否则在新窗口中打开
            OpenNewWindow(projectPath);
        }
        else
        {
            // 无项目路径，将已有窗口置于前台
            if (CurrentWindow != null)
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(CurrentWindow);
                Infrastructure.WindowManager.BringWindowToFront(hwnd);
            }
        }
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
