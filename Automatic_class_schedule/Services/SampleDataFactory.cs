using Automatic_class_schedule.Models;

namespace Automatic_class_schedule.Services;

public static class SampleDataFactory
{
    public static SchoolData Create(IProgress<double>? progress = null, CancellationToken ct = default, bool skipSolve = false)
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
        progress?.Report(0.05);
        data.GradeInputs.AddRange(new[]
        {
            new GradeInput { GradeName = "七年级", ClassCount = 7 },
            new GradeInput { GradeName = "八年级", ClassCount = 8 },
            new GradeInput { GradeName = "九年级", ClassCount = 8 }
        });

        ScheduleService service = new();
        data.Classes.AddRange(service.BuildClasses(data.GradeInputs));
        progress?.Report(0.15);

        data.Subjects.AddRange(new[]
        {
            // 主科（全年级）
            new SubjectDefinition { Name = "语文", Category = "主科", DefaultWeeklyCount = 7, DistributionRule = "每天一次", GradeName = "七年级" },
            new SubjectDefinition { Name = "语文", Category = "主科", DefaultWeeklyCount = 6, DistributionRule = "每天一次", GradeName = "八年级" },
            new SubjectDefinition { Name = "语文", Category = "主科", DefaultWeeklyCount = 7, DistributionRule = "每天一次", GradeName = "九年级" },
            new SubjectDefinition { Name = "数学", Category = "主科", DefaultWeeklyCount = 7, DistributionRule = "每天一次", GradeName = "七年级" },
            new SubjectDefinition { Name = "数学", Category = "主科", DefaultWeeklyCount = 6, DistributionRule = "每天一次", GradeName = "八年级" },
            new SubjectDefinition { Name = "数学", Category = "主科", DefaultWeeklyCount = 6, DistributionRule = "每天一次", GradeName = "九年级" },
            new SubjectDefinition { Name = "英语", Category = "主科", DefaultWeeklyCount = 7, DistributionRule = "每天一次", GradeName = "七年级" },
            new SubjectDefinition { Name = "英语", Category = "主科", DefaultWeeklyCount = 6, DistributionRule = "每天一次", GradeName = "八年级" },
            new SubjectDefinition { Name = "英语", Category = "主科", DefaultWeeklyCount = 6, DistributionRule = "每天一次", GradeName = "九年级" },
            // 理科（物理仅八/九年级，化学仅九年级）
            new SubjectDefinition { Name = "物理", Category = "理科", DefaultWeeklyCount = 3, DistributionRule = "均衡分布", GradeName = "八年级" },
            new SubjectDefinition { Name = "物理", Category = "理科", DefaultWeeklyCount = 4, DistributionRule = "均衡分布", GradeName = "九年级" },
            new SubjectDefinition { Name = "化学", Category = "理科", DefaultWeeklyCount = 4, DistributionRule = "均衡分布", GradeName = "九年级" },
            // 文科/理科（七年级有地/生，八年级有地/生，九年级无）
            new SubjectDefinition { Name = "生物", Category = "理科", DefaultWeeklyCount = 2, DistributionRule = "均衡分布", GradeName = "七年级" },
            new SubjectDefinition { Name = "生物", Category = "理科", DefaultWeeklyCount = 2, DistributionRule = "均衡分布", GradeName = "八年级" },
            new SubjectDefinition { Name = "历史", Category = "文科", DefaultWeeklyCount = 2, DistributionRule = "均衡分布", GradeName = "七年级" },
            new SubjectDefinition { Name = "历史", Category = "文科", DefaultWeeklyCount = 2, DistributionRule = "均衡分布", GradeName = "八年级" },
            new SubjectDefinition { Name = "历史", Category = "文科", DefaultWeeklyCount = 2, DistributionRule = "均衡分布", GradeName = "九年级" },
            new SubjectDefinition { Name = "地理", Category = "文科", DefaultWeeklyCount = 2, DistributionRule = "均衡分布", GradeName = "七年级" },
            new SubjectDefinition { Name = "地理", Category = "文科", DefaultWeeklyCount = 2, DistributionRule = "均衡分布", GradeName = "八年级" },
            new SubjectDefinition { Name = "道德", Category = "文科", DefaultWeeklyCount = 2, DistributionRule = "均衡分布" },
            // 副科（体育全年级，音美全年级，信劳仅七八年级）
            new SubjectDefinition { Name = "体育", Category = "副科", DefaultWeeklyCount = 3, DistributionRule = "均衡分布" },
            new SubjectDefinition { Name = "音乐", Category = "副科", DefaultWeeklyCount = 1, DistributionRule = "均衡分布" },
            new SubjectDefinition { Name = "美术", Category = "副科", DefaultWeeklyCount = 1, DistributionRule = "均衡分布" },
            new SubjectDefinition { Name = "信息", Category = "副科", DefaultWeeklyCount = 1, DistributionRule = "均衡分布", GradeName = "七年级" },
            new SubjectDefinition { Name = "信息", Category = "副科", DefaultWeeklyCount = 1, DistributionRule = "均衡分布", GradeName = "八年级" },
            new SubjectDefinition { Name = "劳动", Category = "副科", DefaultWeeklyCount = 1, DistributionRule = "均衡分布", GradeName = "七年级" },
            new SubjectDefinition { Name = "劳动", Category = "副科", DefaultWeeklyCount = 1, DistributionRule = "均衡分布", GradeName = "八年级" }
        });
        // 七年级: 语7+数7+英7+道2+历2+地2+生2+体3+音1+美1+信1+劳1=36, +固定4=40
        // 八年级: 语6+数6+英6+物3+道2+历2+地2+生2+体3+音1+美1+信1+劳1=36, +固定4=40
        // 九年级: 语7+数6+英6+物4+化4+道2+历2+体3+音1+美1=36, +固定4=40

        // 固定课程（全校）
        data.FixedLessons.AddRange(new[]
        {
            new FixedLesson { Scope = FixedLessonScope.All, ScopeValue = "全校", DayIndex = 1, PeriodIndex = 8, Subject = "周会", Reason = "每周例会" },
            new FixedLesson { Scope = FixedLessonScope.All, ScopeValue = "全校", DayIndex = 5, PeriodIndex = 6, Subject = "社团", Reason = "社团活动" },
            new FixedLesson { Scope = FixedLessonScope.All, ScopeValue = "全校", DayIndex = 5, PeriodIndex = 7, Subject = "活动", Reason = "课外活动" },
            new FixedLesson { Scope = FixedLessonScope.All, ScopeValue = "全校", DayIndex = 5, PeriodIndex = 8, Subject = "教育", Reason = "主题教育" }
        });
        ct.ThrowIfCancellationRequested();
        progress?.Report(0.3);

        service.GenerateAssignments(data.TeacherAssignments, data.Subjects, data.Classes);
        progress?.Report(0.5);
        data.Requirements.AddRange(service.BuildRequirementsFromAssignments(data.TeacherAssignments, data.Classes, data.Subjects));
        ct.ThrowIfCancellationRequested();
        progress?.Report(0.6);

        if (!skipSolve)
        {
            ScheduleResult result = service.Generate(data, progress, ct);
            data.ScheduleEntries.AddRange(result.Entries);
        }

        progress?.Report(1.0);
        return data;
    }
}