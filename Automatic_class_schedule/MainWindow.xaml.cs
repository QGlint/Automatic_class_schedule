using Microsoft.UI.Xaml;

namespace Automatic_class_schedule;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        RootFrame.Navigate(typeof(MainPage));
    }
}
