namespace Automatic_class_schedule.Models;

public sealed class SchoolData
{
    public ScheduleSettings Settings { get; set; } = new();
    public List<GradeInput> GradeInputs { get; set; } = new();
    public List<SchoolClass> Classes { get; set; } = new();
    public List<Teacher> Teachers { get; set; } = new();
    public List<SubjectDefinition> Subjects { get; set; } = new();
    public List<LessonRequirement> Requirements { get; set; } = new();
    public List<FixedLesson> FixedLessons { get; set; } = new();
    public List<ScheduleEntry> ScheduleEntries { get; set; } = new();
}
