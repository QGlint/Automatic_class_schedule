using Automatic_class_schedule.Models;
using Automatic_class_schedule.Services;
using Automatic_class_schedule.Solver;

namespace Automatic_class_schedule.Tests;

[Trait("Category", "LocalOnly")]
public sealed class ScheduleIntegrationTests
{
    [Fact]
    public void FullWorkflow_GenerateAndValidate_NoHardConflicts()
    {
        var data = SampleDataFactory.Create();
        var service = new ScheduleService(new CpSatScheduleSolver(), new ConflictService());

        var result = service.Generate(data);
        Assert.NotEmpty(result.Entries);
        Assert.DoesNotContain(result.Conflicts, c => c.Severity == ScheduleConflictSeverity.Hard);

        data.ScheduleEntries.Clear();
        data.ScheduleEntries.AddRange(result.Entries);
        var validationConflicts = service.Validate(data);
        Assert.DoesNotContain(validationConflicts, c => c.Severity == ScheduleConflictSeverity.Hard);
    }

    [Fact]
    public void FullWorkflow_ManualMove_ValidatesCorrectly()
    {
        var data = new SchoolData
        {
            Settings = new ScheduleSettings
            {
                SchoolName = "Test",
                DaysPerWeek = 5,
                PeriodsPerDay = 7,
                MorningPeriods = 4,
                AfternoonPeriods = 3
            }
        };
        data.Classes.Add(new SchoolClass { Name = "七年级1班" });
        data.Requirements.Add(new LessonRequirement
        {
            ClassId = data.Classes[0].Id,
            ClassName = "七年级1班",
            TeacherId = Guid.NewGuid(),
            TeacherName = "张老师",
            Subject = "语文",
            WeeklyCount = 5,
            DistributionRule = "均衡分布"
        });

        var service = new ScheduleService();
        var result = service.Generate(data);
        Assert.NotEmpty(result.Entries);
        data.ScheduleEntries.AddRange(result.Entries);

        var entry = result.Entries[0];
        bool moved = false;

        for (int d = 0; d < data.Settings.DaysPerWeek; d++)
        {
            for (int p = 1; p <= data.Settings.PeriodsPerDay; p++)
            {
                if (d == entry.DayIndex && p == entry.PeriodIndex)
                    continue;

                if (service.TryMoveEntry(data, entry, d, p, out _))
                {
                    moved = true;
                    Assert.Equal(d, entry.DayIndex);
                    Assert.Equal(p, entry.PeriodIndex);
                    break;
                }
            }
            if (moved) break;
        }

        Assert.True(moved, "Should find at least one free slot to move into");
    }

    [Fact]
    public void FullWorkflow_DuplicateMove_Rejected()
    {
        var data = new SchoolData
        {
            Settings = new ScheduleSettings
            {
                DaysPerWeek = 5,
                PeriodsPerDay = 7,
                MorningPeriods = 4,
                AfternoonPeriods = 3
            }
        };
        data.Classes.Add(new SchoolClass { Name = "七年级1班" });
        data.Requirements.Add(new LessonRequirement
        {
            ClassId = data.Classes[0].Id,
            ClassName = "七年级1班",
            TeacherId = Guid.NewGuid(),
            TeacherName = "张老师",
            Subject = "语文",
            WeeklyCount = 6
        });

        var service = new ScheduleService();
        var result = service.Generate(data);
        data.ScheduleEntries.AddRange(result.Entries);

        var dup = new ScheduleEntry
        {
            Id = Guid.NewGuid(),
            ClassId = data.Classes[0].Id,
            TeacherId = Guid.NewGuid(),
            DayIndex = result.Entries[0].DayIndex,
            PeriodIndex = result.Entries[0].PeriodIndex,
            Subject = "冲突课程",
            TeacherName = "冲突教师",
            ClassName = data.Classes[0].Name
        };
        data.ScheduleEntries.Add(dup);

        var conflicts = service.Validate(data);
        var hardConflicts = conflicts.Where(c => c.Severity == ScheduleConflictSeverity.Hard).ToList();
        Assert.NotEmpty(hardConflicts);
    }

    [Fact]
    public void BuildClasses_ThenGenerateRequirements_ThenSchedule_Completes()
    {
        var grades = new List<GradeInput>
        {
            new() { GradeName = "高一", ClassCount = 4 },
            new() { GradeName = "高二", ClassCount = 3 }
        };

        var service = new ScheduleService();
        var classes = service.BuildClasses(grades);
        Assert.Equal(7, classes.Count);

        var subjects = new List<SubjectDefinition>
        {
            new() { Name = "语文", Category = "主科", DefaultWeeklyCount = 6 },
            new() { Name = "数学", Category = "主科", DefaultWeeklyCount = 6 },
            new() { Name = "英语", Category = "主科", DefaultWeeklyCount = 5 }
        };

        var assignments = new List<TeacherAssignment>();
        service.GenerateAssignments(assignments, subjects, classes);
        Assert.NotEmpty(assignments);

        var requirements = service.BuildRequirementsFromAssignments(assignments, classes, subjects);
        Assert.NotEmpty(requirements);

        var data = new SchoolData
        {
            Settings = new ScheduleSettings
            {
                SchoolName = "完整流程测试",
                DaysPerWeek = 5,
                PeriodsPerDay = 7,
                MorningPeriods = 4,
                AfternoonPeriods = 3
            }
        };
        foreach (var r in requirements) data.Requirements.Add(r);

        int totalSlots = classes.Count * data.Settings.DaysPerWeek * data.Settings.PeriodsPerDay;
        int totalLessons = requirements.Sum(r => r.WeeklyCount);
        int expectedMax = Math.Min(totalLessons, totalSlots);

        var result = service.Generate(data);
        Assert.NotEmpty(result.Entries);
        int placedCount = result.Entries.Count;

        Assert.True(placedCount >= expectedMax * 0.9,
            $"Placed {placedCount}/{totalLessons} (max possible: {expectedMax})");
    }
}