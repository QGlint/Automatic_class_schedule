using Automatic_class_schedule.Models;
using Automatic_class_schedule.Services;

namespace Automatic_class_schedule.Tests;

public sealed class ScheduleServiceTests
{
    private readonly ScheduleService _service = new();

    [Fact]
    public void BuildClasses_NullInput_ReturnsEmpty()
    {
        var result = _service.BuildClasses(Array.Empty<GradeInput>());
        Assert.Empty(result);
    }

    [Fact]
    public void BuildClasses_SingleGrade_CreatesCorrectCount()
    {
        var grades = new[] { new GradeInput { GradeName = "七年级", ClassCount = 8 } };
        var result = _service.BuildClasses(grades);
        Assert.Equal(8, result.Count);
        Assert.All(result, c => Assert.StartsWith("七年级", c.Name));
    }

    [Fact]
    public void BuildClasses_MultipleGrades_CreatesAll()
    {
        var grades = new[]
        {
            new GradeInput { GradeName = "七年级", ClassCount = 8 },
            new GradeInput { GradeName = "八年级", ClassCount = 10 },
            new GradeInput { GradeName = "九年级", ClassCount = 6 }
        };
        var result = _service.BuildClasses(grades);
        Assert.Equal(24, result.Count);
    }

    [Fact]
    public void BuildClasses_ZeroClassCount_ReturnsEmpty()
    {
        var grades = new[] { new GradeInput { GradeName = "七年级", ClassCount = 0 } };
        var result = _service.BuildClasses(grades);
        Assert.Empty(result);
    }

    [Fact]
    public void BuildRequirementsFromAssignments_ValidInput_CreatesRequirements()
    {
        var classes = new List<SchoolClass>
        {
            new() { Name = "七年级1班" },
            new() { Name = "七年级2班" }
        };
        var subjects = new List<SubjectDefinition>
        {
            new() { Name = "语文", DefaultWeeklyCount = 6 }
        };
        var assignments = new List<TeacherAssignment>
        {
            new()
            {
                TeacherName = "张老师",
                Subject = "语文",
                WeeklyCount = 6,
                ClassNames = "七年级1班、七年级2班"
            }
        };

        var result = _service.BuildRequirementsFromAssignments(assignments, classes, subjects);
        Assert.Equal(2, result.Count);
        Assert.All(result, r => Assert.Equal("语文", r.Subject));
        Assert.All(result, r => Assert.Equal("张老师", r.TeacherName));
    }

    [Fact]
    public void BuildRequirementsFromAssignments_CommaSeparated_ParsesCorrectly()
    {
        var classes = new List<SchoolClass>
        {
            new() { Name = "七年级1班" },
            new() { Name = "七年级2班" },
            new() { Name = "七年级3班" }
        };
        var assignments = new List<TeacherAssignment>
        {
            new()
            {
                TeacherName = "李老师",
                Subject = "数学",
                WeeklyCount = 6,
                ClassNames = "七年级1班,七年级2班,七年级3班"
            }
        };

        var result = _service.BuildRequirementsFromAssignments(assignments, classes, new List<SubjectDefinition>());
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void Generate_SimpleData_ReturnsResult()
    {
        var data = new SchoolData
        {
            Settings = new ScheduleSettings
            {
                SchoolName = "测试学校",
                DaysPerWeek = 5,
                PeriodsPerDay = 7,
                MorningPeriods = 4,
                AfternoonPeriods = 3
            }
        };
        data.Classes.Add(new SchoolClass { Name = "七年级1班" });
        data.Requirements.Add(new LessonRequirement
        {
            ClassName = "七年级1班",
            TeacherName = "张老师",
            Subject = "语文",
            WeeklyCount = 6
        });

        var result = _service.Generate(data);
        Assert.NotNull(result);
        Assert.NotEmpty(result.Entries);
        Assert.Equal(6, result.Entries.Count);
    }

    [Fact]
    public void Generate_NoRequirements_ReturnsEmpty()
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

        var result = _service.Generate(data);
        Assert.Empty(result.Entries);
    }

    [Fact]
    public void GenerateAssignments_CreatesEntriesForAllSubjects()
    {
        var subjects = new List<SubjectDefinition>
        {
            new() { Name = "语文", DefaultWeeklyCount = 6 },
            new() { Name = "数学", DefaultWeeklyCount = 6 }
        };
        var classes = new List<SchoolClass>
        {
            new() { Name = "七年级1班" },
            new() { Name = "七年级2班" }
        };
        var assignments = new List<TeacherAssignment>();

        _service.GenerateAssignments(assignments, subjects, classes);
        Assert.NotEmpty(assignments);
        Assert.All(assignments, a => Assert.False(string.IsNullOrWhiteSpace(a.TeacherName)));
    }

    [Fact]
    public void Validate_EmptySchedule_ReturnsNoConflicts()
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

        var conflicts = _service.Validate(data);
        Assert.Empty(conflicts);
    }

    [Fact]
    public void Validate_DuplicateSlot_HasConflicts()
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
        var entry1 = new ScheduleEntry
        {
            ClassId = Guid.NewGuid(),
            TeacherId = Guid.NewGuid(),
            DayIndex = 0,
            PeriodIndex = 1,
            Subject = "语文",
            TeacherName = "张老师",
            ClassName = "七年级1班"
        };
        var entry2 = new ScheduleEntry
        {
            ClassId = entry1.ClassId,
            TeacherId = Guid.NewGuid(),
            DayIndex = 0,
            PeriodIndex = 1,
            Subject = "数学",
            TeacherName = "李老师",
            ClassName = "七年级1班"
        };
        data.ScheduleEntries.Add(entry1);
        data.ScheduleEntries.Add(entry2);

        var conflicts = _service.Validate(data);
        Assert.NotEmpty(conflicts);
    }

    [Fact]
    public void GetDefaultWeeklyCount_ReturnsCorrectValues()
    {
        Assert.Equal(6, ScheduleService.GetDefaultWeeklyCount("语文"));
        Assert.Equal(6, ScheduleService.GetDefaultWeeklyCount("数学"));
        Assert.Equal(5, ScheduleService.GetDefaultWeeklyCount("英语"));
        Assert.Equal(3, ScheduleService.GetDefaultWeeklyCount("物理"));
        Assert.Equal(3, ScheduleService.GetDefaultWeeklyCount("化学"));
        Assert.Equal(2, ScheduleService.GetDefaultWeeklyCount("体育"));
        Assert.Equal(2, ScheduleService.GetDefaultWeeklyCount("生物"));
        Assert.Equal(2, ScheduleService.GetDefaultWeeklyCount("历史"));
        Assert.Equal(1, ScheduleService.GetDefaultWeeklyCount("音乐"));
        Assert.Equal(2, ScheduleService.GetDefaultWeeklyCount("未知科目"));
    }
}