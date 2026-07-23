using Microsoft.UI.Xaml;

namespace Automatic_class_schedule;

public partial class App : Application
{
    public static Window? CurrentWindow { get; private set; }

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        CurrentWindow = new MainWindow();
        CurrentWindow.Activate();
    }
}
