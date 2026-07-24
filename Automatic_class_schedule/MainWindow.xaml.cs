using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Navigation;

namespace Automatic_class_schedule;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        var param = App.PendingProjectPath;
        App.PendingProjectPath = null;
        RootFrame.Navigate(typeof(MainPage), param);
    }
}
