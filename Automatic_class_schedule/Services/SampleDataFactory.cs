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
                DaysPerWeek = 5,
                PeriodsPerDay = 8,
                MorningPeriods = 4,
                AfternoonPeriods = 4
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
            new SubjectDefinition { Name = "语文", Category = "主科", DefaultWeeklyCount = 6, DistributionRule = "每天一次" },
            new SubjectDefinition { Name = "数学", Category = "主科", DefaultWeeklyCount = 6, DistributionRule = "每天一次" },
            new SubjectDefinition { Name = "英语", Category = "主科", DefaultWeeklyCount = 5, DistributionRule = "每天一次" },
            new SubjectDefinition { Name = "物理", Category = "理科", DefaultWeeklyCount = 3, DistributionRule = "均衡分布" },
            new SubjectDefinition { Name = "化学", Category = "理科", DefaultWeeklyCount = 3, DistributionRule = "均衡分布" },
            new SubjectDefinition { Name = "生物", Category = "理科", DefaultWeeklyCount = 2, DistributionRule = "均衡分布" },
            new SubjectDefinition { Name = "历史", Category = "文科", DefaultWeeklyCount = 2, DistributionRule = "均衡分布" },
            new SubjectDefinition { Name = "地理", Category = "文科", DefaultWeeklyCount = 2, DistributionRule = "均衡分布" },
            new SubjectDefinition { Name = "道德", Category = "文科", DefaultWeeklyCount = 2, DistributionRule = "均衡分布" },
            new SubjectDefinition { Name = "体育", Category = "副科", DefaultWeeklyCount = 3, DistributionRule = "均衡分布" },
            new SubjectDefinition { Name = "音乐", Category = "副科", DefaultWeeklyCount = 1, DistributionRule = "均衡分布" },
            new SubjectDefinition { Name = "美术", Category = "副科", DefaultWeeklyCount = 1, DistributionRule = "均衡分布" },
            new SubjectDefinition { Name = "信息", Category = "副科", DefaultWeeklyCount = 1, DistributionRule = "均衡分布" },
            new SubjectDefinition { Name = "劳动", Category = "副科", DefaultWeeklyCount = 1, DistributionRule = "均衡分布" }
        });
        ct.ThrowIfCancellationRequested();
        progress?.Report(0.25);

        service.GenerateAssignments(data.TeacherAssignments, data.Subjects, data.Classes);
        data.Requirements.AddRange(service.BuildRequirementsFromAssignments(data.TeacherAssignments, data.Classes, data.Subjects));
        ct.ThrowIfCancellationRequested();
        progress?.Report(0.5);

        ScheduleResult result = service.Generate(data, progress, ct);
        data.ScheduleEntries.AddRange(result.Entries);
        progress?.Report(1.0);

        return data;
    }
}