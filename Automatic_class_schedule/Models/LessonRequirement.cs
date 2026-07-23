namespace Automatic_class_schedule.Models;

public sealed class LessonRequirement : Infrastructure.ObservableObject
{
    private Guid _id = Guid.NewGuid();
    private Guid _classId;
    private Guid _teacherId;
    private string _className = string.Empty;
    private string _teacherName = string.Empty;
    private string _subject = string.Empty;
    private int _weeklyCount = 5;
    private string _distributionRule = "每天一次";
    private bool _preferMorning = true;
    private bool _avoidLastPeriod;

    public Guid Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public Guid ClassId
    {
        get => _classId;
        set => SetProperty(ref _classId, value);
    }

    public Guid TeacherId
    {
        get => _teacherId;
        set => SetProperty(ref _teacherId, value);
    }

    public string ClassName
    {
        get => _className;
        set => SetProperty(ref _className, value);
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
}
