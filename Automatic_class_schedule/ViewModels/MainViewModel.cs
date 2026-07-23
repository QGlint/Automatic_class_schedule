using System.Collections.ObjectModel;
using System.IO;
using Automatic_class_schedule.Infrastructure;
using Automatic_class_schedule.Models;
using Automatic_class_schedule.Services;
using Automatic_class_schedule.Solver;
using Microsoft.Win32;

namespace Automatic_class_schedule.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly ScheduleService _scheduleService;
    private readonly SchoolDataStore _store;
    private readonly ExcelScheduleService _excelService;
    private string _schoolName = "中学";
    private int _daysPerWeek = 5;
    private int _periodsPerDay = 7;
    private int _morningPeriods = 4;
    private int _afternoonPeriods = 3;
    private string _selectedMainPage = "配置";
    private string _selectedConfigPage = "基础设置";
    private string _selectedViewMode = "年级总表";
    private GradeInput? _selectedGradeInput;
    private SchoolClass? _selectedClass;
    private Teacher? _selectedTeacher;
    private SubjectDefinition? _selectedSubject;
    private ScheduleEntry? _selectedScheduleEntry;
    private LessonRequirement? _selectedRequirement;
    private FixedLesson? _selectedFixedLesson;
    private TeacherAssignment? _selectedTeacherAssignment;
    private string _statusMessage = "就绪";

    public MainViewModel()
    {
        _scheduleService = new ScheduleService(new GreedyScheduleSolver(), new ConflictService());
        _store = new SchoolDataStore();
        _excelService = new ExcelScheduleService();

        ConfigPages = new ObservableCollection<string>
        {
            "基础设置",
            "班级配置",
            "课程配置",
            "教师配置",
            "固定课程",
            "自动排课"
        };

        MainPages = new ObservableCollection<string>
        {
            "配置",
            "课表",
            "导出"
        };

        GradeInputs = new ObservableCollection<GradeInput>();
        Classes = new ObservableCollection<SchoolClass>();
        Teachers = new ObservableCollection<Teacher>();
        Subjects = new ObservableCollection<SubjectDefinition>();
        TeacherAssignments = new ObservableCollection<TeacherAssignment>();
        Requirements = new ObservableCollection<LessonRequirement>();
        FixedLessons = new ObservableCollection<FixedLesson>();
        ScheduleEntries = new ObservableCollection<ScheduleEntry>();
        VisibleScheduleEntries = new ObservableCollection<ScheduleEntry>();
        Conflicts = new ObservableCollection<ScheduleConflict>();
        ActivityLog = new ObservableCollection<string>();
        TimetableDays = new ObservableCollection<ScheduleRowViewModel>();

        SeedSampleDataCommand = new RelayCommand(LoadSampleData);
        GenerateClassesCommand = new RelayCommand(GenerateClasses);
        GenerateRequirementsCommand = new RelayCommand(GenerateRequirements);
        GenerateAssignmentsCommand = new RelayCommand(GenerateAssignments);
        AutoScheduleCommand = new RelayCommand(AutoSchedule);
        ValidateCommand = new RelayCommand(ValidateSchedule);
        SaveCommand = new RelayCommand(SaveData);
        LoadCommand = new RelayCommand(LoadData);
        NewProjectCommand = new RelayCommand(NewProject);
        ExportCommand = new RelayCommand(ExportExcel);
        ImportCommand = new RelayCommand(ImportExcel);
        RefreshViewCommand = new RelayCommand(RefreshViews);
        UseFiveDayCommand = new RelayCommand(() => SetDaysPerWeek(5));
        UseSevenDayCommand = new RelayCommand(() => SetDaysPerWeek(7));
        SelectMainPageCommand = new RelayCommand<string>(SetMainPage);
        SelectConfigPageCommand = new RelayCommand<string>(SetConfigPage);
        SelectViewModeCommand = new RelayCommand<string>(SetViewMode);
        SelectGradeCommand = new RelayCommand<GradeInput>(SelectGrade);
        SelectClassCommand = new RelayCommand<SchoolClass>(SelectClass);
        SelectTeacherCommand = new RelayCommand<Teacher>(SelectTeacher);

        LoadData();
        if (GradeInputs.Count == 0)
        {
            InitDefaultGrades();
            InitDefaultSubjects();
        }
        else
        {
            RefreshViews();
        }

        SelectedMainPage = "配置";
        SelectedConfigPage = "基础设置";
    }

    private void InitDefaultGrades()
    {
        GradeInputs.Add(new GradeInput { GradeName = "七年级", ClassCount = 8 });
        GradeInputs.Add(new GradeInput { GradeName = "八年级", ClassCount = 8 });
        GradeInputs.Add(new GradeInput { GradeName = "九年级", ClassCount = 6 });
    }

    private void InitDefaultSubjects()
    {
        Subjects.Add(new SubjectDefinition { Name = "语文", Category = "主科", DefaultWeeklyCount = 6 });
        Subjects.Add(new SubjectDefinition { Name = "数学", Category = "主科", DefaultWeeklyCount = 6 });
        Subjects.Add(new SubjectDefinition { Name = "英语", Category = "主科", DefaultWeeklyCount = 5 });
        Subjects.Add(new SubjectDefinition { Name = "物理", Category = "理科", DefaultWeeklyCount = 4 });
        Subjects.Add(new SubjectDefinition { Name = "化学", Category = "理科", DefaultWeeklyCount = 3 });
        Subjects.Add(new SubjectDefinition { Name = "生物", Category = "理科", DefaultWeeklyCount = 3 });
        Subjects.Add(new SubjectDefinition { Name = "历史", Category = "文科", DefaultWeeklyCount = 3 });
        Subjects.Add(new SubjectDefinition { Name = "地理", Category = "文科", DefaultWeeklyCount = 3 });
        Subjects.Add(new SubjectDefinition { Name = "政治", Category = "文科", DefaultWeeklyCount = 3 });
        Subjects.Add(new SubjectDefinition { Name = "体育", Category = "副科", DefaultWeeklyCount = 3 });
    }

    public ObservableCollection<string> ConfigPages { get; }
    public ObservableCollection<string> MainPages { get; }
    public ObservableCollection<GradeInput> GradeInputs { get; }
    public ObservableCollection<SchoolClass> Classes { get; }
    public ObservableCollection<Teacher> Teachers { get; }
    public ObservableCollection<SubjectDefinition> Subjects { get; }
    public ObservableCollection<TeacherAssignment> TeacherAssignments { get; }
    public ObservableCollection<LessonRequirement> Requirements { get; }
    public ObservableCollection<FixedLesson> FixedLessons { get; }
    public ObservableCollection<ScheduleEntry> ScheduleEntries { get; }
    public ObservableCollection<ScheduleEntry> VisibleScheduleEntries { get; }
    public ObservableCollection<ScheduleConflict> Conflicts { get; }
    public ObservableCollection<string> ActivityLog { get; }
    public ObservableCollection<ScheduleRowViewModel> TimetableDays { get; private set; }

    public string SchoolName
    {
        get => _schoolName;
        set => SetProperty(ref _schoolName, value);
    }

    public int DaysPerWeek
    {
        get => _daysPerWeek;
        set
        {
            if (SetProperty(ref _daysPerWeek, value))
            {
                OnPropertyChanged(nameof(CurrentScopeSummary));
                RefreshViews();
            }
        }
    }

    public int PeriodsPerDay
    {
        get => _periodsPerDay;
        set
        {
            if (SetProperty(ref _periodsPerDay, value))
            {
                OnPropertyChanged(nameof(CurrentScopeSummary));
                RefreshViews();
            }
        }
    }

    public int MorningPeriods
    {
        get => _morningPeriods;
        set
        {
            if (SetProperty(ref _morningPeriods, value))
            {
                if (_morningPeriods + _afternoonPeriods != _periodsPerDay)
                {
                    PeriodsPerDay = _morningPeriods + _afternoonPeriods;
                }
            }
        }
    }

    public int AfternoonPeriods
    {
        get => _afternoonPeriods;
        set
        {
            if (SetProperty(ref _afternoonPeriods, value))
            {
                if (_morningPeriods + _afternoonPeriods != _periodsPerDay)
                {
                    PeriodsPerDay = _morningPeriods + _afternoonPeriods;
                }
            }
        }
    }

    public string SelectedMainPage
    {
        get => _selectedMainPage;
        set
        {
            if (SetProperty(ref _selectedMainPage, value))
            {
                OnPropertyChanged(nameof(CurrentScopeSummary));
            }
        }
    }

    public string SelectedConfigPage
    {
        get => _selectedConfigPage;
        set => SetProperty(ref _selectedConfigPage, value);
    }

    public string SelectedViewMode
    {
        get => _selectedViewMode;
        set
        {
            if (SetProperty(ref _selectedViewMode, value))
            {
                OnPropertyChanged(nameof(CurrentScopeSummary));
                RefreshViews();
            }
        }
    }

    public GradeInput? SelectedGradeInput
    {
        get => _selectedGradeInput;
        set
        {
            if (SetProperty(ref _selectedGradeInput, value))
            {
                OnPropertyChanged(nameof(CurrentScopeSummary));
                RefreshViews();
            }
        }
    }

    public SchoolClass? SelectedClass
    {
        get => _selectedClass;
        set
        {
            if (SetProperty(ref _selectedClass, value))
            {
                OnPropertyChanged(nameof(CurrentScopeSummary));
                RefreshViews();
            }
        }
    }

    public Teacher? SelectedTeacher
    {
        get => _selectedTeacher;
        set
        {
            if (SetProperty(ref _selectedTeacher, value))
            {
                OnPropertyChanged(nameof(CurrentScopeSummary));
                RefreshViews();
            }
        }
    }

    public SubjectDefinition? SelectedSubject
    {
        get => _selectedSubject;
        set => SetProperty(ref _selectedSubject, value);
    }

    public ScheduleEntry? SelectedScheduleEntry
    {
        get => _selectedScheduleEntry;
        set => SetProperty(ref _selectedScheduleEntry, value);
    }

    public LessonRequirement? SelectedRequirement
    {
        get => _selectedRequirement;
        set => SetProperty(ref _selectedRequirement, value);
    }

    public FixedLesson? SelectedFixedLesson
    {
        get => _selectedFixedLesson;
        set => SetProperty(ref _selectedFixedLesson, value);
    }

    public TeacherAssignment? SelectedTeacherAssignment
    {
        get => _selectedTeacherAssignment;
        set => SetProperty(ref _selectedTeacherAssignment, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string CurrentScopeSummary
    {
        get
        {
            return SelectedViewMode switch
            {
                "年级总表" => SelectedGradeInput is null
                    ? "未选择年级"
                    : $"{SelectedGradeInput.GradeName} · {SelectedGradeInput.ClassCount}班",
                "班级课表" => SelectedClass is null
                    ? "未选择班级"
                    : SelectedClass.DisplayName,
                "教师课表" => SelectedTeacher is null
                    ? "未选择教师"
                    : SelectedTeacher.Name,
                _ => "未选择"
            };
        }
    }

    public string ExportFolderPath => AppPaths.ExportFolder;
    public string DataFilePath => AppPaths.DataFile;

    public int TotalClasses => Classes.Count;
    public int TotalSubjects => Subjects.Count;
    public int TotalAssignments => TeacherAssignments.Count;
    public int TotalRequirements => Requirements.Count;
    public int TotalScheduleEntries => ScheduleEntries.Count;
    public int TotalConflicts => Conflicts.Count;

    public RelayCommand SeedSampleDataCommand { get; }
    public RelayCommand GenerateClassesCommand { get; }
    public RelayCommand GenerateRequirementsCommand { get; }
    public RelayCommand GenerateAssignmentsCommand { get; }
    public RelayCommand AutoScheduleCommand { get; }
    public RelayCommand ValidateCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand LoadCommand { get; }
    public RelayCommand NewProjectCommand { get; }
    public RelayCommand ExportCommand { get; }
    public RelayCommand ImportCommand { get; }
    public RelayCommand RefreshViewCommand { get; }
    public RelayCommand UseFiveDayCommand { get; }
    public RelayCommand UseSevenDayCommand { get; }
    public RelayCommand<string> SelectMainPageCommand { get; }
    public RelayCommand<string> SelectConfigPageCommand { get; }
    public RelayCommand<string> SelectViewModeCommand { get; }
    public RelayCommand<GradeInput> SelectGradeCommand { get; }
    public RelayCommand<SchoolClass> SelectClassCommand { get; }
    public RelayCommand<Teacher> SelectTeacherCommand { get; }

    public void MoveEntry(ScheduleEntry entry, int dayIndex, int periodIndex)
    {
        if (_scheduleService.TryMoveEntry(BuildSchoolData(), entry, dayIndex, periodIndex, out IReadOnlyList<ScheduleConflict> conflicts))
        {
            entry.DayIndex = dayIndex;
            entry.PeriodIndex = periodIndex;
            entry.Note = "手动调整";
            Log($"已移动 {entry.Subject}-{entry.TeacherName} 到 {entry.SlotLabel}");
            ValidateSchedule();
            RefreshViews();
            return;
        }

        Conflicts.Clear();
        foreach (ScheduleConflict conflict in conflicts)
        {
            Conflicts.Add(conflict);
        }

        StatusMessage = conflicts.FirstOrDefault()?.Message ?? "移动失败";
    }

    public void SwapEntries(ScheduleEntry source, ScheduleEntry target)
    {
        if (_scheduleService.TrySwapEntries(BuildSchoolData(), source, target, out IReadOnlyList<ScheduleConflict> conflicts))
        {
            source.Note = "换课";
            target.Note = "换课";
            Log($"已换课 {source.Subject}-{source.TeacherName} <-> {target.Subject}-{target.TeacherName}");
            ValidateSchedule();
            RefreshViews();
            return;
        }

        Conflicts.Clear();
        foreach (ScheduleConflict conflict in conflicts)
        {
            Conflicts.Add(conflict);
        }

        StatusMessage = conflicts.FirstOrDefault()?.Message ?? "换课失败";
    }

    private void NewProject()
    {
        GradeInputs.Clear();
        Classes.Clear();
        Teachers.Clear();
        Subjects.Clear();
        TeacherAssignments.Clear();
        Requirements.Clear();
        FixedLessons.Clear();
        ScheduleEntries.Clear();
        Conflicts.Clear();
        SchoolName = "中学";
        DaysPerWeek = 5;
        PeriodsPerDay = 7;
        MorningPeriods = 4;
        AfternoonPeriods = 3;
        StatusMessage = "已创建新项目";
        SelectedConfigPage = "基础设置";
        RefreshViews();
    }

    private void LoadSampleData()
    {
        ApplySchoolData(SampleDataFactory.Create());
        SelectedMainPage = "配置";
        SelectedConfigPage = "基础设置";
        SelectedViewMode = "年级总表";
        SelectedGradeInput = GradeInputs.FirstOrDefault();
        SelectedClass = Classes.FirstOrDefault();
        SelectedTeacher = Teachers.FirstOrDefault();
        StatusMessage = "已载入示例数据";
        RefreshViews();
    }

    private void GenerateClasses()
    {
        Classes.Clear();
        Requirements.Clear();
        ScheduleEntries.Clear();
        Conflicts.Clear();

        foreach (SchoolClass schoolClass in _scheduleService.BuildClasses(GradeInputs))
        {
            Classes.Add(schoolClass);
        }

        SelectedClass = Classes.FirstOrDefault();
        StatusMessage = $"已生成 {Classes.Count} 个班级";
        Log($"生成班级：{Classes.Count} 个");
        RefreshViews();
    }

    private void GenerateAssignments()
    {
        _scheduleService.GenerateAssignments(TeacherAssignments, Subjects, Classes);
        StatusMessage = $"已生成 {TeacherAssignments.Count} 条教师授课安排";
        Log($"生成教师安排：{TeacherAssignments.Count} 条");
    }

    private void GenerateRequirements()
    {
        Requirements.Clear();
        Teachers.Clear();
        ScheduleEntries.Clear();
        Conflicts.Clear();

        foreach (LessonRequirement requirement in _scheduleService.BuildRequirementsFromAssignments(TeacherAssignments, Classes, Subjects))
        {
            Requirements.Add(requirement);
            if (Teachers.All(t => t.Name != requirement.TeacherName))
            {
                Teachers.Add(new Teacher { Name = requirement.TeacherName, Subject = requirement.Subject });
            }
        }

        SelectedRequirement = Requirements.FirstOrDefault();
        StatusMessage = $"已生成 {Requirements.Count} 条授课需求";
        Log($"生成需求：{Requirements.Count} 条");
        RefreshViews();
    }

    private void AutoSchedule()
    {
        GenerateRequirements();

        ScheduleResult result = _scheduleService.Generate(BuildSchoolData());
        ScheduleEntries.Clear();
        foreach (ScheduleEntry entry in result.Entries.OrderBy(x => x.DayIndex).ThenBy(x => x.PeriodIndex))
        {
            ScheduleEntries.Add(entry);
        }

        Conflicts.Clear();
        foreach (ScheduleConflict conflict in result.Conflicts)
        {
            Conflicts.Add(conflict);
        }

        OnPropertyChanged(nameof(TotalScheduleEntries));
        OnPropertyChanged(nameof(TotalConflicts));

        StatusMessage = $"排课完成：{ScheduleEntries.Count} 节课，{Conflicts.Count} 条提示";
        Log($"自动排课：{ScheduleEntries.Count} 节课");
        RefreshViews();
    }

    private void ValidateSchedule()
    {
        Conflicts.Clear();
        foreach (ScheduleConflict conflict in _scheduleService.Validate(BuildSchoolData()))
        {
            Conflicts.Add(conflict);
        }

        OnPropertyChanged(nameof(TotalConflicts));
        StatusMessage = $"检查完成：{Conflicts.Count} 条提示";
        Log($"检查冲突：{Conflicts.Count} 条");
    }

    private void SaveData()
    {
        _store.Save(BuildSchoolData());
        StatusMessage = "数据已保存";
    }

    private void LoadData()
    {
        SchoolData data = _store.Load();
        if (data.GradeInputs.Count == 0)
        {
            return;
        }

        ApplySchoolData(data);
        SelectedMainPage = "配置";
        SelectedConfigPage = "基础设置";
        SelectedViewMode = "年级总表";
        SelectedGradeInput = GradeInputs.FirstOrDefault();
        SelectedClass = Classes.FirstOrDefault();
        SelectedTeacher = Teachers.FirstOrDefault();
        StatusMessage = "数据已载入";
        RefreshViews();
    }

    private void ExportExcel()
    {
        string folder = EnsureExportFolder();
        _excelService.ExportAll(BuildSchoolData(), folder);
        StatusMessage = $"已导出到 {folder}";
        Log("导出 Excel");
    }

    private void ImportExcel()
    {
        OpenFileDialog dialog = new()
        {
            Filter = "Excel 文件 (*.xlsx)|*.xlsx",
            Title = "导入排课数据"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        SchoolData data = _excelService.Import(dialog.FileName);
        ApplySchoolData(data);
        RefreshViews();
        StatusMessage = "已导入 Excel 数据";
        Log("导入 Excel");
    }

    private void SetMainPage(string? page)
    {
        if (string.IsNullOrWhiteSpace(page))
        {
            return;
        }

        SelectedMainPage = page;
        OnPropertyChanged(nameof(CurrentScopeSummary));
    }

    private void SetConfigPage(string? page)
    {
        if (string.IsNullOrWhiteSpace(page))
        {
            return;
        }

        SelectedConfigPage = page;
    }

    private void SetViewMode(string? viewMode)
    {
        if (string.IsNullOrWhiteSpace(viewMode))
        {
            return;
        }

        SelectedViewMode = viewMode;
        if (SelectedViewMode == "年级总表" && SelectedGradeInput is null)
        {
            SelectedGradeInput = GradeInputs.FirstOrDefault();
        }
        else if (SelectedViewMode == "班级课表" && SelectedClass is null)
        {
            SelectedClass = Classes.FirstOrDefault();
        }
        else if (SelectedViewMode == "教师课表" && SelectedTeacher is null)
        {
            SelectedTeacher = Teachers.FirstOrDefault();
        }

        RefreshViews();
    }

    private void SelectGrade(GradeInput? grade)
    {
        if (grade is null)
        {
            return;
        }

        SelectedViewMode = "年级总表";
        SelectedGradeInput = grade;
    }

    private void SelectClass(SchoolClass? schoolClass)
    {
        if (schoolClass is null)
        {
            return;
        }

        SelectedViewMode = "班级课表";
        SelectedClass = schoolClass;
    }

    private void SelectTeacher(Teacher? teacher)
    {
        if (teacher is null)
        {
            return;
        }

        SelectedViewMode = "教师课表";
        SelectedTeacher = teacher;
    }

    private void SetDaysPerWeek(int days)
    {
        DaysPerWeek = days;
        StatusMessage = days == 5 ? "已切换到五天制" : "已切换到七天制";
        Log(StatusMessage);
    }

    public void RefreshViews()
    {
        RefreshVisibleEntries();
        RefreshTimetable();
        OnPropertyChanged(nameof(CurrentScopeSummary));
        OnPropertyChanged(nameof(TotalClasses));
        OnPropertyChanged(nameof(TotalSubjects));
        OnPropertyChanged(nameof(TotalAssignments));
        OnPropertyChanged(nameof(TotalRequirements));
        OnPropertyChanged(nameof(TotalScheduleEntries));
        OnPropertyChanged(nameof(TotalConflicts));
    }

    private void RefreshVisibleEntries()
    {
        VisibleScheduleEntries.Clear();

        IEnumerable<ScheduleEntry> entries = ScheduleEntries;
        if (SelectedViewMode == "班级课表" && SelectedClass is not null)
        {
            entries = entries.Where(x => x.ClassId == SelectedClass.Id);
        }
        else if (SelectedViewMode == "教师课表" && SelectedTeacher is not null)
        {
            entries = entries.Where(x => x.TeacherId == SelectedTeacher.Id);
        }
        else if (SelectedViewMode == "年级总表" && SelectedGradeInput is not null)
        {
            string gradeName = SelectedGradeInput.GradeName;
            entries = entries.Where(x => x.ClassName.StartsWith(gradeName, StringComparison.OrdinalIgnoreCase));
        }

        foreach (ScheduleEntry entry in entries.OrderBy(x => x.DayIndex).ThenBy(x => x.PeriodIndex).ThenBy(x => x.ClassName))
        {
            VisibleScheduleEntries.Add(entry);
        }
    }

    private void RefreshTimetable()
    {
        ObservableCollection<ScheduleRowViewModel> days = new();
        for (int day = 0; day < Math.Max(1, DaysPerWeek); day++)
        {
            ScheduleRowViewModel dayView = new()
            {
                DayIndex = day,
                DayName = GetDayName(day)
            };

            for (int period = 1; period <= Math.Max(1, PeriodsPerDay); period++)
            {
                ScheduleCellViewModel periodView = new()
                {
                    DayIndex = day,
                    PeriodIndex = period
                };

                foreach (ScheduleEntry entry in VisibleScheduleEntries
                    .Where(x => x.DayIndex == day && x.PeriodIndex == period)
                    .OrderBy(x => x.ClassName)
                    .ThenBy(x => x.Subject))
                {
                    periodView.Entries.Add(entry);
                }

                dayView.Cells.Add(periodView);
            }

            days.Add(dayView);
        }

        TimetableDays = days;
        OnPropertyChanged(nameof(TimetableDays));
    }

    private void ApplySchoolData(SchoolData data)
    {
        SchoolName = data.Settings.SchoolName;
        DaysPerWeek = data.Settings.DaysPerWeek;
        PeriodsPerDay = data.Settings.PeriodsPerDay;
        MorningPeriods = data.Settings.MorningPeriods;
        AfternoonPeriods = data.Settings.AfternoonPeriods;

        ReplaceCollection(GradeInputs, data.GradeInputs);
        ReplaceCollection(Classes, data.Classes);
        ReplaceCollection(Teachers, data.Teachers);
        ReplaceCollection(Subjects, data.Subjects);
        ReplaceCollection(TeacherAssignments, data.TeacherAssignments);
        ReplaceCollection(Requirements, data.Requirements);
        ReplaceCollection(FixedLessons, data.FixedLessons);
        ReplaceCollection(ScheduleEntries, data.ScheduleEntries);

        SelectedGradeInput = GradeInputs.FirstOrDefault();
        SelectedClass = Classes.FirstOrDefault();
        SelectedTeacher = Teachers.FirstOrDefault();
        SelectedSubject = Subjects.FirstOrDefault();
        SelectedRequirement = Requirements.FirstOrDefault();
        SelectedFixedLesson = FixedLessons.FirstOrDefault();
        SelectedTeacherAssignment = TeacherAssignments.FirstOrDefault();
    }

    private SchoolData BuildSchoolData()
    {
        List<Teacher> teacherList = Teachers.ToList();
        if (teacherList.Count == 0)
        {
            foreach (LessonRequirement r in Requirements)
            {
                if (teacherList.All(t => t.Name != r.TeacherName))
                {
                    teacherList.Add(new Teacher { Name = r.TeacherName, Subject = r.Subject });
                }
            }
        }

        return new SchoolData
        {
            Settings = new ScheduleSettings
            {
                SchoolName = SchoolName,
                DaysPerWeek = DaysPerWeek,
                PeriodsPerDay = PeriodsPerDay,
                MorningPeriods = MorningPeriods,
                AfternoonPeriods = AfternoonPeriods
            },
            GradeInputs = GradeInputs.ToList(),
            Classes = Classes.ToList(),
            Teachers = teacherList,
            Subjects = Subjects.ToList(),
            TeacherAssignments = TeacherAssignments.ToList(),
            Requirements = Requirements.ToList(),
            FixedLessons = FixedLessons.ToList(),
            ScheduleEntries = ScheduleEntries.ToList()
        };
    }

    private static void ReplaceCollection<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();
        foreach (T item in items)
        {
            target.Add(item);
        }
    }

    private string EnsureExportFolder()
    {
        Directory.CreateDirectory(AppPaths.ExportFolder);
        return AppPaths.ExportFolder;
    }

    private void Log(string message)
    {
        ActivityLog.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {message}");
        while (ActivityLog.Count > 50)
        {
            ActivityLog.RemoveAt(ActivityLog.Count - 1);
        }
    }

    private static string GetDayName(int dayIndex)
    {
        return dayIndex switch
        {
            0 => "周一",
            1 => "周二",
            2 => "周三",
            3 => "周四",
            4 => "周五",
            5 => "周六",
            6 => "周日",
            _ => $"周{dayIndex + 1}"
        };
    }
}
