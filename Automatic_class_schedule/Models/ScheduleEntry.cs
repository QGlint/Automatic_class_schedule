namespace Automatic_class_schedule.Models;

public sealed class ScheduleEntry : Automatic_class_schedule.Infrastructure.ObservableObject
{
    private Guid _id = Guid.NewGuid();
    private Guid _requirementId;
    private Guid _classId;
    private Guid _teacherId;
    private string _className = string.Empty;
    private string _teacherName = string.Empty;
    private string _subject = string.Empty;
    private int _dayIndex;
    private int _periodIndex = 1;
    private bool _locked;
    private bool _isFixed;
    private string _note = string.Empty;

    public Guid Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public Guid RequirementId
    {
        get => _requirementId;
        set => SetProperty(ref _requirementId, value);
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
        set
        {
            if (SetProperty(ref _className, value))
            {
                OnPropertyChanged(nameof(Summary));
            }
        }
    }

    public string TeacherName
    {
        get => _teacherName;
        set
        {
            if (SetProperty(ref _teacherName, value))
            {
                OnPropertyChanged(nameof(Summary));
            }
        }
    }

    public string Subject
    {
        get => _subject;
        set
        {
            if (SetProperty(ref _subject, value))
            {
                OnPropertyChanged(nameof(Summary));
            }
        }
    }

    public int DayIndex
    {
        get => _dayIndex;
        set
        {
            if (SetProperty(ref _dayIndex, value))
            {
                OnPropertyChanged(nameof(SlotLabel));
            }
        }
    }

    public int PeriodIndex
    {
        get => _periodIndex;
        set
        {
            if (SetProperty(ref _periodIndex, value))
            {
                OnPropertyChanged(nameof(SlotLabel));
            }
        }
    }

    public bool Locked
    {
        get => _locked;
        set => SetProperty(ref _locked, value);
    }

    public bool IsFixed
    {
        get => _isFixed;
        set => SetProperty(ref _isFixed, value);
    }

    public string Note
    {
        get => _note;
        set => SetProperty(ref _note, value);
    }

    public string SlotLabel => $"周{DayIndex + 1} 第{PeriodIndex}节";
    public string Summary => $"{ClassName} {Subject} {TeacherName}";
}
