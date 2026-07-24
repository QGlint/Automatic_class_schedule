namespace Automatic_class_schedule.Models;

public sealed class TeacherAssignment : Infrastructure.ObservableObject
{
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
        set => SetProperty(ref _subject, value);
    }

    public int WeeklyCount
    {
        get => _weeklyCount;
        set => SetProperty(ref _weeklyCount, value);
    }

    public string GradeName
    {
        get => _gradeName;
        set
        {
            if (SetProperty(ref _gradeName, value))
                UpdateClassNames();
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