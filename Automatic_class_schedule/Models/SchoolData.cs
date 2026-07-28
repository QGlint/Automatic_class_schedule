namespace Automatic_class_schedule.Models;

public sealed class SchoolData
{
    public string ProjectName { get; set; } = "";
    public ScheduleSettings Settings { get; set; } = new();
    public List<GradeScheduleConfig> GradeConfigs { get; set; } = new();
    public List<GradeInput> GradeInputs { get; set; } = new();
    public List<SchoolClass> Classes { get; set; } = new();
    public List<Teacher> Teachers { get; set; } = new();
    public List<SubjectDefinition> Subjects { get; set; } = new();
    public List<TeacherAssignment> TeacherAssignments { get; set; } = new();
    public List<LessonRequirement> Requirements { get; set; } = new();
    public List<FixedLesson> FixedLessons { get; set; } = new();
    public List<ScheduleEntry> ScheduleEntries { get; set; } = new();
    /// <summary>教师生成配置（随项目保存）</summary>
    public TeacherGenConfig? TeacherGenConfig { get; set; }
}
