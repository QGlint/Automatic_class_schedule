using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Automatic_class_schedule.ViewModels;

namespace Automatic_class_schedule;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        var param = App.PendingProjectPath;
        App.PendingProjectPath = null;
        RootFrame.Navigate(typeof(MainPage), param);

        var appWindow = GetAppWindow();
        appWindow.Closing += OnWindowClosing;
    }

    private async void OnWindowClosing(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowClosingEventArgs args)
    {
        if (RootFrame.Content is not MainPage page || page.DataContext is not MainViewModel vm)
            return;

        if (!vm.HasUnsavedChanges)
            return;

        args.Cancel = true;

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
    }

    private Microsoft.UI.Windowing.AppWindow GetAppWindow()
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        return Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
    }
}
