namespace Automatic_class_schedule.Models;

public sealed class LessonInstance
{
    public Guid RequirementId { get; init; }
    public Guid ClassId { get; init; }
    public Guid TeacherId { get; init; }
    public string ClassName { get; init; } = string.Empty;
    public string TeacherName { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public int Sequence { get; init; }
    public string DistributionRule { get; init; } = string.Empty;
    public bool PreferMorning { get; init; }
    public bool AvoidLastPeriod { get; init; }
}
