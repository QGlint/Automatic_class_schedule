using Automatic_class_schedule.Models;

namespace Automatic_class_schedule.Services;

public static class SampleDataFactory
{
    public static SchoolData Create(IProgress<double>? progress = null, CancellationToken ct = default)
    {
        SchoolData data = new()
        {
            Settings = new ScheduleSettings
            {
                SchoolName = "自动排课示例",
                DaysPerWeek = 5,
                PeriodsPerDay = 7,
                MorningPeriods = 4,
                AfternoonPeriods = 3
            }
        };

        ct.ThrowIfCancellationRequested();
        progress?.Report(0);
        data.GradeInputs.AddRange(new[]
        {
            new GradeInput { GradeName = "七年级", ClassCount = 8 },
            new GradeInput { GradeName = "八年级", ClassCount = 8 },
            new GradeInput { GradeName = "九年级", ClassCount = 6 }
        });

        ScheduleService service = new();
        data.Classes.AddRange(service.BuildClasses(data.GradeInputs));

        data.Subjects.AddRange(new[]
        {
            new SubjectDefinition { Name = "语文", Category = "主科", DefaultWeeklyCount = 6 },
            new SubjectDefinition { Name = "数学", Category = "主科", DefaultWeeklyCount = 6 },
            new SubjectDefinition { Name = "英语", Category = "主科", DefaultWeeklyCount = 5 },
            new SubjectDefinition { Name = "物理", Category = "理科", DefaultWeeklyCount = 4 },
            new SubjectDefinition { Name = "化学", Category = "理科", DefaultWeeklyCount = 3 },
            new SubjectDefinition { Name = "生物", Category = "理科", DefaultWeeklyCount = 3 },
            new SubjectDefinition { Name = "历史", Category = "文科", DefaultWeeklyCount = 3 },
            new SubjectDefinition { Name = "地理", Category = "文科", DefaultWeeklyCount = 3 },
            new SubjectDefinition { Name = "政治", Category = "文科", DefaultWeeklyCount = 3 },
            new SubjectDefinition { Name = "体育", Category = "副科", DefaultWeeklyCount = 3 }
        });
        ct.ThrowIfCancellationRequested();
        progress?.Report(0.25);

        service.GenerateAssignments(data.TeacherAssignments, data.Subjects, data.Classes);
        data.Requirements.AddRange(service.BuildRequirementsFromAssignments(data.TeacherAssignments, data.Classes, data.Subjects));

        data.FixedLessons.Add(new FixedLesson
        {
            Scope = FixedLessonScope.All,
            ScopeValue = "全校",
            DayIndex = 0,
            PeriodIndex = 1,
            Subject = "升旗",
            Reason = "全校升旗"
        });
        ct.ThrowIfCancellationRequested();
        progress?.Report(0.5);

        ScheduleResult result = service.Generate(data, progress, ct);
        data.ScheduleEntries.AddRange(result.Entries);
        progress?.Report(1.0);

        return data;
    }
}
