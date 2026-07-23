using Automatic_class_schedule.Models;
using Automatic_class_schedule.Services;

namespace Automatic_class_schedule.Tests;

public sealed class ConflictServiceTests
{
    private readonly ConflictService _service = new();

    private static ScheduleProblem CreateProblem() => new()
    {
        Settings = new ScheduleSettings
        {
            DaysPerWeek = 5,
            PeriodsPerDay = 7,
            MorningPeriods = 4,
            AfternoonPeriods = 3
        }
    };

    [Fact]
    public void Analyze_EmptyEntries_ReturnsNoConflicts()
    {
        var result = _service.Analyze(CreateProblem(), Array.Empty<ScheduleEntry>());
        Assert.Empty(result);
    }

    [Fact]
    public void Analyze_ValidEntry_ReturnsNoConflicts()
    {
        var entries = new List<ScheduleEntry>
        {
            new()
            {
                ClassId = Guid.NewGuid(),
                TeacherId = Guid.NewGuid(),
                DayIndex = 0,
                PeriodIndex = 1,
                Subject = "语文",
                TeacherName = "张老师",
                ClassName = "七年级1班"
            }
        };
        var result = _service.Analyze(CreateProblem(), entries);
        Assert.Empty(result);
    }

    [Fact]
    public void Analyze_SameClassSameSlot_DetectsClassConflict()
    {
        var classId = Guid.NewGuid();
        var entries = new List<ScheduleEntry>
        {
            new()
            {
                ClassId = classId,
                TeacherId = Guid.NewGuid(),
                DayIndex = 1,
                PeriodIndex = 2,
                Subject = "语文",
                TeacherName = "张老师",
                ClassName = "七年级1班"
            },
            new()
            {
                ClassId = classId,
                TeacherId = Guid.NewGuid(),
                DayIndex = 1,
                PeriodIndex = 2,
                Subject = "数学",
                TeacherName = "李老师",
                ClassName = "七年级1班"
            }
        };
        var result = _service.Analyze(CreateProblem(), entries);
        Assert.Contains(result, c => c.Type == ScheduleConflictType.ClassConflict);
    }

    [Fact]
    public void Analyze_SameTeacherSameSlot_DetectsTeacherConflict()
    {
        var teacherId = Guid.NewGuid();
        var entries = new List<ScheduleEntry>
        {
            new()
            {
                ClassId = Guid.NewGuid(),
                TeacherId = teacherId,
                DayIndex = 1,
                PeriodIndex = 2,
                Subject = "语文",
                TeacherName = "张老师",
                ClassName = "七年级1班"
            },
            new()
            {
                ClassId = Guid.NewGuid(),
                TeacherId = teacherId,
                DayIndex = 1,
                PeriodIndex = 2,
                Subject = "语文",
                TeacherName = "张老师",
                ClassName = "七年级2班"
            }
        };
        var result = _service.Analyze(CreateProblem(), entries);
        Assert.Contains(result, c => c.Type == ScheduleConflictType.TeacherConflict);
    }

    [Fact]
    public void Analyze_OutOfRangeSlot_DetectsUnscheduled()
    {
        var entries = new List<ScheduleEntry>
        {
            new()
            {
                ClassId = Guid.NewGuid(),
                TeacherId = Guid.NewGuid(),
                DayIndex = 10,
                PeriodIndex = 20,
                Subject = "语文",
                TeacherName = "张老师",
                ClassName = "七年级1班"
            }
        };
        var result = _service.Analyze(CreateProblem(), entries);
        Assert.Contains(result, c => c.Type == ScheduleConflictType.UnscheduledLesson);
    }
}