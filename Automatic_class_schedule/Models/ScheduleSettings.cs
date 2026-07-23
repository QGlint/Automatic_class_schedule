namespace Automatic_class_schedule.Models;

public sealed class ScheduleSettings : Infrastructure.ObservableObject
{
    private int _daysPerWeek = 5;
    private int _periodsPerDay = 7;
    private string _schoolName = "中学排课示例";

    public int DaysPerWeek
    {
        get => _daysPerWeek;
        set => SetProperty(ref _daysPerWeek, value);
    }

    public int PeriodsPerDay
    {
        get => _periodsPerDay;
        set => SetProperty(ref _periodsPerDay, value);
    }

    public string SchoolName
    {
        get => _schoolName;
        set => SetProperty(ref _schoolName, value);
    }
}
