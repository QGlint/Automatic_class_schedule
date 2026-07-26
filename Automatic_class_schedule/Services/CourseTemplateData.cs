using Automatic_class_schedule.Models;

namespace Automatic_class_schedule.Services;

/// <summary>课程配置模板数据（含科目和固定课程）</summary>
public sealed class CourseTemplateData
{
    public List<SubjectDefinition> Subjects { get; set; } = new();
    public List<FixedLesson> FixedLessons { get; set; } = new();
}
