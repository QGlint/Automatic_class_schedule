namespace Automatic_class_schedule.Models;

public sealed class ScheduleProblem
{
    public ScheduleSettings Settings { get; init; } = new();
    public IReadOnlyList<SchoolClass> Classes { get; init; } = Array.Empty<SchoolClass>();
    public IReadOnlyList<Teacher> Teachers { get; init; } = Array.Empty<Teacher>();
    public IReadOnlyList<LessonRequirement> Requirements { get; init; } = Array.Empty<LessonRequirement>();
    public IReadOnlyList<FixedLesson> FixedLessons { get; init; } = Array.Empty<FixedLesson>();
}
