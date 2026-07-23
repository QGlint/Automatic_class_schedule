using Automatic_class_schedule.Models;

namespace Automatic_class_schedule.Services;

public static class SampleDataFactory
{
    public static SchoolData Create()
    {
        SchoolData data = new()
        {
            Settings = new ScheduleSettings
            {
                SchoolName = "自动排课示例",
                DaysPerWeek = 5,
                PeriodsPerDay = 7
            }
        };

        data.GradeInputs.AddRange(new[]
        {
            new GradeInput { GradeName = "七年级", ClassCount = 8 },
            new GradeInput { GradeName = "八年级", ClassCount = 10 },
            new GradeInput { GradeName = "九年级", ClassCount = 6 }
        });

        data.Classes.AddRange(new ScheduleService().BuildClasses(data.GradeInputs));

        data.Teachers.AddRange(new[]
        {
            new Teacher { Name = "张三", Subject = "数学", Role = "教师" },
            new Teacher { Name = "李四", Subject = "英语", Role = "班主任" },
            new Teacher { Name = "王五", Subject = "语文", Role = "教师" },
            new Teacher { Name = "赵六", Subject = "体育", Role = "教师" },
            new Teacher { Name = "钱七", Subject = "历史", Role = "教师" }
        });

        data.Subjects.AddRange(new[]
        {
            new SubjectDefinition { Name = "数学", Category = "主科" },
            new SubjectDefinition { Name = "英语", Category = "主科" },
            new SubjectDefinition { Name = "语文", Category = "主科" },
            new SubjectDefinition { Name = "体育", Category = "副科" },
            new SubjectDefinition { Name = "历史", Category = "副科" }
        });

        data.Requirements.AddRange(new ScheduleService().BuildRequirements(data.Classes, data.Teachers));

        data.FixedLessons.Add(new FixedLesson
        {
            Scope = FixedLessonScope.All,
            ScopeValue = "全校",
            DayIndex = 0,
            PeriodIndex = 1,
            Subject = "升旗",
            Reason = "全校升旗"
        });

        return data;
    }
}
