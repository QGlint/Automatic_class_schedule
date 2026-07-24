using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Windows.ApplicationModel.DataTransfer;
using Automatic_class_schedule.Models;
using Automatic_class_schedule.ViewModels;

namespace Automatic_class_schedule;

public sealed partial class MainPage : Page, Infrastructure.IRuntimeInspectable
{
    public MainPage()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        var vm = (MainViewModel)DataContext;
        if (e.Parameter is string param)
        {
            if (!string.IsNullOrEmpty(param))
            {
                if (param == App.OpenIntent)
                {
                    _ = PickAndOpenProjectAsync();
                    return;
                }
                vm.OpenProject(param);
                return;
            }
        }
    }

    public Infrastructure.RuntimeSnapshot GetRuntimeSnapshot()
    {
        return Infrastructure.RuntimeInspector.Inspect(this);
    }

    private void GradeButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is GradeInput grade)
        {
            ((MainViewModel)DataContext).SelectGradeCommand.Execute(grade);
        }
    }

    private void ClassButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is SchoolClass schoolClass)
        {
            ((MainViewModel)DataContext).SelectClassCommand.Execute(schoolClass);
        }
    }

    private void TeacherButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is Teacher teacher)
        {
            ((MainViewModel)DataContext).SelectTeacherCommand.Execute(teacher);
        }
    }

    private void GradeTabButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is GradeInput grade)
        {
            var vm = (MainViewModel)DataContext;
            vm.SelectGradeCommand.Execute(grade);
            vm.SelectViewModeCommand.Execute("年级总表");
        }
    }

    private void SubjectGradeTabButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is GradeInput grade)
        {
            ((MainViewModel)DataContext).SelectSubjectGradeCommand.Execute(grade.GradeName);
        }
    }

    private void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        ((MainViewModel)DataContext).ImportCommand.Execute(null);
    }

    private void ImportTeacherData_Click(object sender, RoutedEventArgs e)
    {
        ((MainViewModel)DataContext).ImportCommand.Execute(null);
    }

    private async void SaveCourseTemplate_Click(object sender, RoutedEventArgs e)
    {
        var vm = (MainViewModel)DataContext;
        var filePicker = new Windows.Storage.Pickers.FileSavePicker();
        filePicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
        filePicker.FileTypeChoices.Add("课程模板", new List<string> { ".json" });
        filePicker.SuggestedFileName = "课程模板";
        var file = await filePicker.PickSaveFileAsync();
        if (file != null)
        {
            var json = vm.SerializeSubjects();
            await Windows.Storage.FileIO.WriteTextAsync(file, json);
        }
    }

    private async void LoadCourseTemplate_Click(object sender, RoutedEventArgs e)
    {
        var vm = (MainViewModel)DataContext;
        var filePicker = new Windows.Storage.Pickers.FileOpenPicker();
        filePicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
        filePicker.FileTypeFilter.Add(".json");
        var file = await filePicker.PickSingleFileAsync();
        if (file != null)
        {
            var json = await Windows.Storage.FileIO.ReadTextAsync(file);
            vm.DeserializeSubjects(json);
        }
    }

    private void Entry_DragStarting(UIElement sender, DragStartingEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is ScheduleEntry entry)
        {
            e.Data.Properties.Add("ScheduleEntry", entry);
            e.Data.RequestedOperation = DataPackageOperation.Move;
        }
    }

    private void Entry_DragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.Move;
    }

    private void Entry_Drop(object sender, DragEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is ScheduleEntry target)
        {
            if (e.DataView.Properties.TryGetValue("ScheduleEntry", out object value) && value is ScheduleEntry source && source.Id != target.Id)
            {
                ((MainViewModel)DataContext).SwapEntries(source, target);
                e.Handled = true;
            }
        }
    }

    private void NewProject_Click(object sender, RoutedEventArgs e)
    {
        var vm = (MainViewModel)DataContext;
        if (vm.HasActiveProject)
        {
            App.OpenNewWindow();
            return;
        }
        var _ = ShowCreateProjectDialogAsync();
    }

    private async void OpenProject_Click(object sender, RoutedEventArgs e)
    {
        var vm = (MainViewModel)DataContext;
        if (vm.HasActiveProject)
        {
            App.PendingProjectPath = App.OpenIntent;
            App.OpenNewWindow();
            return;
        }

        await PickAndOpenProjectAsync();
    }

    private async Task PickAndOpenProjectAsync()
    {
        var vm = (MainViewModel)DataContext;
        var filePicker = new Windows.Storage.Pickers.FileOpenPicker
        {
            SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary
        };
        filePicker.FileTypeFilter.Add(".acsproj");

        try
        {
            var file = await filePicker.PickSingleFileAsync();
            if (file != null)
            {
                vm.OpenProject(file.Path);
                vm.ProjectFilePath = file.Path;
            }
        }
        catch
        {
            // 文件选择器取消或出错时忽略
        }
    }

    private async void SaveProject_Click(object sender, RoutedEventArgs e)
    {
        var vm = (MainViewModel)DataContext;
        vm.SaveProject(vm.ProjectFilePath);
    }

    private async void RecentProject_Click(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is Services.ProjectInfo info)
        {
            var vm = (MainViewModel)DataContext;
            if (vm.HasActiveProject)
            {
                App.PendingProjectPath = info.Path;
                App.OpenNewWindow();
                return;
            }
            vm.OpenProject(info.Path);
        }
    }

    private async Task ShowCreateProjectDialogAsync()
    {
        var vm = (MainViewModel)DataContext;
        vm.ProjectName = string.Empty;
        vm.ProjectFilePath = Infrastructure.AppPaths.ProjectsPath;

        var nameBox = new TextBox
        {
            PlaceholderText = "例如: 2024年上学期",
            Margin = new Thickness(0, 4, 0, 0)
        };
        var statusText = new TextBlock
        {
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.DarkRed),
            FontSize = 13,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        };

        var dialog = new ContentDialog
        {
            Title = "新建项目",
            PrimaryButtonText = "创建",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
            Content = new StackPanel
            {
                Spacing = 8,
                MinWidth = 360,
                Children =
                {
                    new TextBlock { Text = "项目名称", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                    nameBox,
                    new TextBlock
                    {
                        Text = $"保存到: {Infrastructure.AppPaths.ProjectsPath}",
                        FontSize = 11,
                        Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray)
                    },
                    statusText
                }
            }
        };

        while (true)
        {
            nameBox.Text = "";
            statusText.Text = "";

            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary)
                break;

            vm.ProjectName = nameBox.Text.Trim();

            if (string.IsNullOrEmpty(vm.ProjectName))
            {
                statusText.Text = "请输入项目名称";
                continue;
            }

            var fullPath = Infrastructure.AppPaths.GetProjectFilePath(vm.ProjectName);
            if (System.IO.File.Exists(fullPath))
            {
                statusText.Text = $"文件已存在: {vm.ProjectName}.acsproj";
                continue;
            }

            vm.CreateProject();
            break;
        }
    }

    private void GridCell_DragStarting(UIElement sender, DragStartingEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is ScheduleGridCell cell && cell.Entry != null)
        {
            e.Data.Properties.Add("ScheduleEntry", cell.Entry);
            e.Data.RequestedOperation = DataPackageOperation.Move;
        }
    }

    private void GridCell_DragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.Move;
    }

    private void GridCell_Drop(object sender, DragEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is ScheduleGridCell targetCell && targetCell.Entry != null)
        {
            if (e.DataView.Properties.TryGetValue("ScheduleEntry", out object value) && value is ScheduleEntry source && source.Id != targetCell.Entry.Id)
            {
                ((MainViewModel)DataContext).SwapEntries(source, targetCell.Entry);
                e.Handled = true;
            }
        }
    }
}
