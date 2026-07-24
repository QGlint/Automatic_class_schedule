namespace Automatic_class_schedule.Models;

public sealed class ScheduleSettings : Infrastructure.ObservableObject
{
    private int _daysPerWeek = 5;
    private int _periodsPerDay = 7;
    private int _morningPeriods = 4;
    private int _afternoonPeriods = 3;
    private bool _includeEveningSelfStudy;
    private int _eveningPeriods = 2;
    private string _schoolName = "";

    public int DaysPerWeek
    {
        get => _daysPerWeek;
        set => SetProperty(ref _daysPerWeek, value);
    }

    public int PeriodsPerDay
    {
        get => _periodsPerDay;
        set
        {
            if (SetProperty(ref _periodsPerDay, value))
            {
                OnPropertyChanged(nameof(MorningPeriods));
                OnPropertyChanged(nameof(AfternoonPeriods));
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
                OnPropertyChanged(nameof(PeriodsPerDay));
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
                OnPropertyChanged(nameof(PeriodsPerDay));
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
                OnPropertyChanged(nameof(PeriodsPerDay));
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
                OnPropertyChanged(nameof(PeriodsPerDay));
            }
        }
    }

    public string SchoolName
    {
        get => _schoolName;
        set => SetProperty(ref _schoolName, value);
    }
}
