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
    private string _schoolName = "中学";
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

    public MainViewModel()
    {
        _scheduleService = new ScheduleService(new GreedyScheduleSolver(), new ConflictService());
        _store = new SchoolDataStore();
        _excelService = new ExcelScheduleService();

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
        TimetableDays = new ObservableCollection<ScheduleDayViewModel>();
        TimetableRows = new ObservableCollection<SchedulePeriodRowViewModel>();

        SeedSampleDataCommand = new RelayCommand(() => _ = LoadSampleDataAsync());
        GenerateClassesCommand = new RelayCommand(GenerateClasses);
        GenerateRequirementsCommand = new RelayCommand(GenerateRequirements);
        GenerateAssignmentsCommand = new RelayCommand(GenerateAssignments);
        AutoScheduleCommand = new RelayCommand(() => _ = AutoScheduleAsync());
        ValidateCommand = new RelayCommand(ValidateSchedule);
        SaveCommand = new RelayCommand(SaveData);
        LoadCommand = new RelayCommand(LoadData);
        NewProjectCommand = new RelayCommand(NewProject);
        ExportCommand = new RelayCommand(ExportExcel);
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
        SearchTeacherCommand = new RelayCommand(RefreshViews);
        FilterGradeCommand = new RelayCommand(RefreshViews);
        AddSubjectCommand = new RelayCommand(AddSubject);
        DeleteSubjectCommand = new RelayCommand(DeleteSubject, () => SelectedSubject is not null);

        LoadData();
        if (GradeInputs.Count == 0)
        {
            InitDefaultGrades();
            InitDefaultSubjects();
        }

        if (Classes.Count == 0 && GradeInputs.Count > 0)
        {
            GenerateClasses();
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
        string[] allGrades = GradeInputs.Select(g => g.GradeName).ToArray();
        foreach (string grade in allGrades)
        {
            Subjects.Add(new SubjectDefinition { Name = "语文", Category = "主科", DefaultWeeklyCount = 6, DistributionRule = "每天一次", GradeName = grade });
            Subjects.Add(new SubjectDefinition { Name = "数学", Category = "主科", DefaultWeeklyCount = 6, DistributionRule = "每天一次", GradeName = grade });
            Subjects.Add(new SubjectDefinition { Name = "英语", Category = "主科", DefaultWeeklyCount = 5, DistributionRule = "每天一次", GradeName = grade });
            if (grade != "七年级")
            {
                Subjects.Add(new SubjectDefinition { Name = "物理", Category = "理科", DefaultWeeklyCount = 3, DistributionRule = "均衡分布", GradeName = grade });
            }
            if (grade != "七年级" && grade != "八年级")
            {
                Subjects.Add(new SubjectDefinition { Name = "化学", Category = "理科", DefaultWeeklyCount = 3, DistributionRule = "均衡分布", GradeName = grade });
            }
            Subjects.Add(new SubjectDefinition { Name = "生物", Category = "理科", DefaultWeeklyCount = 2, DistributionRule = "均衡分布", GradeName = grade });
            Subjects.Add(new SubjectDefinition { Name = "历史", Category = "文科", DefaultWeeklyCount = 2, DistributionRule = "均衡分布", GradeName = grade });
            Subjects.Add(new SubjectDefinition { Name = "地理", Category = "文科", DefaultWeeklyCount = 2, DistributionRule = "均衡分布", GradeName = grade });
            Subjects.Add(new SubjectDefinition { Name = "政治", Category = "文科", DefaultWeeklyCount = 2, DistributionRule = "均衡分布", GradeName = grade });
            Subjects.Add(new SubjectDefinition { Name = "体育", Category = "副科", DefaultWeeklyCount = 2, DistributionRule = "均衡分布", GradeName = grade });
        }
    }

    private void AddSubject()
    {
        string? firstGrade = GradeInputs.FirstOrDefault()?.GradeName;
        var subj = new SubjectDefinition
        {
            Name = "新科目",
            Category = "副科",
            DefaultWeeklyCount = 2,
            GradeName = firstGrade ?? string.Empty
        };
        subj.DistributionRule = GetDefaultDistributionRule(subj.Category, subj.DefaultWeeklyCount);
        Subjects.Add(subj);
    }

    private static string GetDefaultDistributionRule(string category, int weeklyCount)
    {
        if (category == "主科" && weeklyCount >= 4) return "均衡分布";
        if (category == "主科") return "均衡分布";
        if (weeklyCount >= 3) return "均衡分布";
        return "集中安排";
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

    public string ExportFolderPath => AppPaths.ExportFolder;
    public string DataFilePath => AppPaths.DataFile;
    public string ProjectFilePath => _projectFilePath;

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
            }
        }
    }

    public List<SubjectDefinition> FilteredSubjects => string.IsNullOrWhiteSpace(CurrentSubjectGradeName) || CurrentSubjectGradeName == "全部"
        ? Subjects.ToList()
        : Subjects.Where(s => s.GradeName == CurrentSubjectGradeName).ToList();

    public bool IsAllSubjectsGrade => CurrentSubjectGradeName == "全部";
    public bool IsSubjectGradeSelected => !string.IsNullOrWhiteSpace(CurrentSubjectGradeName) && CurrentSubjectGradeName != "全部";

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
    public RelayCommand SearchTeacherCommand { get; }
    public RelayCommand AddSubjectCommand { get; }
    public RelayCommand DeleteSubjectCommand { get; }
    public RelayCommand FilterGradeCommand { get; }

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

    private void CancelOperation()
    {
        _cts?.Cancel();
        StatusMessage = "正在取消...";
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
        IncludeEveningSelfStudy = false;
        EveningPeriods = 2;
        StatusMessage = "已创建新项目";
        SelectedConfigPage = "基础设置";
        RefreshViews();
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
            IProgress<double> progress = new Progress<double>(v => ProgressValue = v);
            SchoolData data = await Task.Run(() => SampleDataFactory.Create(progress, _cts.Token), _cts.Token);

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

    private async Task AutoScheduleAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        ProgressMessage = "正在生成需求...";
        ProgressValue = 0;

        try
        {
            _cts = new CancellationTokenSource();
            IProgress<double> progress = new Progress<double>(v => ProgressValue = v);

            GenerateRequirements();

            ProgressMessage = "正在自动排课...";
            SchoolData data = BuildSchoolData();
            ScheduleResult result = await Task.Run(
                () => _scheduleService.Generate(data, progress, _cts.Token), _cts.Token);

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
        catch (OperationCanceledException)
        {
            StatusMessage = "排课已取消";
        }
        finally
        {
            IsBusy = false;
            _cts = null;
            ProgressValue = 0;
            ProgressMessage = string.Empty;
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

    private async Task ImportExcelAsync()
    {
        if (IsBusy) return;

        var picker = new Windows.Storage.Pickers.FileOpenPicker
        {
            FileTypeFilter = { ".xlsx" },
            ViewMode = Windows.Storage.Pickers.PickerViewMode.List
        };

        nint hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.CurrentWindow);
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
            entries = entries.Where(x => x.TeacherId == SelectedTeacher.Id);
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
    }

    private void ApplySchoolData(SchoolData data)
    {
        SchoolName = data.Settings.SchoolName;
        DaysPerWeek = data.Settings.DaysPerWeek;
        PeriodsPerDay = data.Settings.PeriodsPerDay;
        MorningPeriods = data.Settings.MorningPeriods;
        AfternoonPeriods = data.Settings.AfternoonPeriods;
        IncludeEveningSelfStudy = data.Settings.IncludeEveningSelfStudy;
        EveningPeriods = data.Settings.EveningPeriods;

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
        return new SchoolData
        {
            Settings = new ScheduleSettings
            {
                SchoolName = SchoolName,
                DaysPerWeek = DaysPerWeek,
                PeriodsPerDay = PeriodsPerDay,
                MorningPeriods = MorningPeriods,
                AfternoonPeriods = AfternoonPeriods,
                IncludeEveningSelfStudy = IncludeEveningSelfStudy,
                EveningPeriods = EveningPeriods
            },
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
