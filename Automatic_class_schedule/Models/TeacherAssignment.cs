namespace Automatic_class_schedule.Models;

public sealed class TeacherAssignment : Infrastructure.ObservableObject
{
    /// <summary>用于解析默认周课时的委托（由ViewModel设置）</summary>
    public static Func<string, string, int>? DefaultWeeklyCountResolver { get; set; }

    private Guid _id = Guid.NewGuid();
    private string _teacherName = string.Empty;
    private string _subject = string.Empty;
    private int _weeklyCount;
    private string _gradeName = string.Empty;
    private string _classNumbers = string.Empty;
    private string _classNames = string.Empty;
    private string _distributionRule = "每天一次";
    private bool _preferMorning;
    private bool _avoidLastPeriod;

    public Guid Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public string TeacherName
    {
        get => _teacherName;
        set => SetProperty(ref _teacherName, value);
    }

    public string Subject
    {
        get => _subject;
        set
        {
            if (SetProperty(ref _subject, value))
            {
                OnPropertyChanged(nameof(WeeklyCountInput));
                OnPropertyChanged(nameof(WeeklyCountPlaceholder));
            }
        }
    }

    /// <summary>周课时（0=继承年级默认值）</summary>
    public int WeeklyCount
    {
        get => _weeklyCount;
        set
        {
            if (SetProperty(ref _weeklyCount, value))
                OnPropertyChanged(nameof(WeeklyCountInput));
        }
    }

    /// <summary>UI编辑用字符串属性（空=继承默认）</summary>
    public string WeeklyCountInput
    {
        get => _weeklyCount > 0 ? _weeklyCount.ToString() : string.Empty;
        set
        {
            int parsed = int.TryParse(value?.Trim(), out int v) && v > 0 ? v : 0;
            WeeklyCount = parsed;
        }
    }

    /// <summary>PlaceholderText：灰色显示继承的默认值</summary>
    public string WeeklyCountPlaceholder
    {
        get
        {
            int defaultCount = DefaultWeeklyCountResolver?.Invoke(_subject, _gradeName) ?? 0;
            return defaultCount > 0 ? $"·{defaultCount}（默认）" : "";
        }
    }

    public string GradeName
    {
        get => _gradeName;
        set
        {
            if (SetProperty(ref _gradeName, value))
            {
                UpdateClassNames();
                OnPropertyChanged(nameof(WeeklyCountPlaceholder));
            }
        }
    }

    public string ClassNumbers
    {
        get => _classNumbers;
        set
        {
            if (SetProperty(ref _classNumbers, value))
                UpdateClassNames();
        }
    }

    public string ClassNames
    {
        get => _classNames;
        set => SetProperty(ref _classNames, value);
    }

    public string DistributionRule
    {
        get => _distributionRule;
        set => SetProperty(ref _distributionRule, value);
    }

    public bool PreferMorning
    {
        get => _preferMorning;
        set => SetProperty(ref _preferMorning, value);
    }

    public bool AvoidLastPeriod
    {
        get => _avoidLastPeriod;
        set => SetProperty(ref _avoidLastPeriod, value);
    }

    private void UpdateClassNames()
    {
        if (!string.IsNullOrWhiteSpace(_gradeName) && !string.IsNullOrWhiteSpace(_classNumbers))
        {
            string shortGrade = _gradeName.Replace("年级", "");
            var numbers = _classNumbers.Split(new[] { ',', '，' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            _classNames = string.Join("、", numbers.Select(n => $"{shortGrade}{n}班"));
            OnPropertyChanged(nameof(ClassNames));
        }
    }
}