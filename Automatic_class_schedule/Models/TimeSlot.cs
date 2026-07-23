namespace Automatic_class_schedule.Models;

public readonly record struct TimeSlot(int DayIndex, int PeriodIndex)
{
    public string DisplayName => $"周{DayIndex + 1} 第{PeriodIndex}节";
}
