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
    private string _target = string.Empty;

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

    /// <summary>具体涉及的对象（老师姓名或班级名称）</summary>
    public string Target
    {
        get => _target;
        set => SetProperty(ref _target, value);
    }

    /// <summary>中文级别显示</summary>
    public string SeverityText => _severity switch
    {
        ScheduleConflictSeverity.Info => "信息",
        ScheduleConflictSeverity.Warning => "警告",
        ScheduleConflictSeverity.Hard => "错误",
        _ => "信息"
    };

    /// <summary>中文类型显示</summary>
    public string TypeText => _type switch
    {
        ScheduleConflictType.TeacherConflict => "教师冲突",
        ScheduleConflictType.ClassConflict => "班级冲突",
        ScheduleConflictType.FixedLessonConflict => "固定课冲突",
        ScheduleConflictType.UnscheduledLesson => "未排课程",
        ScheduleConflictType.PreferenceConflict => "偏好冲突",
        _ => "其他"
    };
}
