using System.Collections.ObjectModel;
using System.IO;
using Automatic_class_schedule.Infrastructure;
using Automatic_class_schedule.Models;
using Automatic_class_schedule.Services;
using Automatic_class_schedule.Solver;

namespace Automatic_class_schedule.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly ScheduleService _scheduleService;
    private readonly SchoolDataStore _store;
    private readonly ExcelScheduleService _excelService;
    private int _daysPerWeek = 5;
    private int _periodsPerDay = 7;
    private int _morningPeriods = 4;
    private int _afternoonPeriods = 3;
    private bool _includeEveningSelfStudy;
    private int _eveningPeriods = 2;
    private string _selectedMainPage = "配置";
    private string _selectedConfigPage = "基础设置";
    private string _selectedViewMode = "年级总表";
    private string _projectFilePath = string.Empty;
    private string _projectName = string.Empty;
    private GradeInput? _selectedGradeInput;
    private SchoolClass? _selectedClass;
    private Teacher? _selectedTeacher;
    private SubjectDefinition? _selectedSubject;
    private ScheduleEntry? _selectedScheduleEntry;
    private LessonRequirement? _selectedRequirement;
    private FixedLesson? _selectedFixedLesson;
    private TeacherAssignment? _selectedTeacherAssignment;
    private string _statusMessage = "就绪";
    private bool _isBusy;
    private double _progressValue;
    private string _progressMessage = string.Empty;
    private string _teacherSearchText = string.Empty;
    private string _gradeFilterText = string.Empty;
    private string _currentSubjectGradeName = string.Empty;
    private CancellationTokenSource? _cts;
    private string _selectedCourseTemplate = string.Empty;
    private bool _isToolbarExpanded = true;
    private bool _hasActiveProject;
    private byte[]? _savedSnapshot;
    private readonly RecentProjectsService _recentProjects;
    private bool[] _eveningStudyDays = { true, true, true, true, true, false, false };
    private int _selectedSettingsTabIndex;
    private double _dialogProgress;
    private string _dialogProgressText = string.Empty;
    private bool _dialogIsComplete;
    private string _dialogTitle = "自动排课";
    private System.Threading.Timer? _progressSmoothTimer;
    private Microsoft.UI.Dispatching.DispatcherQueue? _dispatcherQueue;

    public MainViewModel()
    {
        _recentProjects = new RecentProjectsService();
        _scheduleService = new ScheduleService(new CpSatScheduleSolver(), new ConflictService());
        _store = new SchoolDataStore();
        _excelService = new ExcelScheduleService();

        GradeInputs = new ObservableCollection<GradeInput>();
        GradeConfigs = new ObservableCollection<GradeScheduleConfig>();
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
        TimetableDays = new ObservableCollection<ScheduleDayViewModel>();
        TimetableRows = new ObservableCollection<SchedulePeriodRowViewModel>();

        SeedSampleDataCommand = new RelayCommand(() => _ = LoadSampleDataAsync());
        GenerateClassesCommand = new RelayCommand(GenerateClasses);
        GenerateRequirementsCommand = new RelayCommand(GenerateRequirements);
        GenerateAssignmentsCommand = new RelayCommand(GenerateAssignments);
        AddTeacherAssignmentCommand = new RelayCommand(AddTeacherAssignment);
        DeleteTeacherAssignmentCommand = new RelayCommand(DeleteTeacherAssignment, () => SelectedTeacherAssignment is not null);
        GenerateTeacherTemplateCommand = new RelayCommand(GenerateTeacherTemplate);
        ImportTeacherListCommand = new RelayCommand(() => _ = ImportTeacherListAsync());
        GenerateTeachersCommand = new RelayCommand(() => _ = GenerateTeachersAsync());
        AddFixedLessonCommand = new RelayCommand(AddFixedLesson);
        DeleteFixedLessonCommand = new RelayCommand(DeleteFixedLesson, () => SelectedFixedLesson is not null);
        AutoScheduleCommand = new RelayCommand(() => _ = AutoScheduleAsync());
        ValidateCommand = new RelayCommand(ValidateSchedule);
        ClearConflictsCommand = new RelayCommand(() => { Conflicts.Clear(); OnPropertyChanged(nameof(TotalConflicts)); });
        LocalAdjustCommand = new RelayCommand(() => _ = LocalAdjustAsync());
        SaveCommand = new RelayCommand(SaveData);
        LoadCommand = new RelayCommand(LoadData);
        NewProjectCommand = new RelayCommand(NewProject);
        ExportCommand = new RelayCommand(() => _ = ExportExcelAsync());
        SelectExportFolderCommand = new RelayCommand(() => _ = SelectExportFolderAsync());
        ImportCommand = new RelayCommand(() => _ = ImportExcelAsync());
        CancelCommand = new RelayCommand(CancelOperation, () => IsBusy);
        UseFiveDayCommand = new RelayCommand(() => SetDaysPerWeek(5));
        UseSevenDayCommand = new RelayCommand(() => SetDaysPerWeek(7));
        SelectMainPageCommand = new RelayCommand<string>(SetMainPage);
        SelectConfigPageCommand = new RelayCommand<string>(SetConfigPage);
        SelectViewModeCommand = new RelayCommand<string>(SetViewMode);
        SelectSubjectGradeCommand = new RelayCommand<string>(SetSubjectGrade);
        SelectGradeCommand = new RelayCommand<GradeInput>(SelectGrade);
        SelectClassCommand = new RelayCommand<SchoolClass>(SelectClass);
        SelectTeacherCommand = new RelayCommand<Teacher>(SelectTeacher);
        SelectDayCommand = new RelayCommand<int>(SelectDay);
        SearchTeacherCommand = new RelayCommand(RefreshViews);
        FilterGradeCommand = new RelayCommand(RefreshViews);
        AddSubjectCommand = new RelayCommand(AddSubject);
        DeleteSubjectCommand = new RelayCommand(DeleteSubject, () => SelectedSubject is not null);
        SaveCourseTemplateCommand = new RelayCommand(() => SaveCourseTemplate());
        LoadCourseTemplateCommand = new RelayCommand(LoadCourseTemplate);
        DeleteCourseTemplateCommand = new RelayCommand(DeleteCourseTemplate, () => !string.IsNullOrEmpty(_selectedCourseTemplate));
        ToggleToolbarCommand = new RelayCommand(ToggleToolbar);
        SaveAsCommand = new RelayCommand<string?>(SaveProject);
        OpenProjectCommand = new RelayCommand<string?>(OpenProject);
        CreateProjectCommand = new RelayCommand(() => CreateProject());
        ToggleEveningDayCommand = new RelayCommand<int>(ToggleEveningDay);
        ToggleGradeEveningDayCommand = new RelayCommand<string>(ToggleGradeEveningDay);
        SelectSettingsTabCommand = new RelayCommand<int>(SelectSettingsTab);

        InitEveningDayItems();
        LoadCourseTemplates();

        // 设置教师配置周课时默认值解析器
        TeacherAssignment.DefaultWeeklyCountResolver = (subject, gradeName) =>
            Subjects.FirstOrDefault(s => s.Name == subject &&
                (string.IsNullOrEmpty(s.GradeName) || s.GradeName == gradeName))?.DefaultWeeklyCount ?? 0;

        // 确保 ACS 工作空间目录存在
        AppPaths.EnsureDirectories();

        // Start with no project loaded
        _projectFilePath = "";
        _hasActiveProject = false;
        OnPropertyChanged(nameof(HasActiveProject));
        OnPropertyChanged(nameof(ProjectFileName));
        RefreshHomePageProjects();
        SelectedMainPage = "配置";
        SelectedConfigPage = "基础设置";
    }

    private void InitDefaultGrades()
    {
        GradeInputs.Add(new GradeInput { GradeName = "七年级", ClassCount = 8 });
        GradeInputs.Add(new GradeInput { GradeName = "八年级", ClassCount = 8 });
        GradeInputs.Add(new GradeInput { GradeName = "九年级", ClassCount = 6 });
    }

    /// <summary>内置初中标准课程配置（随软件打包，不依赖文件）</summary>
    private static CourseTemplateData GetBuiltInTemplate()
    {
        return new CourseTemplateData
        {
            Subjects = new List<SubjectDefinition>
            {
                // 七年级: 语7+数7+英7+地2+生2+体3+美1+音1+信1+道2+历2+劳1=36
                new() { Name = "语文", Category = "主科", DefaultWeeklyCount = 7, DistributionRule = "每日至少一次", GradeName = "七年级" },
                new() { Name = "数学", Category = "主科", DefaultWeeklyCount = 7, DistributionRule = "每日至少一次", GradeName = "七年级" },
                new() { Name = "英语", Category = "主科", DefaultWeeklyCount = 7, DistributionRule = "每日至少一次", GradeName = "七年级" },
                new() { Name = "地理", Category = "文科", DefaultWeeklyCount = 2, DistributionRule = "均匀分布", GradeName = "七年级" },
                new() { Name = "生物", Category = "理科", DefaultWeeklyCount = 2, DistributionRule = "均匀分布", GradeName = "七年级" },
                new() { Name = "体育", Category = "副科", DefaultWeeklyCount = 3, DistributionRule = "均匀分布", GradeName = "七年级" },
                new() { Name = "美术", Category = "副科", DefaultWeeklyCount = 1, DistributionRule = "均匀分布", GradeName = "七年级" },
                new() { Name = "音乐", Category = "副科", DefaultWeeklyCount = 1, DistributionRule = "均匀分布", GradeName = "七年级" },
                new() { Name = "信息", Category = "副科", DefaultWeeklyCount = 1, DistributionRule = "均匀分布", GradeName = "七年级" },
                new() { Name = "道德", Category = "文科", DefaultWeeklyCount = 2, DistributionRule = "均匀分布", GradeName = "七年级" },
                new() { Name = "历史", Category = "文科", DefaultWeeklyCount = 2, DistributionRule = "均匀分布", GradeName = "七年级" },
                new() { Name = "劳动", Category = "副科", DefaultWeeklyCount = 1, DistributionRule = "均匀分布", GradeName = "七年级" },
                // 八年级: 语6+数6+英6+物3+地2+生2+体3+美1+音1+信1+道2+历2+劳1=36
                new() { Name = "语文", Category = "主科", DefaultWeeklyCount = 6, DistributionRule = "每日至少一次", GradeName = "八年级" },
                new() { Name = "数学", Category = "主科", DefaultWeeklyCount = 6, DistributionRule = "每日至少一次", GradeName = "八年级" },
                new() { Name = "英语", Category = "主科", DefaultWeeklyCount = 6, DistributionRule = "每日至少一次", GradeName = "八年级" },
                new() { Name = "物理", Category = "理科", DefaultWeeklyCount = 3, DistributionRule = "均匀分布", GradeName = "八年级" },
                new() { Name = "地理", Category = "文科", DefaultWeeklyCount = 2, DistributionRule = "均匀分布", GradeName = "八年级" },
                new() { Name = "生物", Category = "理科", DefaultWeeklyCount = 2, DistributionRule = "均匀分布", GradeName = "八年级" },
                new() { Name = "体育", Category = "副科", DefaultWeeklyCount = 3, DistributionRule = "均匀分布", GradeName = "八年级" },
                new() { Name = "美术", Category = "副科", DefaultWeeklyCount = 1, DistributionRule = "均匀分布", GradeName = "八年级" },
                new() { Name = "音乐", Category = "副科", DefaultWeeklyCount = 1, DistributionRule = "均匀分布", GradeName = "八年级" },
                new() { Name = "信息", Category = "副科", DefaultWeeklyCount = 1, DistributionRule = "均匀分布", GradeName = "八年级" },
                new() { Name = "道德", Category = "文科", DefaultWeeklyCount = 2, DistributionRule = "均匀分布", GradeName = "八年级" },
                new() { Name = "历史", Category = "文科", DefaultWeeklyCount = 2, DistributionRule = "均匀分布", GradeName = "八年级" },
                new() { Name = "劳动", Category = "副科", DefaultWeeklyCount = 1, DistributionRule = "均匀分布", GradeName = "八年级" },
                // 九年级: 语7+数6+英6+物4+化4+体3+美1+音1+道2+历2=36
                new() { Name = "语文", Category = "主科", DefaultWeeklyCount = 7, DistributionRule = "每日至少一次", GradeName = "九年级" },
                new() { Name = "数学", Category = "主科", DefaultWeeklyCount = 6, DistributionRule = "每日至少一次", GradeName = "九年级" },
                new() { Name = "英语", Category = "主科", DefaultWeeklyCount = 6, DistributionRule = "每日至少一次", GradeName = "九年级" },
                new() { Name = "物理", Category = "理科", DefaultWeeklyCount = 4, DistributionRule = "均匀分布", GradeName = "九年级" },
                new() { Name = "化学", Category = "理科", DefaultWeeklyCount = 4, DistributionRule = "均匀分布", GradeName = "九年级" },
                new() { Name = "体育", Category = "副科", DefaultWeeklyCount = 3, DistributionRule = "均匀分布", GradeName = "九年级" },
                new() { Name = "美术", Category = "副科", DefaultWeeklyCount = 1, DistributionRule = "均匀分布", GradeName = "九年级" },
                new() { Name = "音乐", Category = "副科", DefaultWeeklyCount = 1, DistributionRule = "均匀分布", GradeName = "九年级" },
                new() { Name = "道德", Category = "文科", DefaultWeeklyCount = 2, DistributionRule = "均匀分布", GradeName = "九年级" },
                new() { Name = "历史", Category = "文科", DefaultWeeklyCount = 2, DistributionRule = "均匀分布", GradeName = "九年级" },
            },
            FixedLessons = new List<FixedLesson>
            {
                new() { ScopeValue = "全校", DayIndex = 1, PeriodIndex = 8, Subject = "周会", Reason = "固定课程" },
                new() { ScopeValue = "全校", DayIndex = 5, PeriodIndex = 6, Subject = "社团", Reason = "固定课程" },
                new() { ScopeValue = "全校", DayIndex = 5, PeriodIndex = 7, Subject = "活动", Reason = "固定课程" },
                new() { ScopeValue = "全校", DayIndex = 5, PeriodIndex = 8, Subject = "教育", Reason = "固定课程" },
            }
        };
    }

    /// <summary>新建项目时加载内置初中标准配置</summary>
    private void InitDefaultSubjectsFromTemplate()
    {
        var builtIn = GetBuiltInTemplate();
        Subjects.Clear();
        foreach (var s in builtIn.Subjects)
            Subjects.Add(s);
        FixedLessons.Clear();
        foreach (var fl in builtIn.FixedLessons)
            FixedLessons.Add(fl);
    }

    private void AddSubject()
    {
        string grade = !string.IsNullOrWhiteSpace(CurrentSubjectGradeName) && CurrentSubjectGradeName != "全部"
            ? CurrentSubjectGradeName
            : GradeInputs.FirstOrDefault()?.GradeName ?? string.Empty;
        var subj = new SubjectDefinition
        {
            Name = "新科目",
            Category = "副科",
            DefaultWeeklyCount = 2,
            GradeName = grade
        };
        subj.DistributionRule = GetDefaultDistributionRule(subj.Category, subj.DefaultWeeklyCount);
        Subjects.Add(subj);
        OnPropertyChanged(nameof(FilteredSubjects));
    }

    private static string GetDefaultDistributionRule(string category, int weeklyCount)
    {
        if (category == "主科" && weeklyCount >= 4) return "每日至少一次";
        if (category == "主科") return "每日至少一次";
        if (weeklyCount >= 3) return "均匀分布";
        return "均匀分布";
    }

    private void DeleteSubject()
    {
        if (SelectedSubject is not null)
        {
            Subjects.Remove(SelectedSubject);
            SelectedSubject = Subjects.FirstOrDefault();
        }
    }

    public ObservableCollection<GradeInput> GradeInputs { get; }

    /// <summary>七年级班级数（代理 GradeInputs[0]）</summary>
    public int Grade7ClassCount
    {
        get => GradeInputs.Count > 0 ? GradeInputs[0].ClassCount : 0;
        set { if (GradeInputs.Count > 0 && GradeInputs[0].ClassCount != value) { GradeInputs[0].ClassCount = value; OnPropertyChanged(); } }
    }

    /// <summary>八年级班级数（代理 GradeInputs[1]）</summary>
    public int Grade8ClassCount
    {
        get => GradeInputs.Count > 1 ? GradeInputs[1].ClassCount : 0;
        set { if (GradeInputs.Count > 1 && GradeInputs[1].ClassCount != value) { GradeInputs[1].ClassCount = value; OnPropertyChanged(); } }
    }

    /// <summary>九年级班级数（代理 GradeInputs[2]）</summary>
    public int Grade9ClassCount
    {
        get => GradeInputs.Count > 2 ? GradeInputs[2].ClassCount : 0;
        set { if (GradeInputs.Count > 2 && GradeInputs[2].ClassCount != value) { GradeInputs[2].ClassCount = value; OnPropertyChanged(); } }
    }

    public ObservableCollection<GradeScheduleConfig> GradeConfigs { get; }
    public ObservableCollection<DayToggleItem> EveningDayItems { get; } = new();
    public ObservableCollection<DayToggleItem> GradeEveningDayItems { get; } = new();
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
    public ObservableCollection<ScheduleDayViewModel> TimetableDays { get; private set; }
    public ObservableCollection<SchedulePeriodRowViewModel> TimetableRows { get; private set; }
    public ObservableCollection<PeriodGroup> GradePeriodGroups { get; private set; } = new();
        public ObservableCollection<GradeDayHeader> GradeDayHeaders { get; private set; } = new();
        public ObservableCollection<GradeClassRow> GradeClassRows { get; private set; } = new();

    private double _gradeCellWidth = 48;
    /// <summary>年级总表单元格宽度（根据可用区域动态计算）</summary>
    public double GradeCellWidth
    {
        get => _gradeCellWidth;
        set => SetProperty(ref _gradeCellWidth, value);
    }

    public void UpdateGradeCellWidth(double availableWidth)
    {
        int totalCols = DaysPerWeek * PeriodsPerDay;
        if (totalCols <= 0) return;
        double width = (availableWidth - 70) / totalCols; // 70 = 班级列宽
        GradeCellWidth = Math.Max(32, Math.Min(80, width));
    }

    public ObservableCollection<ScheduleGridRow> MatrixRows { get; private set; } = new();
    public ObservableCollection<SchoolClass> AvailableClasses { get; private set; } = new();
    public ObservableCollection<DayTabItem> DayTabs { get; private set; } = new();
    public ObservableCollection<string> CourseTemplates { get; private set; } = new();
    public string[] DayNames => Enumerable.Range(0, DaysPerWeek).Select(i => GetDayName(i)).ToArray();

    private int _selectedDayIndex;
    public int SelectedDayIndex
    {
        get => _selectedDayIndex;
        set => SetProperty(ref _selectedDayIndex, value);
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
                if (_morningPeriods + _afternoonPeriods + (_includeEveningSelfStudy ? _eveningPeriods : 0) != _periodsPerDay)
                {
                    PeriodsPerDay = _morningPeriods + _afternoonPeriods + (_includeEveningSelfStudy ? _eveningPeriods : 0);
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
                if (_morningPeriods + _afternoonPeriods + (_includeEveningSelfStudy ? _eveningPeriods : 0) != _periodsPerDay)
                {
                    PeriodsPerDay = _morningPeriods + _afternoonPeriods + (_includeEveningSelfStudy ? _eveningPeriods : 0);
                }
            }
        }
    }

    public bool IncludeEveningSelfStudy
    {
        get => _includeEveningSelfStudy;
        set
        {
            if (SetProperty(ref _includeEveningSelfStudy, value))
            {
                if (_morningPeriods + _afternoonPeriods + (value ? _eveningPeriods : 0) != _periodsPerDay)
                {
                    PeriodsPerDay = _morningPeriods + _afternoonPeriods + (value ? _eveningPeriods : 0);
                }
            }
        }
    }

    public int EveningPeriods
    {
        get => _eveningPeriods;
        set
        {
            if (SetProperty(ref _eveningPeriods, value))
            {
                if (_includeEveningSelfStudy && _morningPeriods + _afternoonPeriods + value != _periodsPerDay)
                {
                    PeriodsPerDay = _morningPeriods + _afternoonPeriods + value;
                }
            }
        }
    }

    /// <summary>全局晚自习天配置（周一到周日）</summary>
    public bool[] EveningStudyDays
    {
        get => _eveningStudyDays;
        set => SetProperty(ref _eveningStudyDays, value);
    }

    /// <summary>基础设置页Tab索引（0=全局, 1=七年级, 2=八年级, 3=九年级）</summary>
    public int SelectedSettingsTabIndex
    {
        get => _selectedSettingsTabIndex;
        set
        {
            if (SetProperty(ref _selectedSettingsTabIndex, value))
            {
                OnPropertyChanged(nameof(IsGlobalSettingsTab));
                OnPropertyChanged(nameof(SelectedGradeConfig));
                OnPropertyChanged(nameof(IsGradeSettingsTab));
            }
        }
    }

    public bool IsGlobalSettingsTab => _selectedSettingsTabIndex == 0;
    public bool IsGradeSettingsTab => _selectedSettingsTabIndex > 0;

    /// <summary>当前选中的年级配置（Tab索引1-3对应七年级/八年级/九年级）</summary>
    public GradeScheduleConfig? SelectedGradeConfig =>
        _selectedSettingsTabIndex > 0 && _selectedSettingsTabIndex <= GradeConfigs.Count
            ? GradeConfigs[_selectedSettingsTabIndex - 1]
            : null;

    public string[] SettingsTabNames { get; } = { "全局", "七年级", "八年级", "九年级" };
    public string[] DayLabels { get; } = { "周一", "周二", "周三", "周四", "周五", "周六", "周日" };

    public string SelectedMainPage
    {
        get => _selectedMainPage;
        set
        {
            if (SetProperty(ref _selectedMainPage, value))
            {
                OnPropertyChanged(nameof(CurrentScopeSummary));
                OnPropertyChanged(nameof(ConfigPageVisibility));
                OnPropertyChanged(nameof(SchedulePageVisibility));
                OnPropertyChanged(nameof(ExportPageVisibility));
            }
        }
    }

    public string SelectedConfigPage
    {
        get => _selectedConfigPage;
        set
        {
            if (SetProperty(ref _selectedConfigPage, value))
            {
                OnPropertyChanged(nameof(IsBasicSettingsVisible));
                OnPropertyChanged(nameof(IsClassConfigVisible));
                OnPropertyChanged(nameof(IsSubjectConfigVisible));
                OnPropertyChanged(nameof(IsTeacherConfigVisible));
                OnPropertyChanged(nameof(IsFixedLessonConfigVisible));
                OnPropertyChanged(nameof(IsAutoScheduleVisible));
            }
        }
    }

    public string SelectedViewMode
    {
        get => _selectedViewMode;
        set
        {
            if (SetProperty(ref _selectedViewMode, value))
            {
                OnPropertyChanged(nameof(CurrentScopeSummary));
                OnPropertyChanged(nameof(IsGradeViewMode));
                OnPropertyChanged(nameof(IsClassViewMode));
                OnPropertyChanged(nameof(IsTeacherViewMode));
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

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                CancelCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public double ProgressValue
    {
        get => _progressValue;
        set => SetProperty(ref _progressValue, value);
    }

    public string ProgressMessage
    {
        get => _progressMessage;
        set => SetProperty(ref _progressMessage, value);
    }

    // ===== 排课进度弹窗属性 =====
    public double DialogProgress
    {
        get => _dialogProgress;
        set => SetProperty(ref _dialogProgress, value);
    }

    public string DialogProgressText
    {
        get => _dialogProgressText;
        set => SetProperty(ref _dialogProgressText, value);
    }

    public bool DialogIsComplete
    {
        get => _dialogIsComplete;
        set
        {
            if (SetProperty(ref _dialogIsComplete, value))
                OnPropertyChanged(nameof(DialogIsRunning));
        }
    }

    public bool DialogIsRunning => !DialogIsComplete;

    public string DialogTitle
    {
        get => _dialogTitle;
        set => SetProperty(ref _dialogTitle, value);
    }

    /// <summary>弹窗是否应显示（由View绑定控制ContentDialog显示）</summary>
    public bool IsScheduleDialogOpen { get; set; }

    /// <summary>请求打开进度弹窗（View订阅）</summary>
    public event Action? RequestOpenProgressDialog;
    /// <summary>请求关闭进度弹窗（View订阅）</summary>
    public event Action? RequestCloseProgressDialog;

    /// <summary>请求显示消息弹窗</summary>
    public event Action<string, string>? RequestShowMessage;

    /// <summary>打开进度弹窗</summary>
    private void OpenProgressDialog(string title)
    {
        _lastProgressValue = 0;
        DialogTitle = title;
        DialogProgress = 0;
        DialogProgressText = "正在准备...";
        DialogIsComplete = false;
        IsScheduleDialogOpen = true;
        RequestOpenProgressDialog?.Invoke();
    }

    /// <summary>关闭进度弹窗</summary>
    private void CloseProgressDialog()
    {
        StopSmoothProgress();
        IsScheduleDialogOpen = false;
        RequestCloseProgressDialog?.Invoke();
    }

    /// <summary>更新弹窗进度</summary>
    private double _lastProgressValue;

    private void UpdateDialogProgress(double value, string? text = null)
    {
        // 进度不允许回退（除非重置为0）
        if (value > 0 && value < _lastProgressValue) value = _lastProgressValue;
        _lastProgressValue = value;
        DialogProgress = Math.Clamp(value, 0, 1);
        DialogProgressText = text ?? $"正在排课... {DialogProgress * 100:F0}%";
    }

    /// <summary>启动平滑进度定时器（求解阶段用）</summary>
    private void StartSmoothProgress(double from, double to)
    {
        StopSmoothProgress();
        _dispatcherQueue ??= Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        double current = Math.Max(from, _lastProgressValue);
        var dispatcher = _dispatcherQueue;
        _progressSmoothTimer = new System.Threading.Timer(_ =>
        {
            // 减速逼近目标：剩余距离的 5%，最低步进0.1%，永远到不了目标
            double remaining = to - current;
            if (remaining > 0.003)
            {
                current += Math.Max(remaining * 0.05, 0.001);
                dispatcher?.TryEnqueue(() => UpdateDialogProgress(current));
            }
        }, null, 300, 300);
    }

    /// <summary>停止平滑进度定时器</summary>
    private void StopSmoothProgress()
    {
        _progressSmoothTimer?.Dispose();
        _progressSmoothTimer = null;
    }

    public string TeacherSearchText
    {
        get => _teacherSearchText;
        set
        {
            if (SetProperty(ref _teacherSearchText, value))
            {
                RefreshViews();
            }
        }
    }

    public string GradeFilterText
    {
        get => _gradeFilterText;
        set
        {
            if (SetProperty(ref _gradeFilterText, value))
            {
                RefreshViews();
            }
        }
    }

    public string CurrentScopeSummary
    {
        get
        {
            return SelectedViewMode switch
            {
                "年级总表" => SelectedGradeInput is null
                    ? "未选择年级"
                    : SelectedGradeInput.GradeName,
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

    private string _exportFolderPath = AppPaths.OutputPath;
    public string ExportFolderPath
    {
        get => _exportFolderPath;
        set => SetProperty(ref _exportFolderPath, value);
    }
    public string DataFilePath => AppPaths.DataFile;

    public string ProjectFilePath
    {
        get => _projectFilePath;
        set
        {
            if (SetProperty(ref _projectFilePath, value))
                OnPropertyChanged(nameof(ProjectDirectory));
        }
    }

    public string ProjectDirectory =>
        string.IsNullOrEmpty(_projectFilePath)
            ? Infrastructure.AppPaths.ProjectsPath
            : Path.GetDirectoryName(_projectFilePath)!;

    public string ProjectName
    {
        get => _projectName;
        set => SetProperty(ref _projectName, value);
    }

    public string ProjectFileName =>
        string.IsNullOrEmpty(_projectFilePath) ? "未保存的项目" : Path.GetFileNameWithoutExtension(_projectFilePath);

    public bool HasActiveProject
    {
        get => _hasActiveProject;
        set
        {
            if (SetProperty(ref _hasActiveProject, value))
            {
                OnPropertyChanged(nameof(ProjectFileName));
                OnPropertyChanged(nameof(ProjectName));
                RefreshHomePageProjects();
            }
        }
    }

    public IReadOnlyList<ProjectInfo> RecentProjects => _recentProjects.Projects;

    /// <summary>当前窗口句柄，由 MainPage 在加载后设置。</summary>
    internal nint WindowHandle { get; set; }

    private List<ProjectInfo> _homePageProjects = new();
    public IReadOnlyList<ProjectInfo> HomePageProjects => _homePageProjects;

    private void RefreshHomePageProjects()
    {
        var merged = new List<ProjectInfo>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in _recentProjects.Projects)
        {
            merged.Add(p);
            seen.Add(p.Path);
        }
        var dir = new DirectoryInfo(AppPaths.ProjectsPath);
        if (dir.Exists)
        {
            // 扫描项目文件（v3：子目录内的 .acsproj 文件）
            foreach (var file in dir.GetFiles("*.acsproj", SearchOption.AllDirectories))
            {
                if (seen.Add(file.FullName))
                {
                    merged.Add(new ProjectInfo
                    {
                        Name = Path.GetFileNameWithoutExtension(file.Name),
                        Path = file.FullName,
                        LastOpen = file.LastWriteTime.ToString("yyyy-MM-dd")
                    });
                }
            }
            // 兼容旧版 v2 目录格式（.acsproj 为目录名，内含 project.acs）
            foreach (var projDir in dir.GetDirectories("*.acsproj"))
            {
                string mainFile = Path.Combine(projDir.FullName, "project.acs");
                if (File.Exists(mainFile) && seen.Add(projDir.FullName))
                {
                    merged.Add(new ProjectInfo
                    {
                        Name = Path.GetFileNameWithoutExtension(projDir.Name),
                        Path = projDir.FullName,
                        LastOpen = projDir.LastWriteTime.ToString("yyyy-MM-dd")
                    });
                }
            }
        }
        _homePageProjects = merged;
        OnPropertyChanged(nameof(HomePageProjects));
    }

    public bool IsToolbarExpanded
    {
        get => _isToolbarExpanded;
        set => SetProperty(ref _isToolbarExpanded, value);
    }

    public void ToggleToolbar()
    {
        IsToolbarExpanded = !IsToolbarExpanded;
    }

    public void CreateProject(string? filePath = null)
    {
        if (string.IsNullOrEmpty(_projectName))
        {
            StatusMessage = "请输入项目名称";
            return;
        }

        if (string.IsNullOrEmpty(filePath))
            filePath = Infrastructure.AppPaths.GetProjectFilePath(_projectName);

        if (File.Exists(filePath))
        {
            StatusMessage = $"项目已存在: {_projectName}.acsproj";
            return;
        }


        DaysPerWeek = 5;
        PeriodsPerDay = 8;
        MorningPeriods = 4;
        AfternoonPeriods = 4;
        IncludeEveningSelfStudy = false;
        EveningPeriods = 2;
        EveningStudyDays = new[] { true, true, true, true, true, false, false };
        SelectedSettingsTabIndex = 0;
        InitDefaultGrades();
        InitDefaultGradeConfigs();
        InitDefaultSubjectsFromTemplate();
        GenerateClasses();
        _currentSubjectGradeName = GradeInputs.FirstOrDefault()?.GradeName ?? "";
        OnPropertyChanged(nameof(CurrentSubjectGradeName));
        OnPropertyChanged(nameof(FilteredSubjects));

        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        SchoolDataSerializer.SerializeToDirectory(filePath, BuildSchoolData(), _projectName);
        _projectFilePath = filePath;
        OnPropertyChanged(nameof(ProjectFileName));
        OnPropertyChanged(nameof(ProjectDirectory));
        HasActiveProject = true;
        CaptureSnapshot();
        _recentProjects.AddOrUpdate(_projectName, filePath);
        RefreshHomePageProjects();
        SelectedMainPage = "配置";
        SelectedConfigPage = "基础设置";
        StatusMessage = $"已创建项目: {_projectName}";
        RefreshViews();
    }

    public void SaveProject(string? filePath = null)
    {
        if (!HasActiveProject) return;

        if (string.IsNullOrEmpty(filePath))
        {
            if (!string.IsNullOrEmpty(_projectFilePath))
            {
                filePath = _projectFilePath;
            }
            else
            {
                // Need to use file picker from UI — return early for UI to handle
                SaveAsCommand.Execute(null);
                return;
            }
        }

        // 确保项目目录存在
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

        // 如果项目名称为空，从文件名推导
        if (string.IsNullOrEmpty(_projectName))
        {
            _projectName = Path.GetFileNameWithoutExtension(filePath);
            OnPropertyChanged(nameof(ProjectName));
        }

        SchoolDataSerializer.SerializeToDirectory(filePath, BuildSchoolData(), _projectName);
        _projectFilePath = filePath;
        OnPropertyChanged(nameof(ProjectFileName));
        OnPropertyChanged(nameof(ProjectDirectory));
        StatusMessage = $"已保存: {ProjectFileName}";
        CaptureSnapshot();
    }

    public bool HasUnsavedChanges
    {
        get
        {
            if (!HasActiveProject || _savedSnapshot == null) return false;
            // 序列化到临时目录进行对比
            string tempDir = Path.Combine(Path.GetTempPath(), "acs_diff_" + Guid.NewGuid().ToString("N"));
            try
            {
                string tempFile = Path.Combine(tempDir, "snapshot.acsproj");
                SchoolDataSerializer.SerializeToDirectory(tempFile, BuildSchoolData(), _projectName);
                // 将目录内容打包为字节数组进行比较
                var current = SerializeDirectoryToBytes(tempDir);
                return !current.AsSpan().SequenceEqual(_savedSnapshot.AsSpan());
            }
            finally
            {
                try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); } catch { }
            }
        }
    }

    private void CaptureSnapshot()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "acs_snap_" + Guid.NewGuid().ToString("N"));
        try
        {
            string tempFile = Path.Combine(tempDir, "snapshot.acsproj");
            SchoolDataSerializer.SerializeToDirectory(tempFile, BuildSchoolData(), _projectName);
            _savedSnapshot = SerializeDirectoryToBytes(tempDir);
        }
        finally
        {
            try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); } catch { }
        }
    }

    private static byte[] SerializeDirectoryToBytes(string dir)
    {
        using var ms = new MemoryStream();
        using var archiveWriter = new BinaryWriter(ms);
        // 写入所有文件内容作为快照
        foreach (var file in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
        {
            string rel = Path.GetRelativePath(dir, file);
            byte[] content = File.ReadAllBytes(file);
            archiveWriter.Write(rel);
            archiveWriter.Write(content.Length);
            archiveWriter.Write(content);
        }
        return ms.ToArray();
    }

    public void CloseProject()
    {
        if (!HasActiveProject) return;
        HasActiveProject = false;
        ClearAllData();
        _projectFilePath = "";
        _projectName = "";
        _savedSnapshot = null;
        OnPropertyChanged(nameof(ProjectName));
        OnPropertyChanged(nameof(ProjectFilePath));
        OnPropertyChanged(nameof(ProjectFileName));
        OnPropertyChanged(nameof(ProjectDirectory));
        StatusMessage = "";
        RefreshHomePageProjects();
    }

    public void OpenProject(string? filePath = null)
    {
        if (string.IsNullOrEmpty(filePath))
            return;

        if (!File.Exists(filePath) && !Directory.Exists(filePath))
        {
            StatusMessage = "文件不存在";
            return;
        }

        // 先释放旧项目数据
        ClearAllData();

        Models.SchoolData? data;
        try
        {
            data = SchoolDataSerializer.DeserializeFromDirectory(filePath);
        }
        catch
        {
            StatusMessage = "项目文件读取失败";
            return;
        }

        if (data == null || data.GradeInputs.Count == 0)
        {
            StatusMessage = "项目文件无效";
            return;
        }

        ApplySchoolData(data);
        _projectFilePath = filePath;

        // 恢复项目名称
        if (!string.IsNullOrEmpty(data.ProjectName))
        {
            _projectName = data.ProjectName;
            OnPropertyChanged(nameof(ProjectName));
        }
        else
        {
            _projectName = Path.GetFileNameWithoutExtension(filePath);
            OnPropertyChanged(nameof(ProjectName));
        }

        HasActiveProject = true;
        CaptureSnapshot();
        _recentProjects.AddOrUpdate(_projectName, filePath);
        RefreshHomePageProjects();
        SelectedMainPage = "配置";
        SelectedConfigPage = "基础设置";
        SelectedViewMode = "年级总表";
        SelectedGradeInput = GradeInputs.FirstOrDefault();
        _currentSubjectGradeName = GradeInputs.FirstOrDefault()?.GradeName ?? "";
        OnPropertyChanged(nameof(CurrentSubjectGradeName));
        OnPropertyChanged(nameof(FilteredSubjects));
        SelectedClass = Classes.FirstOrDefault();
        SelectedTeacher = Teachers.FirstOrDefault();
        StatusMessage = $"已打开: {ProjectFileName}";
        RefreshViews();
    }

    public Microsoft.UI.Xaml.Visibility ConfigPageVisibility =>
        SelectedMainPage == "配置" ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
    public Microsoft.UI.Xaml.Visibility SchedulePageVisibility =>
        SelectedMainPage == "课表" ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
    public Microsoft.UI.Xaml.Visibility ExportPageVisibility =>
        SelectedMainPage == "导出" ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

    public bool IsBasicSettingsVisible => SelectedMainPage == "配置" && SelectedConfigPage == "基础设置";
    public bool IsClassConfigVisible => SelectedMainPage == "配置" && SelectedConfigPage == "班级配置";
    public bool IsSubjectConfigVisible => SelectedMainPage == "配置" && SelectedConfigPage == "课程配置";
    public bool IsTeacherConfigVisible => SelectedMainPage == "配置" && SelectedConfigPage == "教师配置";
    public bool IsFixedLessonConfigVisible => SelectedMainPage == "配置" && SelectedConfigPage == "固定课程";
    public bool IsAutoScheduleVisible => SelectedMainPage == "配置" && SelectedConfigPage == "自动排课";

    public bool IsGradeViewMode => SelectedViewMode == "年级总表";
    public bool IsClassViewMode => SelectedViewMode == "班级课表";
    public bool IsTeacherViewMode => SelectedViewMode == "教师课表";

    public int TotalClasses => Classes.Count;
    public int TotalSubjects => Subjects.Count;
    public int TotalAssignments => TeacherAssignments.Count;
    public int TotalRequirements => Requirements.Count;
    public int TotalScheduleEntries => ScheduleEntries.Count;
    public int TotalConflicts => Conflicts.Count;

    public List<string> DistributionRuleOptions { get; } = new() { "均匀分布", "每日至少一次", "集中安排" };

    public List<GradeInput> FilteredGrades => string.IsNullOrWhiteSpace(GradeFilterText)
        ? GradeInputs.ToList()
        : GradeInputs.Where(g => g.GradeName.Contains(GradeFilterText, StringComparison.OrdinalIgnoreCase)).ToList();

    public List<Teacher> FilteredTeachers => string.IsNullOrWhiteSpace(TeacherSearchText)
        ? Teachers.ToList()
        : Teachers.Where(t => t.Name.Contains(TeacherSearchText, StringComparison.OrdinalIgnoreCase)
            || t.Subject.Contains(TeacherSearchText, StringComparison.OrdinalIgnoreCase)).ToList();

    public List<SchoolClass> FilteredClasses => string.IsNullOrWhiteSpace(GradeFilterText)
        ? Classes.ToList()
        : Classes.Where(c => c.GradeName.Contains(GradeFilterText, StringComparison.OrdinalIgnoreCase)).ToList();

    public string SelectedCourseTemplate
    {
        get => _selectedCourseTemplate;
        set => SetProperty(ref _selectedCourseTemplate, value);
    }

    public string CurrentSubjectGradeName
    {
        get => _currentSubjectGradeName;
        set
        {
            if (SetProperty(ref _currentSubjectGradeName, value))
            {
                OnPropertyChanged(nameof(FilteredSubjects));
                OnPropertyChanged(nameof(IsAllSubjectsGrade));
                OnPropertyChanged(nameof(IsSubjectGradeSelected));
                OnPropertyChanged(nameof(IsFixedTimeTabSelected));
                OnPropertyChanged(nameof(IsSubjectGridVisible));
            }
        }
    }

    public List<SubjectDefinition> FilteredSubjects => string.IsNullOrWhiteSpace(CurrentSubjectGradeName) || CurrentSubjectGradeName == "全部" || CurrentSubjectGradeName == "固定时间"
        ? Subjects.ToList()
        : Subjects.Where(s => s.GradeName == CurrentSubjectGradeName).ToList();

    public bool IsAllSubjectsGrade => CurrentSubjectGradeName == "全部";
    public bool IsSubjectGradeSelected => !string.IsNullOrWhiteSpace(CurrentSubjectGradeName) && CurrentSubjectGradeName != "全部" && CurrentSubjectGradeName != "固定时间";
    /// <summary>当前选中的是"固定时间"标签页</summary>
    public bool IsFixedTimeTabSelected => CurrentSubjectGradeName == "固定时间";
    /// <summary>课程列表区域是否可见（非固定时间标签时显示）</summary>
    public bool IsSubjectGridVisible => CurrentSubjectGradeName != "固定时间";

    public RelayCommand SeedSampleDataCommand { get; }
    public RelayCommand GenerateClassesCommand { get; }
    public RelayCommand GenerateRequirementsCommand { get; }
    public RelayCommand GenerateAssignmentsCommand { get; }
    public RelayCommand AddTeacherAssignmentCommand { get; }
    public RelayCommand DeleteTeacherAssignmentCommand { get; }
    public RelayCommand GenerateTeacherTemplateCommand { get; }
    public RelayCommand ImportTeacherListCommand { get; }
    public RelayCommand GenerateTeachersCommand { get; }
    public RelayCommand AddFixedLessonCommand { get; }
    public RelayCommand DeleteFixedLessonCommand { get; }
    public RelayCommand AutoScheduleCommand { get; }
    public RelayCommand ValidateCommand { get; }
    public RelayCommand ClearConflictsCommand { get; }
    public RelayCommand LocalAdjustCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand LoadCommand { get; }
    public RelayCommand NewProjectCommand { get; }
    public RelayCommand ExportCommand { get; }
    public RelayCommand SelectExportFolderCommand { get; }
    public RelayCommand ImportCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand UseFiveDayCommand { get; }
    public RelayCommand UseSevenDayCommand { get; }
    public RelayCommand<string> SelectMainPageCommand { get; }
    public RelayCommand<string> SelectConfigPageCommand { get; }
    public RelayCommand<string> SelectViewModeCommand { get; }
    public RelayCommand<string> SelectSubjectGradeCommand { get; }
    public RelayCommand<GradeInput> SelectGradeCommand { get; }
    public RelayCommand<SchoolClass> SelectClassCommand { get; }
    public RelayCommand<Teacher> SelectTeacherCommand { get; }
    public RelayCommand<int> SelectDayCommand { get; }
    public RelayCommand SearchTeacherCommand { get; }
    public RelayCommand AddSubjectCommand { get; }
    public RelayCommand DeleteSubjectCommand { get; }
    public RelayCommand SaveCourseTemplateCommand { get; }
    public RelayCommand LoadCourseTemplateCommand { get; }
    public RelayCommand DeleteCourseTemplateCommand { get; }
    public RelayCommand ToggleToolbarCommand { get; }
    public RelayCommand<string?> SaveAsCommand { get; }
    public RelayCommand<string?> OpenProjectCommand { get; }
    public RelayCommand CreateProjectCommand { get; }
    public RelayCommand FilterGradeCommand { get; }
    public RelayCommand<int> ToggleEveningDayCommand { get; }
    public RelayCommand<string> ToggleGradeEveningDayCommand { get; }
    public RelayCommand<int> SelectSettingsTabCommand { get; }

    public string SerializeSubjects()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(Subjects, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        return json;
    }

    public void DeserializeSubjects(string json)
    {
        var subjects = System.Text.Json.JsonSerializer.Deserialize<System.Collections.ObjectModel.ObservableCollection<SubjectDefinition>>(json);
        if (subjects != null)
        {
            Subjects.Clear();
            foreach (var s in subjects)
                Subjects.Add(s);
        }
    }

    public void LoadCourseTemplates()
    {
        CourseTemplates.Clear();
        string filePath = AppPaths.TemplatesFile;
        if (File.Exists(filePath))
        {
            var json = File.ReadAllText(filePath);
            var names = System.Text.Json.JsonSerializer.Deserialize<List<string>>(json);
            if (names != null)
            {
                foreach (var n in names)
                    CourseTemplates.Add(n);
            }
        }

        // 始终确保默认模板存在且为最新版本
        SeedDefaults();

        OnPropertyChanged(nameof(CourseTemplates));
        _selectedCourseTemplate = CourseTemplates.FirstOrDefault() ?? "";
        OnPropertyChanged(nameof(SelectedCourseTemplate));
    }

    private void SeedDefaults()
    {
        // 将内置初中标准配置写入模板文件，使其可在模板下拉列表中载入
        const string name = "初中标准";
        var templateData = GetBuiltInTemplate();
        var json = System.Text.Json.JsonSerializer.Serialize(templateData, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        try
        {
            string dir = Path.GetDirectoryName(AppPaths.TemplatesFile)!;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            string path = GetTemplateFilePath(name);
            File.WriteAllText(path, json);
        }
        catch { /* 文件权限受限时静默跳过 */ }
        if (!CourseTemplates.Contains(name))
            CourseTemplates.Add(name);

        try { SaveTemplatesToDisk(); } catch { /* 文件权限受限时静默跳过 */ }
    }

    private void SaveTemplatesToDisk()
    {
        string dir = Path.GetDirectoryName(AppPaths.TemplatesFile)!;
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var names = CourseTemplates.ToList();
        var json = System.Text.Json.JsonSerializer.Serialize(names);
        File.WriteAllText(AppPaths.TemplatesFile, json);

        // Save individual template files
        foreach (var name in CourseTemplates)
        {
            SaveTemplateContent(name);
        }
    }

    private string GetTemplateFilePath(string name)
    {
        string safe = string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(Path.GetDirectoryName(AppPaths.TemplatesFile)!, $"template_{safe}.json");
    }

    private string LoadTemplateContent(string name)
    {
        string path = GetTemplateFilePath(name);
        return File.Exists(path) ? File.ReadAllText(path) : "[]";
    }

    private void SaveTemplateContent(string name)
    {
        // Only save if name exists in our template list
        var dict = new Dictionary<string, string>();
        string path = GetTemplateFilePath(name);

        // If there's a JSON file for this template name, keep it
        if (File.Exists(path))
            return;

        // For seeded defaults, write the content
        var subjects = GetDefaultTemplate(name);
        if (subjects != null)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(subjects, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }
    }

    private List<SubjectDefinition>? GetDefaultTemplate(string name)
    {
        return name switch
        {
            "初中标准" => new()
            {
                new() { Name = "语文", Category = "主科", DefaultWeeklyCount = 6, DistributionRule = "每天一次", GradeName = "七年级" },
                new() { Name = "语文", Category = "主科", DefaultWeeklyCount = 6, DistributionRule = "每天一次", GradeName = "八年级" },
                new() { Name = "语文", Category = "主科", DefaultWeeklyCount = 6, DistributionRule = "每天一次", GradeName = "九年级" },
                new() { Name = "数学", Category = "主科", DefaultWeeklyCount = 6, DistributionRule = "每天一次", GradeName = "七年级" },
                new() { Name = "数学", Category = "主科", DefaultWeeklyCount = 6, DistributionRule = "每天一次", GradeName = "八年级" },
                new() { Name = "数学", Category = "主科", DefaultWeeklyCount = 6, DistributionRule = "每天一次", GradeName = "九年级" },
                new() { Name = "英语", Category = "主科", DefaultWeeklyCount = 5, DistributionRule = "每天一次", GradeName = "七年级" },
                new() { Name = "英语", Category = "主科", DefaultWeeklyCount = 5, DistributionRule = "每天一次", GradeName = "八年级" },
                new() { Name = "英语", Category = "主科", DefaultWeeklyCount = 5, DistributionRule = "每天一次", GradeName = "九年级" },
            },
            "初中标准（含理科）" => new()
            {
                new() { Name = "语文", Category = "主科", DefaultWeeklyCount = 6, DistributionRule = "每天一次", GradeName = "七年级" },
                new() { Name = "数学", Category = "主科", DefaultWeeklyCount = 6, DistributionRule = "每天一次", GradeName = "七年级" },
                new() { Name = "英语", Category = "主科", DefaultWeeklyCount = 5, DistributionRule = "每天一次", GradeName = "七年级" },
                new() { Name = "生物", Category = "理科", DefaultWeeklyCount = 2, DistributionRule = "均衡分布", GradeName = "七年级" },
                new() { Name = "地理", Category = "文科", DefaultWeeklyCount = 2, DistributionRule = "均衡分布", GradeName = "七年级" },
                new() { Name = "历史", Category = "文科", DefaultWeeklyCount = 2, DistributionRule = "均衡分布", GradeName = "七年级" },
                new() { Name = "道德", Category = "文科", DefaultWeeklyCount = 2, DistributionRule = "均衡分布", GradeName = "七年级" },
                new() { Name = "体育", Category = "副科", DefaultWeeklyCount = 2, DistributionRule = "均衡分布", GradeName = "七年级" },
                new() { Name = "语文", Category = "主科", DefaultWeeklyCount = 6, DistributionRule = "每天一次", GradeName = "八年级" },
                new() { Name = "数学", Category = "主科", DefaultWeeklyCount = 6, DistributionRule = "每天一次", GradeName = "八年级" },
                new() { Name = "英语", Category = "主科", DefaultWeeklyCount = 5, DistributionRule = "每天一次", GradeName = "八年级" },
                new() { Name = "物理", Category = "理科", DefaultWeeklyCount = 3, DistributionRule = "均衡分布", GradeName = "八年级" },
                new() { Name = "生物", Category = "理科", DefaultWeeklyCount = 2, DistributionRule = "均衡分布", GradeName = "八年级" },
                new() { Name = "地理", Category = "文科", DefaultWeeklyCount = 2, DistributionRule = "均衡分布", GradeName = "八年级" },
                new() { Name = "历史", Category = "文科", DefaultWeeklyCount = 2, DistributionRule = "均衡分布", GradeName = "八年级" },
                new() { Name = "道德", Category = "文科", DefaultWeeklyCount = 2, DistributionRule = "均衡分布", GradeName = "八年级" },
                new() { Name = "体育", Category = "副科", DefaultWeeklyCount = 2, DistributionRule = "均衡分布", GradeName = "八年级" },
                new() { Name = "语文", Category = "主科", DefaultWeeklyCount = 6, DistributionRule = "每天一次", GradeName = "九年级" },
                new() { Name = "数学", Category = "主科", DefaultWeeklyCount = 6, DistributionRule = "每天一次", GradeName = "九年级" },
                new() { Name = "英语", Category = "主科", DefaultWeeklyCount = 5, DistributionRule = "每天一次", GradeName = "九年级" },
                new() { Name = "物理", Category = "理科", DefaultWeeklyCount = 3, DistributionRule = "均衡分布", GradeName = "九年级" },
                new() { Name = "化学", Category = "理科", DefaultWeeklyCount = 3, DistributionRule = "均衡分布", GradeName = "九年级" },
                new() { Name = "历史", Category = "文科", DefaultWeeklyCount = 2, DistributionRule = "均衡分布", GradeName = "九年级" },
                new() { Name = "道德", Category = "文科", DefaultWeeklyCount = 2, DistributionRule = "均衡分布", GradeName = "九年级" },
                new() { Name = "体育", Category = "副科", DefaultWeeklyCount = 2, DistributionRule = "均衡分布", GradeName = "九年级" },
            },
            _ => null,
        };
    }

    public void SaveCourseTemplate(string? templateName = null)
    {
        string dir = Path.GetDirectoryName(AppPaths.TemplatesFile)!;
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        string name = string.IsNullOrWhiteSpace(templateName)
            ? $"自定义模板 {CourseTemplates.Count + 1}"
            : templateName.Trim();

        // 如果同名模板已存在，覆盖
        if (!CourseTemplates.Contains(name))
            CourseTemplates.Add(name);

        var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
        string path = GetTemplateFilePath(name);
        var templateData = new CourseTemplateData
        {
            Subjects = Subjects.ToList(),
            FixedLessons = FixedLessons.ToList()
        };
        var json = System.Text.Json.JsonSerializer.Serialize(templateData, options);
        File.WriteAllText(path, json);

        SaveTemplatesToDisk();
        OnPropertyChanged(nameof(CourseTemplates));
        _selectedCourseTemplate = name;
        OnPropertyChanged(nameof(SelectedCourseTemplate));
        Log($"已保存课程模板: {name}");
        StatusMessage = $"已保存课程模板: {name}";
    }

    public void LoadCourseTemplate()
    {
        if (string.IsNullOrEmpty(_selectedCourseTemplate))
            return;

        string path = GetTemplateFilePath(_selectedCourseTemplate);
        if (!File.Exists(path))
        {
            StatusMessage = "模板文件不存在";
            return;
        }

        var json = File.ReadAllText(path);
        LoadTemplateFromJson(json);
        StatusMessage = $"已载入模板: {_selectedCourseTemplate}";
    }

    /// <summary>从 JSON 加载模板数据</summary>
    private void LoadTemplateFromJson(string json)
    {
        var templateData = System.Text.Json.JsonSerializer.Deserialize<CourseTemplateData>(json);
        if (templateData != null)
        {
            Subjects.Clear();
            foreach (var s in templateData.Subjects)
                Subjects.Add(s);
            FixedLessons.Clear();
            foreach (var fl in templateData.FixedLessons)
                FixedLessons.Add(fl);
        }
        OnPropertyChanged(nameof(FilteredSubjects));
    }

    public void DeleteCourseTemplate()
    {
        if (string.IsNullOrEmpty(_selectedCourseTemplate))
            return;

        string path = GetTemplateFilePath(_selectedCourseTemplate);
        if (File.Exists(path))
            File.Delete(path);

        CourseTemplates.Remove(_selectedCourseTemplate);
        SaveTemplatesToDisk();
        _selectedCourseTemplate = CourseTemplates.FirstOrDefault() ?? "";
        OnPropertyChanged(nameof(SelectedCourseTemplate));
        StatusMessage = $"已删除模板";
    }

    public void MoveEntry(ScheduleEntry entry, int dayIndex, int periodIndex)
    {
        if (_scheduleService.TryMoveEntry(BuildSchoolData(), entry, dayIndex, periodIndex, out _))
        {
            entry.DayIndex = dayIndex;
            entry.PeriodIndex = periodIndex;
            entry.Note = "手动调整";
            Log($"已移动 {entry.Subject}-{entry.TeacherName} 到 {entry.SlotLabel}");
            ValidateSchedule();
            RefreshViews();
            return;
        }

        StatusMessage = "移动失败：存在冲突";
    }

    public void SwapEntries(ScheduleEntry source, ScheduleEntry target)
    {
        if (_scheduleService.TrySwapEntries(BuildSchoolData(), source, target, out _))
        {
            source.Note = "换课";
            target.Note = "换课";
            Log($"已换课 {source.Subject}-{source.TeacherName} <-> {target.Subject}-{target.TeacherName}");
            ValidateSchedule();
            RefreshViews();
            return;
        }

        StatusMessage = "换课失败：存在冲突";
    }

    /// <summary>拖拽后最小变化重排</summary>
    public async Task DragRescheduleAsync(ScheduleEntry draggedEntry, int targetDay, int targetPeriod, ScheduleEntry? targetEntry)
    {
        if (IsBusy) return;

        // ===== 拦截固定课拖拽 =====
        if (draggedEntry.IsFixed)
        {
            AddInfoMessage("无法拖动", "固定课程不允许拖动调整");
            return;
        }
        if (targetEntry?.IsFixed == true)
        {
            AddInfoMessage("无法放置", "目标位置是固定课程，不允许交换");
            return;
        }

        // ===== 同班交换：直接交换位置，冲突仅提示 =====
        if (targetEntry != null && draggedEntry.ClassName == targetEntry.ClassName)
        {
            int sDay = draggedEntry.DayIndex, sPeriod = draggedEntry.PeriodIndex;
            draggedEntry.DayIndex = targetEntry.DayIndex;
            draggedEntry.PeriodIndex = targetEntry.PeriodIndex;
            targetEntry.DayIndex = sDay;
            targetEntry.PeriodIndex = sPeriod;
            draggedEntry.Locked = true;
            targetEntry.Locked = true;
            draggedEntry.Note = "换课";
            targetEntry.Note = "换课";

            // 检测教师冲突并提示
            SchoolData data = BuildSchoolData();
            var conflicts = _scheduleService.Validate(data);
            Conflicts.Clear();
            foreach (var c in conflicts) Conflicts.Add(c);
            OnPropertyChanged(nameof(TotalConflicts));

            StatusMessage = $"已交换 {draggedEntry.Subject} 和 {targetEntry.Subject}（{draggedEntry.ClassName}）";
            if (conflicts.Count > 0)
                AddInfoMessage("换课完成", $"存在 {conflicts.Count} 条冲突提示，可点击“局部调整”自动修复");
            Log($"同班换课: {draggedEntry.Subject}↔{targetEntry.Subject} ({draggedEntry.ClassName})");
            RefreshViews();
            return;
        }

        // ===== 跨班交换：仅允许同科目互换时间槽，拆分为两个同班交换 =====
        if (targetEntry != null)
        {
            // 跨班验证：被拖动课程的老师必须带目标班级
            bool draggedTeacherCoversTarget = draggedEntry.TeacherName == targetEntry.TeacherName
                || TeacherAssignments.Any(a => a.TeacherName == draggedEntry.TeacherName
                    && !string.IsNullOrEmpty(a.ClassNames)
                    && a.ClassNames.Contains(targetEntry.ClassName));
            if (!draggedTeacherCoversTarget)
            {
                AddInfoMessage("无法交换", $"{draggedEntry.TeacherName} 不带 {targetEntry.ClassName}，不能跨班交换");
                return;
            }
            int srcDay = draggedEntry.DayIndex, srcPeriod = draggedEntry.PeriodIndex;
            int tgtDay = targetEntry.DayIndex, tgtPeriod = targetEntry.PeriodIndex;

            // 找 draggedEntry 班级在目标位置的课程（同班同时段）
            var classXMate = ScheduleEntries.FirstOrDefault(e =>
                e.ClassId == draggedEntry.ClassId && e.DayIndex == tgtDay && e.PeriodIndex == tgtPeriod && e.Id != draggedEntry.Id);
            // 找 targetEntry 班级在源位置的课程
            var classYMate = ScheduleEntries.FirstOrDefault(e =>
                e.ClassId == targetEntry.ClassId && e.DayIndex == srcDay && e.PeriodIndex == srcPeriod && e.Id != targetEntry.Id);

            // 同班交换1：draggedEntry 与 classXMate 交换位置（draggedEntry班级内）
            if (classXMate != null && !classXMate.IsFixed)
            {
                draggedEntry.DayIndex = tgtDay;
                draggedEntry.PeriodIndex = tgtPeriod;
                classXMate.DayIndex = srcDay;
                classXMate.PeriodIndex = srcPeriod;
                draggedEntry.Locked = true;
                classXMate.Locked = true;
                draggedEntry.Note = "换课";
                classXMate.Note = "换课";
            }
            else
            {
                // 目标位置无同班课程（空位），直接移动
                draggedEntry.DayIndex = tgtDay;
                draggedEntry.PeriodIndex = tgtPeriod;
                draggedEntry.Locked = true;
                draggedEntry.Note = "手动调整";
            }

            // 同班交换2：targetEntry 与 classYMate 交换位置（targetEntry班级内）
            if (classYMate != null && !classYMate.IsFixed)
            {
                targetEntry.DayIndex = srcDay;
                targetEntry.PeriodIndex = srcPeriod;
                classYMate.DayIndex = tgtDay;
                classYMate.PeriodIndex = tgtPeriod;
                targetEntry.Locked = true;
                classYMate.Locked = true;
                targetEntry.Note = "换课";
                classYMate.Note = "换课";
            }
            else
            {
                targetEntry.DayIndex = srcDay;
                targetEntry.PeriodIndex = srcPeriod;
                targetEntry.Locked = true;
                targetEntry.Note = "手动调整";
            }
        }
        else
        {
            // 移动到空位：同班交换
            var classMate = ScheduleEntries.FirstOrDefault(e =>
                e.ClassId == draggedEntry.ClassId && e.DayIndex == targetDay && e.PeriodIndex == targetPeriod && e.Id != draggedEntry.Id);
            if (classMate != null && !classMate.IsFixed)
            {
                int origDay = draggedEntry.DayIndex, origPeriod = draggedEntry.PeriodIndex;
                draggedEntry.DayIndex = targetDay;
                draggedEntry.PeriodIndex = targetPeriod;
                classMate.DayIndex = origDay;
                classMate.PeriodIndex = origPeriod;
                draggedEntry.Locked = true;
                classMate.Locked = true;
                draggedEntry.Note = "换课";
                classMate.Note = "换课";
            }
            else
            {
                draggedEntry.DayIndex = targetDay;
                draggedEntry.PeriodIndex = targetPeriod;
                draggedEntry.Locked = true;
                draggedEntry.Note = "手动调整";
            }
        }

        // 检测冲突并提示（显示具体原因）
        var conflictDetails = ScheduleEntries
            .Where(e => e.TeacherId != Guid.Empty && !e.IsFixed)
            .GroupBy(e => (e.TeacherName, e.DayIndex, e.PeriodIndex))
            .Where(g => g.Select(e => e.ClassId).Distinct().Count() > 1)
            .Select(g => $"{g.Key.TeacherName} 在{GetDayName(g.Key.DayIndex)}第{g.Key.PeriodIndex}节同时出现在 {string.Join("、", g.Select(e => e.ClassName).Distinct())}")
            .ToList();

        SchoolData data2 = BuildSchoolData();
        var conflicts2 = _scheduleService.Validate(data2);
        Conflicts.Clear();
        foreach (var c in conflicts2) Conflicts.Add(c);
        OnPropertyChanged(nameof(TotalConflicts));

        StatusMessage = $"已调整 {draggedEntry.Subject}（{draggedEntry.ClassName}）的位置";
        if (conflictDetails.Count > 0)
        {
            foreach (var detail in conflictDetails.Take(3))
                AddInfoMessage("教师冲突", detail + "，可点击“局部调整”修复");
        }
        Log($"拖拽调整: {draggedEntry.Subject} ({draggedEntry.ClassName})");
        RefreshViews();
    }

    /// <summary>同班交换有教师冲突时：扩大解锁范围求解，尽量改动小</summary>
    private async Task MinimalChangeSolveAsync(ScheduleEntry swappedA, ScheduleEntry swappedB)
    {
        IsBusy = true;
        try
        {
            _cts = new CancellationTokenSource();
            SchoolData data = BuildSchoolData();

            var allEntries = ScheduleEntries.ToList();
            int expectedCount = allEntries.Count;

            // 收集涉及的教师ID和天数
            var involvedTeachers = new HashSet<Guid>();
            var involvedDays = new HashSet<int>();
            if (swappedA.TeacherId != Guid.Empty) involvedTeachers.Add(swappedA.TeacherId);
            if (swappedB.TeacherId != Guid.Empty) involvedTeachers.Add(swappedB.TeacherId);
            involvedDays.Add(swappedA.DayIndex);
            involvedDays.Add(swappedB.DayIndex);

            // 第一轮：解锁涉及教师当天的所有课程（交换的entry保持锁定）
            var unlockIds = new HashSet<Guid>();
            foreach (var entry in allEntries)
            {
                if (entry.Id == swappedA.Id || entry.Id == swappedB.Id) continue;
                if (entry.IsFixed) continue;
                // 同教师且同天的条目解锁
                if (involvedTeachers.Contains(entry.TeacherId) && involvedDays.Contains(entry.DayIndex))
                    unlockIds.Add(entry.Id);
            }

            foreach (var entry in allEntries)
            {
                entry.Locked = !unlockIds.Contains(entry.Id) && !entry.IsFixed;
            }
            swappedA.Locked = true;
            swappedB.Locked = true;

            var locks = allEntries
                .Where(e => e.Locked && !e.IsFixed && e.RequirementId != Guid.Empty)
                .Select(e => new LockedLesson
                {
                    RequirementId = e.RequirementId,
                    EntryId = e.Id,
                    DayIndex = e.DayIndex,
                    PeriodIndex = e.PeriodIndex
                }).ToList();

            OpenProgressDialog("局部调整");
            UpdateDialogProgress(0.10, "正在局部求解...");
            StartSmoothProgress(0.10, 0.95);

            ScheduleResult result = await Task.Run(
                () => _scheduleService.GenerateWithLocks(data, locks, null, _cts.Token, relaxLevel: 1),
                _cts.Token);

            StopSmoothProgress();

            // 第一轮失败：尝试更宽范围（解锁涉及教师的所有课程，不限天）
            if (result.Entries.Count < expectedCount)
            {
                UpdateDialogProgress(0.50, "扩大范围重新求解...");
                unlockIds.Clear();
                foreach (var entry in allEntries)
                {
                    if (entry.Id == swappedA.Id || entry.Id == swappedB.Id) continue;
                    if (entry.IsFixed) continue;
                    if (involvedTeachers.Contains(entry.TeacherId))
                        unlockIds.Add(entry.Id);
                }
                foreach (var entry in allEntries)
                {
                    entry.Locked = !unlockIds.Contains(entry.Id) && !entry.IsFixed;
                }
                swappedA.Locked = true;
                swappedB.Locked = true;

                locks = allEntries
                    .Where(e => e.Locked && !e.IsFixed && e.RequirementId != Guid.Empty)
                    .Select(e => new LockedLesson
                    {
                        RequirementId = e.RequirementId,
                        EntryId = e.Id,
                        DayIndex = e.DayIndex,
                        PeriodIndex = e.PeriodIndex
                    }).ToList();

                StartSmoothProgress(0.50, 0.95);
                result = await Task.Run(
                    () => _scheduleService.GenerateWithLocks(data, locks, null, _cts.Token, relaxLevel: 1),
                    _cts.Token);
                StopSmoothProgress();
            }

            // 两轮都失败：回退交换
            if (result.Entries.Count < expectedCount)
            {
                int tmpDay = swappedA.DayIndex, tmpPeriod = swappedA.PeriodIndex;
                swappedA.DayIndex = swappedB.DayIndex;
                swappedA.PeriodIndex = swappedB.PeriodIndex;
                swappedB.DayIndex = tmpDay;
                swappedB.PeriodIndex = tmpPeriod;
                swappedA.Locked = false;
                swappedB.Locked = false;

                CloseProgressDialog();
                StatusMessage = "修改后无解，已恢复上一次状态";
                Log("最小变化求解失败，已回退");
                AddInfoMessage("修改后无解", "该交换导致教师冲突无法解决，已自动恢复原位");
                RefreshViews();
                return;
            }

            UpdateDialogProgress(0.95, "正在整理结果...");

            ScheduleEntries.Clear();
            foreach (var entry in result.Entries.OrderBy(x => x.DayIndex).ThenBy(x => x.PeriodIndex))
                ScheduleEntries.Add(entry);

            Conflicts.Clear();
            foreach (var conflict in result.Conflicts)
                Conflicts.Add(conflict);

            OnPropertyChanged(nameof(TotalScheduleEntries));
            OnPropertyChanged(nameof(TotalConflicts));

            UpdateDialogProgress(0.99, "即将完成...");
            await Task.Delay(800);
            UpdateDialogProgress(1.0, "局部调整完成！");
            DialogIsComplete = true;
            StatusMessage = $"局部调整完成（{unlockIds.Count + 2} 节课参与调整）";
            Log($"最小变化求解: {unlockIds.Count + 2} 节课调整");
            RefreshViews();
        }
        catch (OperationCanceledException)
        {
            StopSmoothProgress();
            CloseProgressDialog();
            StatusMessage = "局部调整已取消";
            RefreshViews();
        }
        catch (Exception ex)
        {
            StopSmoothProgress();
            CloseProgressDialog();
            StatusMessage = $"局部调整失败: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            _cts = null;
        }
    }

    /// <summary>局部调整按钮：渐进放大求解范围 + 放宽约束，两个方向分别尝试</summary>
    private async Task LocalAdjustAsync()
    {
        if (IsBusy || ScheduleEntries.Count == 0) return;
        IsBusy = true;

        int expectedEntryCount = ScheduleEntries.Count;
        var snapshot = ScheduleEntries.Select(e => (e.Id, e.DayIndex, e.PeriodIndex, e.Locked)).ToList();

        try
        {
            _cts = new CancellationTokenSource();
            OpenProgressDialog("局部调整");
            UpdateDialogProgress(0.03, "正在检测冲突...");

            // 检测当前教师时间槽冲突
            var conflictGroups = ScheduleEntries
                .Where(e => e.TeacherId != Guid.Empty && !e.IsFixed)
                .GroupBy(e => (e.TeacherName, e.DayIndex, e.PeriodIndex))
                .Where(g => g.Select(e => e.ClassId).Distinct().Count() > 1)
                .ToList();

            if (conflictGroups.Count == 0)
            {
                CloseProgressDialog();
                StatusMessage = "当前无冲突，无需调整";
                AddInfoMessage("局部调整", "当前课表没有教师冲突，无需调整");
                return;
            }

            var conflictTeacherNames = conflictGroups.Select(g => g.Key.TeacherName).ToHashSet();
            var conflictDays = conflictGroups.Select(g => g.Key.DayIndex).ToHashSet();
            var allEntries = ScheduleEntries.ToList();

            // 定义多轮求解策略
            var rounds = new List<(string Label, HashSet<Guid> UnlockIds, int RelaxLevel)>
            {
                // 方向A：逐渐放大修改范围（relaxLevel=1）
                ("放大范围：冲突教师当天课程",
                    new HashSet<Guid>(allEntries
                        .Where(e => !e.IsFixed && e.RequirementId != Guid.Empty
                            && conflictTeacherNames.Contains(e.TeacherName)
                            && conflictDays.Contains(e.DayIndex))
                        .Select(e => e.Id)),
                    1),
                ("放大范围：冲突教师全部课程",
                    new HashSet<Guid>(allEntries
                        .Where(e => !e.IsFixed && e.RequirementId != Guid.Empty
                            && conflictTeacherNames.Contains(e.TeacherName))
                        .Select(e => e.Id)),
                    1),
                ("放大范围：所有课程",
                    new HashSet<Guid>(allEntries
                        .Where(e => !e.IsFixed && e.RequirementId != Guid.Empty)
                        .Select(e => e.Id)),
                    1),
                // 方向B：放宽约束（relaxLevel=2，固定范围=冲突教师全部课程）
                ("放宽约束：副科连天+第一天限制",
                    new HashSet<Guid>(allEntries
                        .Where(e => !e.IsFixed && e.RequirementId != Guid.Empty
                            && conflictTeacherNames.Contains(e.TeacherName))
                        .Select(e => e.Id)),
                    2),
            };

            SchoolData data = BuildSchoolData();
            ScheduleResult? bestResult = null;
            int bestConflictCount = int.MaxValue;
            double progressBase = 0.05;
            double progressPerRound = 0.90 / rounds.Count;

            for (int i = 0; i < rounds.Count; i++)
            {
                _cts.Token.ThrowIfCancellationRequested();
                var (label, unlockIds, relaxLevel) = rounds[i];
                double roundStart = progressBase + i * progressPerRound;
                UpdateDialogProgress(roundStart, $"第{i + 1}轮：{label}...");

                // 构建锁定：不在解锁范围内的条目都锁定
                var locks = allEntries
                    .Where(e => !e.IsFixed && e.RequirementId != Guid.Empty && !unlockIds.Contains(e.Id))
                    .Select(e => new LockedLesson
                    {
                        RequirementId = e.RequirementId,
                        EntryId = e.Id,
                        DayIndex = e.DayIndex,
                        PeriodIndex = e.PeriodIndex
                    }).ToList();

                StartSmoothProgress(roundStart, roundStart + progressPerRound);
                ScheduleResult result = await Task.Run(
                    () => _scheduleService.GenerateWithLocks(data, locks, null, _cts.Token, relaxLevel),
                    _cts.Token);
                StopSmoothProgress();

                if (result.Entries.Count >= expectedEntryCount)
                {
                    // 检查结果中的教师冲突数
                    int resultConflicts = result.Entries
                        .Where(e => e.TeacherId != Guid.Empty && !e.IsFixed)
                        .GroupBy(e => (e.TeacherName, e.DayIndex, e.PeriodIndex))
                        .Count(g => g.Select(e => e.ClassId).Distinct().Count() > 1);

                    if (resultConflicts < bestConflictCount)
                    {
                        bestResult = result;
                        bestConflictCount = resultConflicts;
                    }

                    if (resultConflicts == 0)
                    {
                        // 冲突完全解决
                        ApplyLocalAdjustResult(result, expectedEntryCount);
                        UpdateDialogProgress(0.99, "即将完成...");
                        await Task.Delay(800);
                        UpdateDialogProgress(1.0, "局部调整完成！");
                        DialogIsComplete = true;
                        int changedCount = result.Entries.Count(e => e.Note != "锁定课程" && !e.IsFixed);
                        StatusMessage = $"局部调整完成：{changedCount} 节课被优化，冲突已全部解决";
                        Log($"局部调整第{i + 1}轮成功: {label}, {changedCount} 节课变动");
                        RefreshViews();
                        return;
                    }
                }
            }

            // 所有轮次完毕，使用最佳结果（如果有）
            if (bestResult != null)
            {
                ApplyLocalAdjustResult(bestResult, expectedEntryCount);
                UpdateDialogProgress(0.99, "即将完成...");
                await Task.Delay(800);
                UpdateDialogProgress(1.0, "局部调整完成");
                DialogIsComplete = true;
                int changedCount = bestResult.Entries.Count(e => e.Note != "锁定课程" && !e.IsFixed);
                StatusMessage = bestConflictCount == 0
                    ? $"局部调整完成：{changedCount} 节课被优化"
                    : $"局部调整完成：剩余 {bestConflictCount} 条冲突（已尽量减少）";
                if (bestConflictCount > 0)
                    AddInfoMessage("局部调整", $"仍有 {bestConflictCount} 条教师冲突未能完全解决，可手动微调");
                Log($"局部调整: {changedCount} 节课变动, 剩余冲突 {bestConflictCount}");
                RefreshViews();
                return;
            }

            // 全部失败，恢复
            foreach (var (id, day, period, locked) in snapshot)
            {
                var entry = ScheduleEntries.FirstOrDefault(e => e.Id == id);
                if (entry != null) { entry.DayIndex = day; entry.PeriodIndex = period; entry.Locked = locked; }
            }
            CloseProgressDialog();
            StatusMessage = "局部调整无解，已恢复";
            AddInfoMessage("局部调整", "所有策略均无法解决冲突，已恢复原状");
            RefreshViews();
        }
        catch (OperationCanceledException)
        {
            StopSmoothProgress();
            CloseProgressDialog();
            StatusMessage = "局部调整已取消";
            RefreshViews();
        }
        catch (Exception ex)
        {
            StopSmoothProgress();
            CloseProgressDialog();
            StatusMessage = $"局部调整失败: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            _cts = null;
        }
    }

    /// <summary>应用局部调整结果到ScheduleEntries</summary>
    private void ApplyLocalAdjustResult(ScheduleResult result, int expectedEntryCount)
    {
        ScheduleEntries.Clear();
        foreach (var entry in result.Entries.OrderBy(x => x.DayIndex).ThenBy(x => x.PeriodIndex))
            ScheduleEntries.Add(entry);
        Conflicts.Clear();
        foreach (var conflict in result.Conflicts)
            Conflicts.Add(conflict);
        OnPropertyChanged(nameof(TotalScheduleEntries));
        OnPropertyChanged(nameof(TotalConflicts));
    }

    /// <summary>弹窗完成后点击确认关闭</summary>
    public void ConfirmProgressDialog()
    {
        CloseProgressDialog();
    }

    private void CancelOperation()
    {
        _cts?.Cancel();
        StatusMessage = "正在取消...";
    }

    private void NewProject()
    {
        _projectFilePath = "";
        _projectName = "";
        OnPropertyChanged(nameof(ProjectName));
        OnPropertyChanged(nameof(ProjectFilePath));
        OnPropertyChanged(nameof(ProjectDirectory));
        HasActiveProject = false;
        StatusMessage = "";
        ClearAllData();
        OnPropertyChanged(nameof(ProjectFileName));
    }

    private void ClearAllData()
    {
        GradeInputs.Clear();
        GradeConfigs.Clear();
        Classes.Clear();
        Teachers.Clear();
        Subjects.Clear();
        TeacherAssignments.Clear();
        Requirements.Clear();
        FixedLessons.Clear();
        ScheduleEntries.Clear();
        Conflicts.Clear();
        DaysPerWeek = 5;
        PeriodsPerDay = 7;
        MorningPeriods = 4;
        AfternoonPeriods = 3;
        IncludeEveningSelfStudy = false;
        EveningPeriods = 2;
        EveningStudyDays = new[] { true, true, true, true, true, false, false };
        SelectedSettingsTabIndex = 0;
        SelectedConfigPage = "基础设置";
        RefreshViews();

        // 及时回收内存
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }

    private async Task LoadSampleDataAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        _cts = new CancellationTokenSource();
        ProgressMessage = "正在加载示例数据...";
        ProgressValue = 0;

        try
        {
            IProgress<double> progress = new Progress<double>(v =>
            {
                ProgressValue = v;
                ProgressMessage = $"正在加载示例数据... {v * 100:F0}%";
            });
            // 跳过求解器，仅生成配置数据（秒级完成）
            SchoolData data = await Task.Run(() => SampleDataFactory.Create(progress, _cts.Token, skipSolve: true), _cts.Token);

            ProgressMessage = "正在应用课程模板...";
            ProgressValue = 0.7;

            // 使用内置初中标准配置替换示例数据中的课程
            var builtIn = GetBuiltInTemplate();
            data.Subjects.Clear();
            data.Subjects.AddRange(builtIn.Subjects);
            data.FixedLessons.Clear();
            data.FixedLessons.AddRange(builtIn.FixedLessons);

            // 重新生成教师配置和需求
            var service = new ScheduleService();
            service.GenerateAssignments(data.TeacherAssignments, data.Subjects, data.Classes);
            data.Requirements.Clear();
            data.Requirements.AddRange(service.BuildRequirementsFromAssignments(data.TeacherAssignments, data.Classes, data.Subjects));

            ProgressValue = 0.9;
            ProgressMessage = "正在初始化界面...";

            ApplySchoolData(data);
            SelectedMainPage = "配置";
            SelectedConfigPage = "基础设置";
            SelectedViewMode = "年级总表";
            SelectedGradeInput = GradeInputs.FirstOrDefault();
            CurrentSubjectGradeName = GradeInputs.FirstOrDefault()?.GradeName ?? "全部";
            SelectedClass = Classes.FirstOrDefault();
            SelectedTeacher = Teachers.FirstOrDefault();
            StatusMessage = "已载入示例数据";
            RefreshViews();
            ProgressValue = 1.0;
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "操作已取消";
        }
        finally
        {
            IsBusy = false;
            _cts = null;
            ProgressValue = 0;
            ProgressMessage = string.Empty;
        }
    }

    private void GenerateClasses()
    {
        Classes.Clear();
        Requirements.Clear();
        ScheduleEntries.Clear();
        Conflicts.Clear();
        Teachers.Clear();
        TeacherAssignments.Clear();

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
        if (TeacherAssignments.Count > 0)
        {
            GenerateRequirements();
        }
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

    private void AddFixedLesson()
    {
        FixedLessons.Add(new FixedLesson
        {
            ScopeValue = "全校",
            DayIndex = 1,
            PeriodIndex = 1,
            Subject = ""
        });
    }

    private void DeleteFixedLesson()
    {
        if (SelectedFixedLesson is not null)
        {
            FixedLessons.Remove(SelectedFixedLesson);
            SelectedFixedLesson = FixedLessons.FirstOrDefault();
        }
    }

    private void AddTeacherAssignment()
    {
        TeacherAssignments.Add(new TeacherAssignment
        {
            TeacherName = "新教师",
            Subject = "",
            GradeName = GradeInputs.FirstOrDefault()?.GradeName ?? "",
            WeeklyCount = 0
        });
    }

    private void DeleteTeacherAssignment()
    {
        if (SelectedTeacherAssignment is not null)
        {
            TeacherAssignments.Remove(SelectedTeacherAssignment);
            SelectedTeacherAssignment = TeacherAssignments.FirstOrDefault();
        }
    }

    private void GenerateTeacherTemplate()
    {
        string dir = EnsureExportFolder();
        string path = Path.Combine(dir, "教师导入模板.xlsx");
        _excelService.GenerateImportTemplate(path, GradeInputs.ToList());
        StatusMessage = $"已生成导入模板: {path}";
        Log($"生成教师导入模板: {path}");
    }

    private async Task ImportTeacherListAsync()
    {
        var picker = new Windows.Storage.Pickers.FileOpenPicker
        {
            FileTypeFilter = { ".xlsx" },
            ViewMode = Windows.Storage.Pickers.PickerViewMode.List
        };
        var hwnd = WindowHandle != 0 ? WindowHandle : WinRT.Interop.WindowNative.GetWindowHandle(App.CurrentWindow!);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSingleFileAsync();
        if (file is null) return;

        try
        {
            var imported = await Task.Run(() => _excelService.ImportTeacherAssignments(file.Path));
            if (imported.Count == 0)
            {
                StatusMessage = "导入失败：未找到有效的教师数据";
                return;
            }

            TeacherAssignments.Clear();
            foreach (var assignment in imported)
                TeacherAssignments.Add(assignment);

            StatusMessage = $"已导入 {imported.Count} 位教师配置";
            Log($"导入教师名单: {imported.Count} 位");
            OnPropertyChanged(nameof(TotalAssignments));
        }
        catch (Exception ex)
        {
            StatusMessage = $"导入失败: {ex.Message}";
        }
    }

    private async Task GenerateTeachersAsync()
    {
        var dialog = new Microsoft.UI.Xaml.Controls.ContentDialog
        {
            Title = "生成教师配置",
            PrimaryButtonText = "生成",
            CloseButtonText = "取消",
            DefaultButton = Microsoft.UI.Xaml.Controls.ContentDialogButton.Primary,
            XamlRoot = App.CurrentWindow!.Content.XamlRoot
        };

        var root = new Microsoft.UI.Xaml.Controls.StackPanel { Spacing = 6, Width = 780 };

        // 替换选项
        var replaceCb = new Microsoft.UI.Xaml.Controls.CheckBox { Content = "替换现有教师配置（取消则追加）", IsChecked = true };
        root.Children.Add(replaceCb);

        // 年级选择
        var gradeCheckBoxes = new List<(string GradeName, Microsoft.UI.Xaml.Controls.CheckBox CheckBox)>();
        var gradePanel = new Microsoft.UI.Xaml.Controls.StackPanel { Orientation = Microsoft.UI.Xaml.Controls.Orientation.Horizontal, Spacing = 12 };
        foreach (var grade in GradeInputs)
        {
            var cb = new Microsoft.UI.Xaml.Controls.CheckBox { Content = $"{grade.GradeName}（{grade.ClassCount}班）", IsChecked = true };
            gradeCheckBoxes.Add((grade.GradeName, cb));
            gradePanel.Children.Add(cb);
        }
        root.Children.Add(gradePanel);

        // 科目配置区域
        var allSubjectNames = Subjects.Select(s => s.Name).Distinct().OrderBy(n => n).ToList();

        var configControls = new Dictionary<(string Grade, string Subject), (Microsoft.UI.Xaml.Controls.ComboBox Mode, Microsoft.UI.Xaml.Controls.NumberBox Num)>();
        var gradeToggles = new Dictionary<string, Microsoft.UI.Xaml.Controls.ToggleSwitch>();

        var contentArea = new Microsoft.UI.Xaml.Controls.Grid { MinHeight = 320 };
        var tabPages = new Dictionary<string, Microsoft.UI.Xaml.UIElement>();

        // 构建科目网格（双列，带间隙）
        Microsoft.UI.Xaml.UIElement BuildSubjectGrid(string gradeKey, List<string> subjectList)
        {
            var grid = new Microsoft.UI.Xaml.Controls.Grid { Margin = new Microsoft.UI.Xaml.Thickness(0, 4, 0, 0) };
            // 7列: 科目名|模式|数值|间隙||科目名|模式|数值
            int[] colWidths = { 60, 100, 50, 30, 60, 100, 50 };
            for (int c = 0; c < 7; c++)
                grid.ColumnDefinitions.Add(new Microsoft.UI.Xaml.Controls.ColumnDefinition { Width = new Microsoft.UI.Xaml.GridLength(colWidths[c]) });

            int half = (subjectList.Count + 1) / 2;
            for (int r = 0; r < half; r++)
                grid.RowDefinitions.Add(new Microsoft.UI.Xaml.Controls.RowDefinition { Height = new Microsoft.UI.Xaml.GridLength(36) });

            for (int i = 0; i < subjectList.Count; i++)
            {
                string subj = subjectList[i];
                int colOffset = i < half ? 0 : 4;
                int rowIdx = i < half ? i : i - half;

                var nameLabel = new Microsoft.UI.Xaml.Controls.TextBlock { Text = subj, VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Center, FontSize = 13 };
                Microsoft.UI.Xaml.Controls.Grid.SetRow(nameLabel, rowIdx);
                Microsoft.UI.Xaml.Controls.Grid.SetColumn(nameLabel, colOffset);
                grid.Children.Add(nameLabel);

                var modeCombo = new Microsoft.UI.Xaml.Controls.ComboBox { Margin = new Microsoft.UI.Xaml.Thickness(4, 1, 4, 1), FontSize = 12, MinWidth = 90 };
                modeCombo.Items.Add("按班");
                modeCombo.Items.Add("按年级");
                modeCombo.Items.Add("全校");
                // 默认模式：全校 > 按年级 > 按班
                modeCombo.SelectedIndex = IsDefaultSchoolWide(subj) ? 2 : IsDefaultByGrade(subj) ? 1 : 0;
                Microsoft.UI.Xaml.Controls.Grid.SetRow(modeCombo, rowIdx);
                Microsoft.UI.Xaml.Controls.Grid.SetColumn(modeCombo, colOffset + 1);
                grid.Children.Add(modeCombo);

                int defaultVal = GetDefaultTeacherConfig(subj, modeCombo.SelectedIndex == 0);
                var numBox = new Microsoft.UI.Xaml.Controls.NumberBox
                {
                    Value = defaultVal, Minimum = 1, Maximum = 30, SmallChange = 1,
                    SpinButtonPlacementMode = Microsoft.UI.Xaml.Controls.NumberBoxSpinButtonPlacementMode.Hidden,
                    Margin = new Microsoft.UI.Xaml.Thickness(2, 1, 0, 1), FontSize = 12, MinWidth = 44
                };
                Microsoft.UI.Xaml.Controls.Grid.SetRow(numBox, rowIdx);
                Microsoft.UI.Xaml.Controls.Grid.SetColumn(numBox, colOffset + 2);
                grid.Children.Add(numBox);

                configControls[(gradeKey, subj)] = (modeCombo, numBox);
            }

            return new Microsoft.UI.Xaml.Controls.ScrollViewer { Content = grid, VerticalScrollBarVisibility = Microsoft.UI.Xaml.Controls.ScrollBarVisibility.Auto };
        }

        // 全局页（所有科目）
        tabPages["全局"] = BuildSubjectGrid("全局", allSubjectNames);

        // 年级页（带开关，继承全局配置）
        foreach (var grade in GradeInputs)
        {
            string gName = grade.GradeName;
            var pagePanel = new Microsoft.UI.Xaml.Controls.StackPanel { Spacing = 4 };
            var toggle = new Microsoft.UI.Xaml.Controls.ToggleSwitch
            {
                Header = "自定义配置",
                IsOn = false,
                Margin = new Microsoft.UI.Xaml.Thickness(0, 4, 0, 0)
            };
            gradeToggles[gName] = toggle;

            // 年级自定义：只显示该年级存在的课程，排除全校科目
            var gradeSubjectNames = Subjects
                .Where(s => (string.IsNullOrEmpty(s.GradeName) || s.GradeName == gName) && !IsDefaultSchoolWide(s.Name))
                .Select(s => s.Name).Distinct().OrderBy(n => n).ToList();
            var gradeGrid = BuildSubjectGrid(gName, gradeSubjectNames);
            gradeGrid.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
            toggle.Toggled += (_, _) =>
            {
                gradeGrid.Visibility = toggle.IsOn ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
            };

            var hint = new Microsoft.UI.Xaml.Controls.TextBlock
            {
                Text = "关闭时使用全局配置",
                FontSize = 11,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 120, 130, 140)),
                Margin = new Microsoft.UI.Xaml.Thickness(0, 2, 0, 0)
            };

            pagePanel.Children.Add(toggle);
            pagePanel.Children.Add(hint);
            pagePanel.Children.Add(gradeGrid);
            tabPages[gName] = pagePanel;
        }

        // 标签按钮栏
        var tabBar = new Microsoft.UI.Xaml.Controls.StackPanel { Orientation = Microsoft.UI.Xaml.Controls.Orientation.Horizontal, Spacing = 4 };
        var tabButtons = new List<(string Name, Microsoft.UI.Xaml.Controls.Button Btn)>();

        foreach (var tabName in tabPages.Keys)
        {
            var btn = new Microsoft.UI.Xaml.Controls.Button
            {
                Content = tabName,
                FontSize = 13,
                Padding = new Microsoft.UI.Xaml.Thickness(14, 5, 14, 5),
                Background = tabName == "全局"
                    ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 33, 78, 120))
                    : new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 240, 245, 255)),
                Foreground = tabName == "全局"
                    ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 255, 255))
                    : new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 33, 78, 120)),
                BorderThickness = new Microsoft.UI.Xaml.Thickness(0),
                CornerRadius = new Microsoft.UI.Xaml.CornerRadius(4)
            };
            string name = tabName;
            btn.Click += (_, _) =>
            {
                foreach (var (n, b) in tabButtons)
                {
                    bool active = n == name;
                    b.Background = active
                        ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 33, 78, 120))
                        : new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 240, 245, 255));
                    b.Foreground = active
                        ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 255, 255))
                        : new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 33, 78, 120));
                }
                contentArea.Children.Clear();
                contentArea.Children.Add(tabPages[name]);
            };
            tabButtons.Add((tabName, btn));
            tabBar.Children.Add(btn);
        }
        root.Children.Add(tabBar);

        // 初始显示全局页
        contentArea.Children.Add(tabPages["全局"]);
        root.Children.Add(contentArea);

        // 提示
        root.Children.Add(new Microsoft.UI.Xaml.Controls.TextBlock
        {
            Text = "“按班”：数值=每位教师所带班级数；“按年级”：数值=该年级该科目教师数；“全校”：数值=全校该科目教师总数。年级自定义时继承全局配置。",
            FontSize = 11,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 90, 104, 119)),
            TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap
        });

        dialog.Content = root;
        var result = await dialog.ShowAsync();
        if (result != Microsoft.UI.Xaml.Controls.ContentDialogResult.Primary) return;

        // 收集配置
        var selectedGrades = gradeCheckBoxes.Where(g => g.CheckBox.IsChecked == true).Select(g => g.GradeName).ToHashSet();
        if (selectedGrades.Count == 0) { StatusMessage = "未选择任何年级"; return; }

        var configMap = new Dictionary<string, (int Value, int Mode)>();

        // 年级科目配置：开关打开用年级配置，否则用全局
        foreach (var gradeName in selectedGrades)
        {
            bool customOn = gradeToggles.TryGetValue(gradeName, out var tg) && tg.IsOn;
            foreach (var subj in allSubjectNames)
            {
                if (customOn && configControls.TryGetValue((gradeName, subj), out var gradeCtrl))
                    configMap[$"{gradeName}|{subj}"] = ((int)gradeCtrl.Num.Value, gradeCtrl.Mode.SelectedIndex);
                else if (configControls.TryGetValue(("全局", subj), out var globalCtrl))
                    configMap[$"{gradeName}|{subj}"] = ((int)globalCtrl.Num.Value, globalCtrl.Mode.SelectedIndex);
            }
        }

        // 生成教师
        var selectedClasses = Classes.Where(c => selectedGrades.Contains(c.GradeName)).ToList();
        var selectedSubjects = Subjects.Where(s => string.IsNullOrEmpty(s.GradeName) || selectedGrades.Contains(s.GradeName)).ToList();

        var newAssignments = new List<TeacherAssignment>();
        GenerateTeachersWithConfigV2(newAssignments, selectedSubjects, selectedClasses, configMap);

        if (replaceCb.IsChecked == true)
            TeacherAssignments.Clear();

        foreach (var a in newAssignments)
            TeacherAssignments.Add(a);

        StatusMessage = $"已生成 {newAssignments.Count} 位教师配置";
        Log($"生成教师: {newAssignments.Count} 位");
        OnPropertyChanged(nameof(TotalAssignments));
    }

    internal static bool IsDefaultByGrade(string subject)
    {
        // 默认“按年级”的科目
        return subject is "物理" or "化学" or "地理" or "生物" or "历史" or "道德" or "音乐" or "美术" or "信息" or "劳动";
    }

    /// <summary>是否为默认“全校”模式的科目</summary>
    internal static bool IsDefaultSchoolWide(string subject)
    {
        return subject is "体育";
    }

    internal static int GetDefaultTeacherConfig(string subject, bool isClassesPerTeacherMode)
    {
        if (isClassesPerTeacherMode)
        {
            return subject switch
            {
                "语文" or "数学" or "英语" => 2,
                "物理" or "化学" => 3,
                "地理" or "生物" or "历史" or "道德" => 4,
                "音乐" or "美术" or "信息" or "劳动" => 8,
                "体育" => 4,
                _ => 3
            };
        }
        else
        {
            return subject switch
            {
                "语文" or "数学" or "英语" => 4,
                "物理" or "化学" => 3,
                "地理" or "生物" or "历史" or "道德" => 2,
                "音乐" or "美术" or "信息" or "劳动" => 1,
                "体育" => 6,
                _ => 2
            };
        }
    }

    private void GenerateTeachersWithConfig(ICollection<TeacherAssignment> assignments, IEnumerable<SubjectDefinition> subjects,
        IEnumerable<SchoolClass> classes, Dictionary<string, (int Value, bool Custom)> configMap, bool isClassesPerTeacherMode)
    {
        var classList = classes.ToList();
        if (classList.Count == 0) return;

        foreach (var gradeGroup in classList.GroupBy(c => c.GradeName))
        {
            string gradeName = gradeGroup.Key;
            string shortGrade = gradeName.Replace("年级", "");
            var gradeClasses = gradeGroup.ToList();
            int classCount = gradeClasses.Count;

            foreach (var subDef in subjects.Where(s => string.IsNullOrEmpty(s.GradeName) || s.GradeName == gradeName))
            {
                if (!configMap.TryGetValue(subDef.Name, out var cfg)) continue;

                int numTeachers;
                if (isClassesPerTeacherMode)
                {
                    int classesPerTeacher = Math.Max(1, cfg.Value);
                    numTeachers = (int)Math.Ceiling((double)classCount / classesPerTeacher);
                }
                else
                {
                    numTeachers = Math.Max(1, cfg.Value);
                }

                int perTeacher = (int)Math.Ceiling((double)classCount / numTeachers);
                for (int t = 0; t < numTeachers; t++)
                {
                    var assignedClasses = gradeClasses.Skip(t * perTeacher).Take(perTeacher).ToList();
                    if (assignedClasses.Count == 0) break;
                    assignments.Add(new TeacherAssignment
                    {
                        TeacherName = $"{shortGrade}{subDef.Name[..1]}{ToChineseNumeral(t + 1)}",
                        Subject = subDef.Name,
                        ClassNames = string.Join("、", assignedClasses.Select(c => c.Name)),
                        GradeName = gradeName
                    });
                }
            }
        }
    }

    /// <summary>V2: 每个年级+科目独立配置，模式: 0=按班, 1=按年级, 2=全校</summary>
    internal static void GenerateTeachersWithConfigV2(ICollection<TeacherAssignment> assignments, IEnumerable<SubjectDefinition> subjects,
        IEnumerable<SchoolClass> classes, Dictionary<string, (int Value, int Mode)> configMap)
    {
        var classList = classes.ToList();
        if (classList.Count == 0) return;

        // 全校模式科目：跨年级统一分配
        var schoolWideKeys = configMap.Where(kv => kv.Value.Mode == 2).ToList();
        var handledSubjects = new HashSet<string>();
        foreach (var (key, cfg) in schoolWideKeys)
        {
            string subj = key.Split('|')[1];
            if (handledSubjects.Contains(subj)) continue;
            handledSubjects.Add(subj);

            int totalClasses = classList.Count;
            int numTeachers = Math.Max(1, cfg.Value);
            int perTeacher = (int)Math.Ceiling((double)totalClasses / numTeachers);
            for (int t = 0; t < numTeachers; t++)
            {
                var assignedClasses = classList.Skip(t * perTeacher).Take(perTeacher).ToList();
                if (assignedClasses.Count == 0) break;
                assignments.Add(new TeacherAssignment
                {
                    TeacherName = $"{subj[..1]}{ToChineseNumeral(t + 1)}",
                    Subject = subj,
                    ClassNames = string.Join("、", assignedClasses.Select(c => c.Name)),
                    GradeName = "全校"
                });
            }
        }

        // 按年级/按班模式
        foreach (var gradeGroup in classList.GroupBy(c => c.GradeName))
        {
            string gradeName = gradeGroup.Key;
            string shortGrade = gradeName.Replace("年级", "");
            var gradeClasses = gradeGroup.ToList();
            int classCount = gradeClasses.Count;

            foreach (var subDef in subjects.Where(s => string.IsNullOrEmpty(s.GradeName) || s.GradeName == gradeName))
            {
                if (handledSubjects.Contains(subDef.Name)) continue; // 全校模式已处理

                string key = $"{gradeName}|{subDef.Name}";
                if (!configMap.TryGetValue(key, out var cfg)) continue;
                if (cfg.Mode == 2) continue; // 全校模式已在上面处理

                int numTeachers;
                if (cfg.Mode == 1) // 按年级
                    numTeachers = Math.Max(1, cfg.Value);
                else // 按班
                    numTeachers = (int)Math.Ceiling((double)classCount / Math.Max(1, cfg.Value));

                int perTeacher = (int)Math.Ceiling((double)classCount / numTeachers);
                for (int t = 0; t < numTeachers; t++)
                {
                    var assignedClasses = gradeClasses.Skip(t * perTeacher).Take(perTeacher).ToList();
                    if (assignedClasses.Count == 0) break;
                    assignments.Add(new TeacherAssignment
                    {
                        TeacherName = $"{shortGrade}{subDef.Name[..1]}{ToChineseNumeral(t + 1)}",
                        Subject = subDef.Name,
                        ClassNames = string.Join("、", assignedClasses.Select(c => c.Name)),
                        GradeName = gradeName
                    });
                }
            }
        }
    }

    /// <summary>数字转中文序号（一、二、三...十、十一...）</summary>
    internal static string ToChineseNumeral(int n)
    {
        string[] digits = { "", "一", "二", "三", "四", "五", "六", "七", "八", "九" };
        if (n <= 0) return n.ToString();
        if (n < 10) return digits[n];
        if (n == 10) return "十";
        if (n < 20) return $"十{digits[n % 10]}";
        if (n < 100) return $"{digits[n / 10]}十{(n % 10 == 0 ? "" : digits[n % 10])}";
        return n.ToString();
    }

    private async Task AutoScheduleAsync()
    {
        if (IsBusy) return;
        IsBusy = true;

        try
        {
            _cts = new CancellationTokenSource();
            OpenProgressDialog("自动排课");

            // 阶段1: 生成需求 (0-5%)
            UpdateDialogProgress(0.02, "正在生成需求...");
            GenerateRequirements();
            UpdateDialogProgress(0.05, "正在生成需求... 5%");

            // 阶段2: 构建数据 (5-15%)
            UpdateDialogProgress(0.10, "正在构建模型...");
            SchoolData data = BuildSchoolData();
            UpdateDialogProgress(0.15, "正在构建模型... 15%");

            // 阶段3: 求解 (15-95%) — 启动平滑进度
            StartSmoothProgress(0.15, 0.95);
            IProgress<double> progress = new Progress<double>(v =>
            {
                // 求解器报告 0-1 映射到 UI 15-95%
                double mapped = 0.15 + v * 0.80;
                StopSmoothProgress();
                UpdateDialogProgress(mapped);
                StartSmoothProgress(mapped, 0.95);
            });

            ScheduleResult result = await Task.Run(
                () => _scheduleService.Generate(data, progress, _cts.Token), _cts.Token);

            StopSmoothProgress();

            // 阶段4: 输出结果 (95-99%)
            UpdateDialogProgress(0.95, "正在整理结果...");

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

            UpdateDialogProgress(0.99, "即将完成...");
            await Task.Delay(800); // 99%短暂停留
            UpdateDialogProgress(1.0, "排课完成！");
            DialogIsComplete = true;

            StatusMessage = $"排课完成：{ScheduleEntries.Count} 节课，{Conflicts.Count} 条提示";
            Log($"自动排课：{ScheduleEntries.Count} 节课");
            RefreshViews();
        }
        catch (OperationCanceledException)
        {
            StopSmoothProgress();
            CloseProgressDialog();
            StatusMessage = "排课已取消";
        }
        finally
        {
            IsBusy = false;
            _cts = null;
        }
    }

    private void ValidateSchedule()
    {
        Conflicts.Clear();
        foreach (ScheduleConflict conflict in _scheduleService.Validate(BuildSchoolData()))
        {
            Conflicts.Add(conflict);
        }

        OnPropertyChanged(nameof(TotalConflicts));
        StatusMessage = Conflicts.Count > 0 ? $"检查完成：{Conflicts.Count} 条信息" : "检查完成，无冲突";
        Log($"检查信息：{Conflicts.Count} 条");
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

    private async Task ExportExcelAsync()
    {
        try
        {
            IsBusy = true;
            StatusMessage = "正在导出...";
            string exportPath = ExportFolderPath;
            await Task.Run(() => _excelService.ExportAll(BuildSchoolData(), exportPath));
            StatusMessage = $"已导出到 {exportPath}";
            Log($"导出 Excel: {exportPath}");
            RequestShowMessage?.Invoke("导出成功", $"课表已导出到：\n{exportPath}");
        }
        catch (Exception ex)
        {
            StatusMessage = $"导出失败: {ex.Message}";
            RequestShowMessage?.Invoke("导出失败", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>选择导出位置</summary>
    public async Task SelectExportFolderAsync()
    {
        var folderPicker = new Windows.Storage.Pickers.FolderPicker
        {
            SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary
        };
        folderPicker.FileTypeFilter.Add("*");

        var hwnd = WindowHandle != 0 ? WindowHandle : WinRT.Interop.WindowNative.GetWindowHandle(App.CurrentWindow!);
        WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);

        var folder = await folderPicker.PickSingleFolderAsync();
        if (folder != null)
        {
            ExportFolderPath = folder.Path;
            StatusMessage = $"导出位置已更改为: {folder.Path}";
        }
    }

    private async Task ImportExcelAsync()
    {
        if (IsBusy) return;

        var picker = new Windows.Storage.Pickers.FileOpenPicker
        {
            FileTypeFilter = { ".xlsx" },
            ViewMode = Windows.Storage.Pickers.PickerViewMode.List
        };

        var hwnd = WindowHandle != 0 ? WindowHandle : WinRT.Interop.WindowNative.GetWindowHandle(App.CurrentWindow!);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSingleFileAsync();
        if (file is null) return;

        IsBusy = true;
        ProgressMessage = "正在导入 Excel...";

        try
        {
            SchoolData data = await Task.Run(() => _excelService.Import(file.Path));
            ApplySchoolData(data);
            RefreshViews();
            StatusMessage = "已导入 Excel 数据";
            Log("导入 Excel");
        }
        finally
        {
            IsBusy = false;
            ProgressValue = 0;
            ProgressMessage = string.Empty;
        }
    }

    private void SetMainPage(string? page)
    {
        if (string.IsNullOrWhiteSpace(page)) return;
        SelectedMainPage = page;
    }

    private void SetConfigPage(string? page)
    {
        if (string.IsNullOrWhiteSpace(page)) return;
        SelectedConfigPage = page;
    }

    private void SetSubjectGrade(string? gradeName)
    {
        if (string.IsNullOrWhiteSpace(gradeName)) return;
        CurrentSubjectGradeName = gradeName;
    }

    private void SetViewMode(string? viewMode)
    {
        if (string.IsNullOrWhiteSpace(viewMode)) return;
        SelectedViewMode = viewMode;
        if (SelectedViewMode == "年级总表" && SelectedGradeInput is null)
            SelectedGradeInput = GradeInputs.FirstOrDefault();
        else if (SelectedViewMode == "班级课表" && SelectedClass is null)
            SelectedClass = Classes.FirstOrDefault();
        else if (SelectedViewMode == "教师课表" && SelectedTeacher is null)
            SelectedTeacher = Teachers.FirstOrDefault();
        RefreshViews();
    }

    private void SelectDay(int dayIndex)
    {
        if (dayIndex < 0 || dayIndex >= DaysPerWeek) return;
        SelectedDayIndex = dayIndex;
        RefreshViews();
    }

    private void SelectGrade(GradeInput? grade)
    {
        if (grade is null) return;
        SelectedViewMode = "年级总表";
        SelectedGradeInput = grade;
    }

    private void SelectClass(SchoolClass? schoolClass)
    {
        if (schoolClass is null) return;
        SelectedViewMode = "班级课表";
        SelectedClass = schoolClass;
    }

    private void SelectTeacher(Teacher? teacher)
    {
        if (teacher is null) return;
        SelectedViewMode = "教师课表";
        SelectedTeacher = teacher;
    }

    private void SetDaysPerWeek(int days)
    {
        DaysPerWeek = days;
        StatusMessage = days == 5 ? "已切换到五天制" : "已切换到七天制";
        Log(StatusMessage);
    }

    private void ToggleEveningDay(int dayIndex)
    {
        if (dayIndex < 0 || dayIndex >= 7) return;
        var days = (bool[])_eveningStudyDays.Clone();
        days[dayIndex] = !days[dayIndex];
        EveningStudyDays = days;
        SyncEveningDayItems();
    }

    private void ToggleGradeEveningDay(string? param)
    {
        // param format: "dayIndex" — toggles on the currently selected grade config
        if (!int.TryParse(param, out int dayIndex) || dayIndex < 0 || dayIndex >= 7) return;
        var config = SelectedGradeConfig;
        if (config == null) return;
        var days = (bool[])config.EveningStudyDays.Clone();
        days[dayIndex] = !days[dayIndex];
        config.EveningStudyDays = days;
        SyncGradeEveningDayItems();
    }

    private void SelectSettingsTab(int tabIndex)
    {
        SelectedSettingsTabIndex = tabIndex;
        SyncGradeEveningDayItems();
    }

    private void InitDefaultGradeConfigs()
    {
        GradeConfigs.Clear();
        string[] gradeNames = { "七年级", "八年级", "九年级" };
        foreach (var name in gradeNames)
        {
            GradeConfigs.Add(new GradeScheduleConfig { GradeName = name });
        }
    }

    private void InitEveningDayItems()
    {
        EveningDayItems.Clear();
        string[] labels = { "周一", "周二", "周三", "周四", "周五", "周六", "周日" };
        for (int i = 0; i < 7; i++)
        {
            EveningDayItems.Add(new DayToggleItem { Label = labels[i], Index = i, IsSelected = _eveningStudyDays[i] });
        }
    }

    private void SyncEveningDayItems()
    {
        for (int i = 0; i < 7 && i < EveningDayItems.Count; i++)
            EveningDayItems[i].IsSelected = _eveningStudyDays[i];
    }

    private void SyncGradeEveningDayItems()
    {
        var config = SelectedGradeConfig;
        GradeEveningDayItems.Clear();
        string[] labels = { "周一", "周二", "周三", "周四", "周五", "周六", "周日" };
        var days = config?.EveningStudyDays ?? new bool[7];
        for (int i = 0; i < 7; i++)
        {
            GradeEveningDayItems.Add(new DayToggleItem { Label = labels[i], Index = i, IsSelected = i < days.Length && days[i] });
        }
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
        OnPropertyChanged(nameof(FilteredGrades));
        OnPropertyChanged(nameof(FilteredTeachers));
        OnPropertyChanged(nameof(FilteredClasses));
    }

    private void RefreshVisibleEntries()
    {
        VisibleScheduleEntries.Clear();

        IEnumerable<ScheduleEntry> entries = ScheduleEntries;
        if (SelectedViewMode == "班级课表" && SelectedClass is not null)
            entries = entries.Where(x => x.ClassId == SelectedClass.Id);
        else if (SelectedViewMode == "教师课表" && SelectedTeacher is not null)
            entries = entries.Where(x => x.TeacherName == SelectedTeacher.Name);
        else if (SelectedViewMode == "年级总表" && SelectedGradeInput is not null)
        {
            string shortGrade = SelectedGradeInput.GradeName.Replace("年级", "");
            entries = entries.Where(x => x.ClassName.StartsWith(shortGrade, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(GradeFilterText) && SelectedViewMode != "年级总表")
            entries = entries.Where(x => x.ClassName.StartsWith(GradeFilterText, StringComparison.OrdinalIgnoreCase));

        foreach (ScheduleEntry entry in entries.OrderBy(x => x.DayIndex).ThenBy(x => x.PeriodIndex).ThenBy(x => x.ClassName))
        {
            VisibleScheduleEntries.Add(entry);
        }
    }

    private void RefreshTimetable()
    {
        int daysPerWeek = Math.Max(1, DaysPerWeek);
        int periodsPerDay = Math.Max(1, PeriodsPerDay);

        ObservableCollection<ScheduleDayViewModel> days = new();
        for (int day = 0; day < daysPerWeek; day++)
        {
            ScheduleDayViewModel dayView = new()
            {
                DayIndex = day,
                DayName = GetDayName(day)
            };

            for (int period = 1; period <= periodsPerDay; period++)
            {
                string periodType = period <= MorningPeriods ? "上午" :
                    period <= MorningPeriods + AfternoonPeriods ? "下午" : "晚自习";

                ScheduleCellViewModel periodView = new()
                {
                    DayIndex = day,
                    PeriodIndex = period,
                    PeriodType = periodType
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

        ObservableCollection<SchedulePeriodRowViewModel> rows = new();
        for (int period = 1; period <= periodsPerDay; period++)
        {
            string periodType = period <= MorningPeriods ? "上午" :
                period <= MorningPeriods + AfternoonPeriods ? "下午" : "晚自习";

            SchedulePeriodRowViewModel row = new()
            {
                PeriodIndex = period,
                PeriodLabel = $"第{period}节",
                PeriodType = periodType
            };

            for (int day = 0; day < daysPerWeek; day++)
            {
                SchedulePeriodDayColumn col = new()
                {
                    DayIndex = day,
                    DayName = GetDayName(day)
                };

                foreach (ScheduleEntry entry in VisibleScheduleEntries
                    .Where(x => x.DayIndex == day && x.PeriodIndex == period)
                    .OrderBy(x => x.ClassName)
                    .ThenBy(x => x.Subject))
                {
                    col.Entries.Add(entry);
                }

                row.DayColumns.Add(col);
            }

            rows.Add(row);
        }

        TimetableRows = rows;
        OnPropertyChanged(nameof(TimetableRows));

        BuildScheduleMatrix();
    }

    private void BuildScheduleMatrix()
    {
        int periodsPerDay = Math.Max(1, PeriodsPerDay);
        int daysPerWeek = Math.Max(1, DaysPerWeek);

        // Build day tabs
        var dayTabs = new ObservableCollection<DayTabItem>();
        for (int i = 0; i < daysPerWeek; i++)
        {
            dayTabs.Add(new DayTabItem { Index = i, Name = GetDayName(i) });
        }
        DayTabs = dayTabs;
        OnPropertyChanged(nameof(DayTabs));

        // Determine classes for the selected grade
        var classes = new List<SchoolClass>();
        if (SelectedGradeInput is not null)
        {
            string gradeName = SelectedGradeInput.GradeName;
            classes = Classes.Where(c => c.GradeName == gradeName)
                .OrderBy(c => c.ClassNumber)
                .ToList();
        }
        AvailableClasses = new ObservableCollection<SchoolClass>(classes);
        OnPropertyChanged(nameof(AvailableClasses));

        if (SelectedViewMode == "年级总表")
        {
            BuildGradeMatrix(classes, periodsPerDay, daysPerWeek);
        }
        else if (SelectedViewMode == "班级课表" && SelectedClass is not null)
        {
            var rows = new ObservableCollection<ScheduleGridRow>();
            BuildSingleClassMatrix(rows, SelectedClass, periodsPerDay, daysPerWeek);
            MatrixRows = rows;
            OnPropertyChanged(nameof(MatrixRows));
        }
        else if (SelectedViewMode == "教师课表" && SelectedTeacher is not null)
        {
            var rows = new ObservableCollection<ScheduleGridRow>();
            BuildTeacherMatrix(rows, periodsPerDay, daysPerWeek);
            MatrixRows = rows;
            OnPropertyChanged(nameof(MatrixRows));
        }

        OnPropertyChanged(nameof(DayNames));
    }

    private void BuildGradeMatrix(List<SchoolClass> classes, int periodsPerDay, int daysPerWeek)
    {
        // 检测教师时间槽冲突：同教师同天同节不同班 → 标红
        var conflictEntryIds = new HashSet<Guid>();
        var teacherSlots = VisibleScheduleEntries
            .Where(e => e.TeacherId != Guid.Empty && !e.IsFixed)
            .GroupBy(e => (e.TeacherName, e.DayIndex, e.PeriodIndex))
            .Where(g => g.Select(e => e.ClassId).Distinct().Count() > 1);
        foreach (var g in teacherSlots)
            foreach (var e in g)
                conflictEntryIds.Add(e.Id);

        // 构建双层表头
        var dayHeaders = new ObservableCollection<GradeDayHeader>();
        for (int day = 0; day < daysPerWeek; day++)
        {
            var header = new GradeDayHeader { DayName = GetDayName(day), DayIndex = day };
            for (int p = 1; p <= periodsPerDay; p++)
                header.PeriodNumbers.Add(p);
            dayHeaders.Add(header);
        }
        GradeDayHeaders = dayHeaders;
        OnPropertyChanged(nameof(GradeDayHeaders));

        // 构建班级行，每行 Cells 按 day×period 平铺
        // 预计算每个班级每个科目的实际数量 vs 配置数量
        var expectedCounts = Requirements
            .GroupBy(r => (r.ClassId, r.Subject))
            .ToDictionary(g => g.Key, g => g.Sum(r => r.WeeklyCount));
        var actualCounts = VisibleScheduleEntries
            .Where(e => !e.IsFixed)
            .GroupBy(e => (e.ClassId, e.Subject))
            .ToDictionary(g => g.Key, g => g.Count());

        var classRows = new ObservableCollection<GradeClassRow>();
        foreach (var cls in classes)
        {
            // 检查该班级每个科目数量是否符合配置
            bool hasError = expectedCounts
                .Where(kv => kv.Key.ClassId == cls.Id)
                .Any(kv => !actualCounts.TryGetValue(kv.Key, out int actual) || actual != kv.Value);
            // 也检查多出来的科目（实际有但配置没有）
            if (!hasError)
                hasError = actualCounts.Keys.Any(k => k.ClassId == cls.Id && !expectedCounts.ContainsKey(k));

            var row = new GradeClassRow { ClassName = cls.DisplayName, ClassId = cls.Id, HasCountError = hasError };
            for (int day = 0; day < daysPerWeek; day++)
            {
                for (int period = 1; period <= periodsPerDay; period++)
                {
                    var entry = VisibleScheduleEntries
                        .FirstOrDefault(e => e.DayIndex == day && e.PeriodIndex == period && e.ClassId == cls.Id);
                    row.Cells.Add(new ScheduleGridCell
                    {
                        DayIndex = day,
                        PeriodIndex = period,
                        Subject = entry?.Subject ?? "",
                        TeacherName = entry?.TeacherName ?? "",
                        ClassName = cls.DisplayName,
                        EntryId = entry?.Id ?? Guid.Empty,
                        Entry = entry,
                        HasConflict = entry != null && conflictEntryIds.Contains(entry.Id),
                    });
                }
            }
            classRows.Add(row);
        }
        GradeClassRows = classRows;
        OnPropertyChanged(nameof(GradeClassRows));
    }

    private void BuildSingleClassMatrix(ObservableCollection<ScheduleGridRow> rows, SchoolClass cls, int periodsPerDay, int daysPerWeek)
    {
        for (int period = 1; period <= periodsPerDay; period++)
        {
            var row = new ScheduleGridRow
            {
                PeriodLabel = $"第{period}节",
                PeriodIndex = period,
                ClassName = cls.DisplayName,
                ClassId = cls.Id,
            };

            for (int day = 0; day < daysPerWeek; day++)
            {
                var entry = VisibleScheduleEntries
                    .FirstOrDefault(e => e.DayIndex == day && e.PeriodIndex == period && e.ClassId == cls.Id);

                row.Cells.Add(new ScheduleGridCell
                {
                    DayIndex = day,
                    Subject = entry?.Subject ?? "",
                    TeacherName = entry?.TeacherName ?? "",
                    ClassName = cls.DisplayName,
                    EntryId = entry?.Id ?? Guid.Empty,
                    Entry = entry,
                });
            }

            rows.Add(row);
        }
    }

    private void BuildTeacherMatrix(ObservableCollection<ScheduleGridRow> rows, int periodsPerDay, int daysPerWeek)
    {
        for (int period = 1; period <= periodsPerDay; period++)
        {
            var row = new ScheduleGridRow
            {
                PeriodLabel = $"第{period}节",
                PeriodIndex = period,
            };

            for (int day = 0; day < daysPerWeek; day++)
            {
                // 取同时段所有条目（体育连班可能有多个）
                var entries = VisibleScheduleEntries
                    .Where(e => e.DayIndex == day && e.PeriodIndex == period)
                    .OrderBy(e => e.ClassName)
                    .ToList();
                var entry = entries.FirstOrDefault();

                row.Cells.Add(new ScheduleGridCell
                {
                    DayIndex = day,
                    Subject = entry?.Subject ?? "",
                    TeacherName = entry?.TeacherName ?? "",
                    ClassName = entry?.ClassName ?? "",
                    EntryId = entry?.Id ?? Guid.Empty,
                    Entry = entry,
                    AllEntries = entries,
                });
            }

            rows.Add(row);
        }
    }

    private void ApplySchoolData(SchoolData data)
    {
        DaysPerWeek = data.Settings.DaysPerWeek;
        PeriodsPerDay = data.Settings.PeriodsPerDay;
        MorningPeriods = data.Settings.MorningPeriods;
        AfternoonPeriods = data.Settings.AfternoonPeriods;
        IncludeEveningSelfStudy = data.Settings.IncludeEveningSelfStudy;
        EveningPeriods = data.Settings.EveningPeriods;
        EveningStudyDays = data.Settings.EveningStudyDays ?? new[] { true, true, true, true, true, false, false };
        SyncEveningDayItems();

        ReplaceCollection(GradeInputs, data.GradeInputs);
        ReplaceCollection(Classes, data.Classes);
        ReplaceCollection(Teachers, data.Teachers);
        ReplaceCollection(Subjects, data.Subjects);
        ReplaceCollection(TeacherAssignments, data.TeacherAssignments);
        ReplaceCollection(Requirements, data.Requirements);
        ReplaceCollection(FixedLessons, data.FixedLessons);
        ReplaceCollection(ScheduleEntries, data.ScheduleEntries);

        // 加载年级个性化配置
        if (data.GradeConfigs.Count > 0)
            ReplaceCollection(GradeConfigs, data.GradeConfigs);
        else
            InitDefaultGradeConfigs();

        SelectedGradeInput = GradeInputs.FirstOrDefault();
        SelectedClass = Classes.FirstOrDefault();
        SelectedTeacher = Teachers.FirstOrDefault();
        SelectedSubject = Subjects.FirstOrDefault();
        SelectedRequirement = Requirements.FirstOrDefault();
        SelectedFixedLesson = FixedLessons.FirstOrDefault();
        SelectedTeacherAssignment = TeacherAssignments.FirstOrDefault();
        SelectedSettingsTabIndex = 0;
    }

    private SchoolData BuildSchoolData()
    {
        return new SchoolData
        {
            ProjectName = _projectName,
            Settings = new ScheduleSettings
            {
                DaysPerWeek = DaysPerWeek,
                PeriodsPerDay = PeriodsPerDay,
                MorningPeriods = MorningPeriods,
                AfternoonPeriods = AfternoonPeriods,
                IncludeEveningSelfStudy = IncludeEveningSelfStudy,
                EveningPeriods = EveningPeriods,
                EveningStudyDays = EveningStudyDays
            },
            GradeConfigs = GradeConfigs.ToList(),
            GradeInputs = GradeInputs.ToList(),
            Classes = Classes.ToList(),
            Teachers = Teachers.ToList(),
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
        string dir = string.IsNullOrEmpty(_projectName)
            ? AppPaths.OutputPath
            : AppPaths.GetProjectOutputDir(_projectName);
        Directory.CreateDirectory(dir);
        return dir;
    }

    private void Log(string message)
    {
        ActivityLog.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {message}");
        while (ActivityLog.Count > 50)
        {
            ActivityLog.RemoveAt(ActivityLog.Count - 1);
        }
    }

    /// <summary>添加信息提示到信息面板</summary>
    private void AddInfoMessage(string typeText, string message)
    {
        Conflicts.Add(new ScheduleConflict
        {
            Severity = ScheduleConflictSeverity.Info,
            Type = ScheduleConflictType.PreferenceConflict,
            Message = message,
            Scope = typeText,
            Target = ""
        });
        OnPropertyChanged(nameof(TotalConflicts));
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
