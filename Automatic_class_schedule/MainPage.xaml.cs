using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.ApplicationModel.DataTransfer;
using Automatic_class_schedule.Models;
using Automatic_class_schedule.ViewModels;

namespace Automatic_class_schedule;

public sealed partial class MainPage : Page, Infrastructure.IRuntimeInspectable
{
    private nint _windowHandle;

    public MainPage()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
        Loaded += (_, _) => InitWindowHandle();
    }

    private void InitWindowHandle()
    {
        DependencyObject current = this;
        while (current != null)
        {
            if (current is Window window)
            {
                _windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(window);
                if (DataContext is MainViewModel vm)
                    vm.WindowHandle = _windowHandle;
                return;
            }
            current = VisualTreeHelper.GetParent(current);
        }
        _windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(App.CurrentWindow!);
        if (DataContext is MainViewModel vm2)
            vm2.WindowHandle = _windowHandle;
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

    private async void NewProject_Click(object sender, RoutedEventArgs e)
    {
        var vm = (MainViewModel)DataContext;
        await ShowCreateProjectDialogAsync(openInNewWindow: vm.HasActiveProject);
    }

    private async void OpenProject_Click(object sender, RoutedEventArgs e)
    {
        var vm = (MainViewModel)DataContext;
        await PickAndOpenProjectAsync(openInNewWindow: vm.HasActiveProject);
    }

    private async Task PickAndOpenProjectAsync(bool openInNewWindow = false)
    {
        var vm = (MainViewModel)DataContext;

        // 先尝试文件夹选择器（v2 目录格式项目）
        var folderPicker = new Windows.Storage.Pickers.FolderPicker
        {
            SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary
        };
        folderPicker.FileTypeFilter.Add("*");

        try
        {
            WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, _windowHandle);
            var folder = await folderPicker.PickSingleFolderAsync();
            if (folder != null)
            {
                string path = folder.Path;
                // 检查是否为有效项目目录（包含 project.acs）
                string mainFile = Infrastructure.AppPaths.GetProjectMainFile(path);
                if (System.IO.File.Exists(mainFile))
                {
                    // v2 目录格式
                    if (openInNewWindow)
                        App.OpenNewWindow(path);
                    else
                        vm.OpenProject(path);
                    return;
                }
                // 可能是旧版单文件 .acsproj
                if (path.EndsWith(".acsproj", StringComparison.OrdinalIgnoreCase) && System.IO.File.Exists(path))
                {
                    if (openInNewWindow)
                        App.OpenNewWindow(path);
                    else
                        vm.OpenProject(path);
                    return;
                }
            }
        }
        catch
        {
        }
    }

    private async void SaveProject_Click(object sender, RoutedEventArgs e)
    {
        var vm = (MainViewModel)DataContext;
        vm.SaveProject(vm.ProjectFilePath);
    }

    private async void CloseProject_Click(object sender, RoutedEventArgs e)
    {
        var vm = (MainViewModel)DataContext;
        if (vm.HasUnsavedChanges)
        {
            var dialog = new ContentDialog
            {
                Title = "关闭项目",
                Content = "项目已修改，是否保存？",
                PrimaryButtonText = "保存",
                SecondaryButtonText = "不保存",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot
            };
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.None)
                return;
            if (result == ContentDialogResult.Primary)
                vm.SaveProject(vm.ProjectFilePath);
        }
        vm.CloseProject();
    }

    private async void RecentProject_Click(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is Services.ProjectInfo info)
        {
            try
            {
                var vm = (MainViewModel)DataContext;
                if (!System.IO.File.Exists(info.Path) && !System.IO.Directory.Exists(info.Path))
                {
                    return;
                }
                if (vm.HasActiveProject)
                {
                    App.OpenNewWindow(info.Path);
                    return;
                }
                vm.OpenProject(info.Path);
            }
            catch { }
        }
    }

    private async void OpenRecentProject_Click(object sender, RoutedEventArgs e)
    {
        var vm = (MainViewModel)DataContext;
        var list = new ListView
        {
            ItemsSource = vm.HomePageProjects,
            SelectionMode = ListViewSelectionMode.None,
            IsItemClickEnabled = true,
            Height = 360,
            MinWidth = 400
        };
        list.ItemTemplate = (Microsoft.UI.Xaml.DataTemplate)Resources["RecentProjectItemTemplate"];

        ContentDialog? dialog = null;
        list.ItemClick += (s, args) =>
        {
            if (args.ClickedItem is Services.ProjectInfo info && (System.IO.File.Exists(info.Path) || System.IO.Directory.Exists(info.Path)))
            {
                App.OpenNewWindow(info.Path);
                dialog?.Hide();
            }
        };

        dialog = new ContentDialog
        {
            Title = "打开最近项目",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
            Content = list
        };

        await dialog.ShowAsync();
    }

    private async Task ShowCreateProjectDialogAsync(bool openInNewWindow = false)
    {
        var vm = (MainViewModel)DataContext;
        vm.ProjectName = string.Empty;
        var saveFolder = Infrastructure.AppPaths.ProjectsPath;
        var nameText = "";
        var errorText = "";

        while (true)
        {
            var nameBox = new TextBox
            {
                PlaceholderText = "例如: 2024年上学期",
                Margin = new Thickness(0, 4, 0, 0),
                Text = nameText
            };
            var statusText = new TextBlock
            {
                Text = errorText,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.DarkRed),
                FontSize = 13,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            };
            var pathText = new TextBlock
            {
                Text = saveFolder,
                FontSize = 11,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };
            var browseBtn = new Button
            {
                Content = "选择位置",
                FontSize = 11,
                Padding = new Thickness(8, 2, 8, 2)
            };

            var browseClicked = false;
            ContentDialog? dialog = null;
            browseBtn.Click += (_, _) =>
            {
                browseClicked = true;
                nameText = nameBox.Text;
                dialog?.Hide();
            };

            dialog = new ContentDialog
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
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            Spacing = 6,
                            Children =
                            {
                                new TextBlock { Text = "保存到:", FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray) },
                                pathText,
                                browseBtn
                            }
                        },
                        statusText
                    }
                }
            };

            var result = await dialog.ShowAsync();

            if (browseClicked)
            {
                var folderPicker = new Windows.Storage.Pickers.FolderPicker
                {
                    SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary
                };
                folderPicker.FileTypeFilter.Add("*");
                WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, _windowHandle);
                var folder = await folderPicker.PickSingleFolderAsync();
                if (folder != null)
                {
                    saveFolder = folder.Path;
                    nameText = nameBox.Text;
                }
                errorText = "";
                continue;
            }

            if (result != ContentDialogResult.Primary)
                break;

            nameText = nameBox.Text.Trim();
            if (string.IsNullOrEmpty(nameText))
            {
                errorText = "请输入项目名称";
                continue;
            }

            var fullPath = System.IO.Path.Combine(saveFolder, nameText + ".acsproj");
            if (System.IO.Directory.Exists(fullPath))
            {
                errorText = $"项目已存在: {nameText}.acsproj";
                continue;
            }

            if (openInNewWindow)
            {
                CreateDefaultProjectFile(fullPath);
                App.OpenNewWindow(fullPath);
            }
            else
            {
                vm.ProjectName = nameText;
                vm.CreateProject(fullPath);
            }
            break;
        }
    }

    private static void CreateDefaultProjectFile(string path)
    {
        // path 是 .acsproj 目录路径
        System.IO.Directory.CreateDirectory(path);
        string cacheDir = Infrastructure.AppPaths.GetProjectCacheDir(path);
        System.IO.Directory.CreateDirectory(cacheDir);

        string projectName = System.IO.Path.GetFileNameWithoutExtension(path);

        var grades = new System.Collections.Generic.List<Models.GradeInput>
        {
            new() { GradeName = "七年级", ClassCount = 8 },
            new() { GradeName = "八年级", ClassCount = 8 },
            new() { GradeName = "九年级", ClassCount = 6 }
        };

        var data = new Models.SchoolData
        {
            ProjectName = projectName,
            Settings = new Models.ScheduleSettings
            {
                DaysPerWeek = 5,
                PeriodsPerDay = 7,
                MorningPeriods = 4,
                AfternoonPeriods = 3,
                IncludeEveningSelfStudy = false,
                EveningPeriods = 2
            },
            GradeInputs = grades
        };

        foreach (var g in grades)
        {
            string grade = g.GradeName;
            data.Subjects.Add(new Models.SubjectDefinition { Name = "语文", Category = "主科", DefaultWeeklyCount = 6, DistributionRule = "每天一次", GradeName = grade });
            data.Subjects.Add(new Models.SubjectDefinition { Name = "数学", Category = "主科", DefaultWeeklyCount = 6, DistributionRule = "每天一次", GradeName = grade });
            data.Subjects.Add(new Models.SubjectDefinition { Name = "英语", Category = "主科", DefaultWeeklyCount = 5, DistributionRule = "每天一次", GradeName = grade });
            if (grade != "七年级")
                data.Subjects.Add(new Models.SubjectDefinition { Name = "物理", Category = "理科", DefaultWeeklyCount = 3, DistributionRule = "均衡分布", GradeName = grade });
            if (grade != "七年级" && grade != "八年级")
                data.Subjects.Add(new Models.SubjectDefinition { Name = "化学", Category = "理科", DefaultWeeklyCount = 3, DistributionRule = "均衡分布", GradeName = grade });
            if (grade != "九年级")
            {
                data.Subjects.Add(new Models.SubjectDefinition { Name = "生物", Category = "理科", DefaultWeeklyCount = 2, DistributionRule = "均衡分布", GradeName = grade });
                data.Subjects.Add(new Models.SubjectDefinition { Name = "地理", Category = "文科", DefaultWeeklyCount = 2, DistributionRule = "均衡分布", GradeName = grade });
            }
            data.Subjects.Add(new Models.SubjectDefinition { Name = "历史", Category = "文科", DefaultWeeklyCount = 2, DistributionRule = "均衡分布", GradeName = grade });
            data.Subjects.Add(new Models.SubjectDefinition { Name = "政治", Category = "文科", DefaultWeeklyCount = 2, DistributionRule = "均衡分布", GradeName = grade });
            data.Subjects.Add(new Models.SubjectDefinition { Name = "体育", Category = "副科", DefaultWeeklyCount = 2, DistributionRule = "均衡分布", GradeName = grade });
        }

        foreach (var g in grades)
        {
            for (int i = 1; i <= g.ClassCount; i++)
                data.Classes.Add(new Models.SchoolClass
                {
                    Id = Guid.NewGuid(),
                    GradeName = g.GradeName,
                    ClassNumber = i,
                    Name = $"{g.GradeName.Replace("年级", "")}{i}班"
                });
        }

        Infrastructure.SchoolDataSerializer.SerializeToDirectory(path, data, projectName);
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
