using Automatic_class_schedule.Models;
using Automatic_class_schedule.Solver;

namespace Automatic_class_schedule.Tests;

public sealed class GreedyScheduleSolverTests
{
    private readonly GreedyScheduleSolver _solver = new();

    private static ScheduleProblem CreateProblem(int days = 5, int periods = 7,
        IReadOnlyList<LessonRequirement>? requirements = null)
    {
        return new ScheduleProblem
        {
            Settings = new ScheduleSettings
            {
                DaysPerWeek = days,
                PeriodsPerDay = periods,
                MorningPeriods = 4,
                AfternoonPeriods = 3
            },
            Requirements = requirements ?? Array.Empty<LessonRequirement>()
        };
    }

    [Fact]
    public void Solve_EmptyProblem_ReturnsEmptyResult()
    {
        var result = _solver.Solve(CreateProblem());
        Assert.Empty(result.Entries);
        Assert.Empty(result.Conflicts);
    }

    [Fact]
    public void Solve_SingleRequirement_PlacesCorrectly()
    {
        var problem = CreateProblem(requirements: new List<LessonRequirement>
        {
            new()
            {
                ClassId = Guid.NewGuid(),
                ClassName = "七年级1班",
                TeacherId = Guid.NewGuid(),
                TeacherName = "张老师",
                Subject = "语文",
                WeeklyCount = 5
            }
        });

        var result = _solver.Solve(problem);
        Assert.Equal(5, result.Entries.Count);
        Assert.All(result.Entries, e => Assert.Equal("语文", e.Subject));
    }

    [Fact]
    public void Solve_NoDays_ReturnsNoEntries()
    {
        var problem = CreateProblem(days: 0);
        var result = _solver.Solve(problem);
        Assert.Empty(result.Entries);
    }

    [Fact]
    public void Solve_FullWorkload_RespectsConstraints()
    {
        var classId = Guid.NewGuid();
        var teacherA = Guid.NewGuid();
        var teacherB = Guid.NewGuid();

        var problem = CreateProblem(requirements: new List<LessonRequirement>
        {
            new()
            {
                ClassId = classId, ClassName = "七年级1班",
                TeacherId = teacherA, TeacherName = "张老师",
                Subject = "语文", WeeklyCount = 6
            },
            new()
            {
                ClassId = classId, ClassName = "七年级1班",
                TeacherId = teacherB, TeacherName = "李老师",
                Subject = "数学", WeeklyCount = 6
            }
        });

        var result = _solver.Solve(problem);
        Assert.Equal(12, result.Entries.Count);

        var byDay = result.Entries.GroupBy(e => e.DayIndex);
        foreach (var day in byDay)
        {
            var byPeriod = day.GroupBy(e => e.PeriodIndex);
            foreach (var period in byPeriod)
            {
                var byClass = period.GroupBy(e => e.ClassId);
                foreach (var cls in byClass)
                    Assert.Single(cls);
                var byTeacher = period.GroupBy(e => e.TeacherId);
                foreach (var teacher in byTeacher)
                    Assert.Single(teacher);
            }
        }
    }

    [Fact]
    public void Solve_WithCancellation_Throws()
    {
        var requirements = new List<LessonRequirement>();
        for (int i = 0; i < 100; i++)
        {
            requirements.Add(new LessonRequirement
            {
                ClassId = Guid.NewGuid(), ClassName = $"班级{i}",
                TeacherId = Guid.NewGuid(), TeacherName = $"教师{i}",
                Subject = "语文", WeeklyCount = 1
            });
        }

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.Throws<OperationCanceledException>(() =>
            _solver.Solve(CreateProblem(requirements: requirements), ct: cts.Token));
    }
}