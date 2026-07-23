namespace Automatic_class_schedule.Models;

public enum FixedLessonScope
{
    All,
    Grade,
    Class,
    Teacher
}

public sealed class FixedLesson : Infrastructure.ObservableObject
{
    private Guid _id = Guid.NewGuid();
    private FixedLessonScope _scope = FixedLessonScope.All;
    private string _scopeValue = "全校";
    private int _dayIndex;
    private int _periodIndex = 1;
    private string _subject = string.Empty;
    private string _teacherName = string.Empty;
    private string _className = string.Empty;
    private string _reason = "固定课程";

    public Guid Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public FixedLessonScope Scope
    {
        get => _scope;
        set => SetProperty(ref _scope, value);
    }

    public string ScopeValue
    {
        get => _scopeValue;
        set => SetProperty(ref _scopeValue, value);
    }

    public int DayIndex
    {
        get => _dayIndex;
        set => SetProperty(ref _dayIndex, value);
    }

    public int PeriodIndex
    {
        get => _periodIndex;
        set => SetProperty(ref _periodIndex, value);
    }

    public string Subject
    {
        get => _subject;
        set => SetProperty(ref _subject, value);
    }

    public string TeacherName
    {
        get => _teacherName;
        set => SetProperty(ref _teacherName, value);
    }

    public string ClassName
    {
        get => _className;
        set => SetProperty(ref _className, value);
    }

    public string Reason
    {
        get => _reason;
        set => SetProperty(ref _reason, value);
    }
}
