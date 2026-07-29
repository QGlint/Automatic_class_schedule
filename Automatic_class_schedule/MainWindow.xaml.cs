using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Automatic_class_schedule.ViewModels;
using System.Runtime.InteropServices;

namespace Automatic_class_schedule;

public sealed partial class MainWindow : Window
{
    private bool _isClosing;
    private nint _hwnd;

    /// <summary>HWND → MainWindow 实例映射，供 MainPage 查找所属窗口</summary>
    private static readonly Dictionary<nint, MainWindow> _instances = new();

    /// <summary>根据 HWND 获取对应的 MainWindow 实例</summary>
    internal static MainWindow? GetByHwnd(nint hwnd)
    {
        _instances.TryGetValue(hwnd, out var window);
        return window;
    }

    /// <summary>当前窗口关联的项目路径（用于 WindowManager 注册）</summary>
    internal string? CurrentProjectPath { get; set; }

    public MainWindow()
    {
        InitializeComponent();
        var param = App.PendingProjectPath;
        App.PendingProjectPath = null;

        _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        _instances[_hwnd] = this;

        // 注册项目与窗口的关联
        if (!string.IsNullOrEmpty(param) && param != App.OpenIntent)
        {
            CurrentProjectPath = param;
            Infrastructure.WindowManager.RegisterProject(_hwnd, param);
        }

        RootFrame.Navigate(typeof(MainPage), param);

        var appWindow = GetAppWindow();
        appWindow.Closing += OnWindowClosing;

        // 设置窗口图标（从 EXE 嵌入资源加载，ApplicationIcon 已将 ACS.ico 编入）
        try
        {
            nint hModule = GetModuleHandleW(null);
            // 加载自定义图标资源（MAKEINTRESOURCE(1)），失败则回退默认应用图标
            nint hIcon = LoadImageW(hModule, "#1", 1, 0, 0, 0x00000040); // IMAGE_ICON=1, LR_DEFAULTSIZE=0x40
            if (hIcon == 0)
                hIcon = LoadIconW(hModule, 32512); // IDI_APPLICATION
            if (hIcon != 0)
            {
                const int WM_SETICON = 0x0080;
                SendMessageW(_hwnd, WM_SETICON, 1, hIcon); // ICON_BIG=1
                SendMessageW(_hwnd, WM_SETICON, 0, hIcon); // ICON_SMALL=0
            }
        }
        catch { }
    }

    /// <summary>更新当前窗口关联的项目路径</summary>
    internal void UpdateProjectRegistration(string? newProjectPath)
    {
        if (!string.IsNullOrEmpty(newProjectPath))
        {
            CurrentProjectPath = newProjectPath;
            Infrastructure.WindowManager.RegisterProject(_hwnd, newProjectPath);
        }
        else
        {
            Infrastructure.WindowManager.UnregisterProject(_hwnd);
            CurrentProjectPath = null;
        }
    }

    private async void OnWindowClosing(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowClosingEventArgs args)
    {
        // 窗口关闭时注销项目注册并移除实例映射
        Infrastructure.WindowManager.UnregisterProject(_hwnd);
        _instances.Remove(_hwnd);

        if (_isClosing) return;

        if (RootFrame.Content is not MainPage page || page.DataContext is not MainViewModel vm)
            return;

        if (!vm.HasUnsavedChanges)
            return;

        args.Cancel = true;
        _isClosing = true;

        var dialog = new ContentDialog
        {
            Title = "关闭项目",
            Content = "项目已修改，是否保存？",
            PrimaryButtonText = "保存",
            SecondaryButtonText = "不保存",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = page.XamlRoot
        };
        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
            vm.SaveProject(vm.ProjectFilePath);
        if (result != ContentDialogResult.None)
            Close();

        _isClosing = false;
    }

    private Microsoft.UI.Windowing.AppWindow GetAppWindow()
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        return Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandleW(string? lpModuleName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint LoadIconW(nint hInstance, int lpIconName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint LoadImageW(nint hInstance, string lpName, int uType, int cx, int cy, int fuLoad);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint SendMessageW(nint hWnd, int Msg, nint wParam, nint lParam);
}
