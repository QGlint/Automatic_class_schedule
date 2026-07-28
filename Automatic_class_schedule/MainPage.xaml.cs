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

        // 订阅进度弹窗事件
        if (DataContext is MainViewModel vm)
        {
            vm.RequestOpenProgressDialog += OnRequestOpenProgressDialog;
            vm.RequestCloseProgressDialog += OnRequestCloseProgressDialog;
            vm.RequestShowMessage += OnRequestShowMessage;
            vm.RequestShowExportSuccess += OnRequestShowExportSuccess;
        }
    }

    private void InitWindowHandle()
    {
        // WinUI 3 中 Window 不继承 DependencyObject，无法通过 VisualTree 获取
        // 使用 App.CurrentWindow（Loaded 事件在窗口创建后同步触发，此时 CurrentWindow 正确）
        _windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(App.CurrentWindow!);
        if (DataContext is MainViewModel vm)
            vm.WindowHandle = _windowHandle;
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
                UpdateWindowRegistration(param);
                return;
            }
        }
    }

    /// <summary>通知父窗口更新项目注册</summary>
    private void UpdateWindowRegistration(string? projectPath)
    {
        if (MainWindow.GetByHwnd(_windowHandle) is MainWindow mainWindow)
            mainWindow.UpdateProjectRegistration(projectPath);
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

    private void FixedTimeTabButton_Click(object sender, RoutedEventArgs e)
    {
        ((MainViewModel)DataContext).SelectSubjectGradeCommand.Execute("固定时间");
    }

    private async void SaveTemplateButton_Click(object sender, RoutedEventArgs e)
    {
        var vm = (MainViewModel)DataContext;
        var nameBox = new TextBox { PlaceholderText = "输入配置名称", MinWidth = 260 };

        var dialog = new ContentDialog
        {
            Title = "保存课程配置",
            PrimaryButtonText = "保存",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
            Content = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock { Text = "配置名称" },
                    nameBox
                }
            }
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            string name = nameBox.Text.Trim();
            if (!string.IsNullOrEmpty(name))
                vm.SaveCourseTemplate(name);
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

        // 使用文件选择器打开 .acsproj 项目文件
        var filePicker = new Windows.Storage.Pickers.FileOpenPicker
        {
            SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary
        };
        filePicker.FileTypeFilter.Add(".acsproj");

        try
        {
            WinRT.Interop.InitializeWithWindow.Initialize(filePicker, _windowHandle);
            var file = await filePicker.PickSingleFileAsync();
            if (file != null)
            {
                string path = file.Path;
                if (System.IO.File.Exists(path))
                {
                    if (openInNewWindow)
                        App.OpenNewWindow(path);
                    else
                    {
                        vm.OpenProject(path);
                        UpdateWindowRegistration(path);
                    }
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
        UpdateWindowRegistration(null);
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
                UpdateWindowRegistration(info.Path);
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

            var fullPath = System.IO.Path.Combine(saveFolder, nameText, nameText + ".acsproj");
            if (System.IO.File.Exists(fullPath))
            {
                errorText = $"项目已存在: {nameText}";
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

    private static void CreateDefaultProjectFile(string acsprojFilePath)
    {
        // acsprojFilePath 是 .acsproj 文件路径，如 C:/.../MyProject/MyProject.acsproj
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(acsprojFilePath)!);

        string projectName = System.IO.Path.GetFileNameWithoutExtension(acsprojFilePath);

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
            data.Subjects.Add(new Models.SubjectDefinition { Name = "道德", Category = "文科", DefaultWeeklyCount = 2, DistributionRule = "均衡分布", GradeName = grade });
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

        Infrastructure.SchoolDataSerializer.SerializeToDirectory(acsprojFilePath, data, projectName);
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
        if (sender is FrameworkElement element && element.DataContext is ScheduleGridCell targetCell)
        {
            if (e.DataView.Properties.TryGetValue("ScheduleEntry", out object value) && value is ScheduleEntry source)
            {
                var vm = (MainViewModel)DataContext;
                ScheduleEntry? targetEntry = targetCell.Entry != null && targetCell.Entry.Id != source.Id
                    ? targetCell.Entry
                    : null;

                if (targetEntry != null || targetCell.IsEmpty)
                {
                    _ = vm.DragRescheduleAsync(source, targetCell.DayIndex, targetCell.PeriodIndex, targetEntry, targetCell.ClassName);
                    e.Handled = true;
                }
            }
        }
    }

    private void ScheduleCell_RightTapped(object sender, Microsoft.UI.Xaml.Input.RightTappedRoutedEventArgs e)
    {
        if (sender is not FrameworkElement element) return;
        if (element.DataContext is not ScheduleGridCell cell) return;
        if (cell.Entry == null || cell.IsEmpty) return;

        var vm = (MainViewModel)DataContext;
        bool isProtected = cell.Entry.Note == "手动调整";

        var flyout = new MenuFlyout();
        var item = new MenuFlyoutItem
        {
            Text = isProtected ? "取消保留（恢复普通状态）" : "标记为保留（局部调整不变）"
        };
        item.Click += (_, _) =>
        {
            vm.ToggleEntryProtected(cell.Entry!);
        };
        flyout.Items.Add(item);
        flyout.ShowAt(element, e.GetPosition(element));
        e.Handled = true;
    }

    // ===== 基础设置页事件处理 =====

    private void EveningDayButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is DayToggleItem item)
        {
            var vm = (MainViewModel)DataContext;
            vm.ToggleEveningDayCommand.Execute(item.Index);
        }
    }

    private void GradeEveningDayButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is DayToggleItem item)
        {
            var vm = (MainViewModel)DataContext;
            vm.ToggleGradeEveningDayCommand.Execute(item.Index.ToString());
        }
    }

    private void SettingsTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button clickedButton && clickedButton.Tag is string tagStr && int.TryParse(tagStr, out int tabIndex))
        {
            var vm = (MainViewModel)DataContext;
            vm.SelectSettingsTabCommand.Execute(tabIndex);
            UpdateSettingsTabVisuals(tabIndex);
        }
    }

    private void UpdateSettingsTabVisuals(int activeIndex)
    {
        var darkBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0x21, 0x4E, 0x78));
        var lightBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0xF0, 0xF5, 0xFF));
        var whiteBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0xFF, 0xFF, 0xFF));

        Button[] tabs = { TabBtnGlobal, TabBtnGrade7, TabBtnGrade8, TabBtnGrade9 };
        for (int i = 0; i < tabs.Length; i++)
        {
            if (i == activeIndex)
            {
                tabs[i].Background = darkBrush;
                tabs[i].Foreground = whiteBrush;
            }
            else
            {
                tabs[i].Background = lightBrush;
                tabs[i].Foreground = darkBrush;
            }
        }
    }

    // ===== 排课进度弹窗事件 =====

    private async void OnRequestOpenProgressDialog()
    {
        ScheduleProgressDialog.PrimaryButtonClick -= OnProgressDialogCancel;
        ScheduleProgressDialog.CloseButtonClick -= OnProgressDialogConfirm;
        ScheduleProgressDialog.PrimaryButtonClick += OnProgressDialogCancel;
        ScheduleProgressDialog.CloseButtonClick += OnProgressDialogConfirm;
        await ScheduleProgressDialog.ShowAsync();
    }

    private void OnRequestCloseProgressDialog()
    {
        ScheduleProgressDialog.Hide();
    }

    private async void OnRequestShowMessage(string title, string content)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = content,
            CloseButtonText = "确认",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot
        };
        await dialog.ShowAsync();
    }

    private async void OnRequestShowExportSuccess(string exportPath)
    {
        var dialog = new ContentDialog
        {
            Title = "导出成功",
            Content = $"课表已导出到：\n{exportPath}",
            PrimaryButtonText = "打开输出位置",
            CloseButtonText = "确认",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot
        };
        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            try
            {
                string dir = System.IO.Path.GetDirectoryName(exportPath) ?? exportPath;
                if (System.IO.Directory.Exists(dir))
                    System.Diagnostics.Process.Start("explorer.exe", dir);
            }
            catch { }
        }
    }

    private void OnProgressDialogCancel(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        // 取消排课
        if (DataContext is MainViewModel vm)
        {
            vm.CancelCommand.Execute(null);
        }
    }

    private void OnProgressDialogConfirm(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        // 未完成时禁止关闭
        if (DataContext is MainViewModel vm && !vm.DialogIsComplete)
        {
            args.Cancel = true;
            return;
        }
        // 完成后确认关闭
        if (DataContext is MainViewModel vm2)
        {
            vm2.ConfirmProgressDialog();
        }
    }
}
