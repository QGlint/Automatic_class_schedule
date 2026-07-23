namespace Automatic_class_schedule.Models;

public sealed class LockedLesson
{
    public Guid RequirementId { get; init; }
    public Guid? EntryId { get; init; }
    public int DayIndex { get; init; }
    public int PeriodIndex { get; init; }
}
