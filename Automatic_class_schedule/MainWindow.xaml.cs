using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Automatic_class_schedule.ViewModels;

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

        // 设置窗口图标
        try
        {
            string iconPath = System.IO.Path.Combine(
                Windows.ApplicationModel.Package.Current.InstalledLocation.Path, "Assets", "ACS.ico");
            if (System.IO.File.Exists(iconPath))
                appWindow.SetIcon(iconPath);
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
}
