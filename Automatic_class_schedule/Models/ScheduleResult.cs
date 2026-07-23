namespace Automatic_class_schedule.Models;

public sealed class ScheduleResult
{
    public List<ScheduleEntry> Entries { get; } = new();
    public List<ScheduleConflict> Conflicts { get; } = new();
    public List<string> UnscheduledLessons { get; } = new();
}
