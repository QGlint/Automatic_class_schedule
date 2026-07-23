namespace Automatic_class_schedule.Models;

public enum ScheduleConflictSeverity
{
    Info,
    Warning,
    Hard
}

public enum ScheduleConflictType
{
    TeacherConflict,
    ClassConflict,
    FixedLessonConflict,
    UnscheduledLesson,
    PreferenceConflict
}

public sealed class ScheduleConflict : Infrastructure.ObservableObject
{
    private ScheduleConflictSeverity _severity;
    private ScheduleConflictType _type;
    private string _message = string.Empty;
    private string _scope = string.Empty;

    public ScheduleConflictSeverity Severity
    {
        get => _severity;
        set => SetProperty(ref _severity, value);
    }

    public ScheduleConflictType Type
    {
        get => _type;
        set => SetProperty(ref _type, value);
    }

    public string Message
    {
        get => _message;
        set => SetProperty(ref _message, value);
    }

    public string Scope
    {
        get => _scope;
        set => SetProperty(ref _scope, value);
    }
}
