using Automatic_class_schedule.Models;
using Automatic_class_schedule.Services;
using Automatic_class_schedule.Solver;
using Automatic_class_schedule.ViewModels;
using Xunit;
using Xunit.Abstractions;

namespace Automatic_class_schedule.Tests;

/// <summary>
/// 新功能测试：体育连班限制、拖拽重排（relaxConsecutiveDays）、锁定求解、DisplayText
/// </summary>
public sealed class NewFeatureTests
{
    private readonly ITestOutputHelper _output;

    public NewFeatureTests(ITestOutputHelper output)
    {
        _output = output;
    }

    #region 辅助方法

    private static SchoolData CreatePEHeavyData(int classCount = 4)
    {
        var data = new SchoolData
        {
            Settings = new ScheduleSettings
            {
                DaysPerWeek = 5,
                PeriodsPerDay = 8,
                MorningPeriods = 4,
                AfternoonPeriods = 4
            }
        };

        for (int i = 1; i <= classCount; i++)
            data.Classes.Add(new SchoolClass { GradeName = "七年级", ClassNumber = i, Name = $"七{i}班" });

        data.Subjects.AddRange(new[]
        {
            new SubjectDefinition { Name = "语文", Category = "主科", DefaultWeeklyCount = 6 },
            new SubjectDefinition { Name = "数学", Category = "主科", DefaultWeeklyCount = 6 },
            new SubjectDefinition { Name = "英语", Category = "主科", DefaultWeeklyCount = 5 },
            new SubjectDefinition { Name = "体育", Category = "副科", DefaultWeeklyCount = 3 },
            new SubjectDefinition { Name = "音乐", Category = "副科", DefaultWeeklyCount = 1 },
            new SubjectDefinition { Name = "历史", Category = "文科", DefaultWeeklyCount = 2 }
        });

        // 同一个体育老师教所有班
        foreach (var cls in data.Classes)
        {
            AddReq(data, cls, "语文", "语老师", 6);
            AddReq(data, cls, "数学", "数老师", 6);
            AddReq(data, cls, "英语", "英老师", 5);
            AddReq(data, cls, "体育", "体老师", 3);  // 同一体育老师
            AddReq(data, cls, "音乐", "音老师", 1);
            AddReq(data, cls, "历史", "历老师", 2);
        }

        return data;
    }

    private static void AddReq(SchoolData data, SchoolClass cls, string subject, string teacher, int weekly)
    {
        data.Requirements.Add(new LessonRequirement
        {
            ClassId = cls.Id,
            ClassName = cls.Name,
            TeacherId = DeterministicGuid(teacher),
            TeacherName = teacher,
            Subject = subject,
            WeeklyCount = weekly
        });
    }

    private static Guid DeterministicGuid(string input)
    {
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(input);
        return new Guid(bytes.Concat(new byte[16 - bytes.Length]).Take(16).ToArray());
    }

    private static ScheduleProblem CreateProblem(SchoolData data)
    {
        return new ScheduleProblem
        {
            Settings = data.Settings,
            Classes = data.Classes,
            Requirements = data.Requirements,
            FixedLessons = new List<FixedLesson>()
        };
    }

    #endregion

    #region 体育连班最多2班

    [Fact]
    public void PE_Teacher_Max2ClassesPerSlot()
    {
        // 4个班共用一个体育老师，验证同一时段最多2个班
        var data = CreatePEHeavyData(4);
        var solver = new CpSatScheduleSolver();
        var result = solver.Solve(CreateProblem(data));

        Assert.NotEmpty(result.Entries);

        var peEntries = result.Entries.Where(e => e.Subject == "体育").ToList();
        _output.WriteLine($"体育课共 {peEntries.Count} 节");

        // 检查每个时间槽
        int maxConcurrent = 0;
        for (int d = 0; d < data.Settings.DaysPerWeek; d++)
        {
            for (int p = 1; p <= data.Settings.PeriodsPerDay; p++)
            {
                int concurrent = peEntries.Count(e => e.DayIndex == d && e.PeriodIndex == p);
                maxConcurrent = Math.Max(maxConcurrent, concurrent);
                if (concurrent > 0)
                    _output.WriteLine($"  周{d + 1} 第{p}节: {concurrent}个班同时上体育");
            }
        }

        Assert.True(maxConcurrent <= 2,
            $"体育教师同一时段最多2个班，实际最大并发={maxConcurrent}");
        _output.WriteLine($"\n体育教师最大同时段班级数: {maxConcurrent} ≤ 2 ✓");
    }

    [Fact]
    public void PE_Teacher_6Classes_StillMax2()
    {
        // 6个班共用一个体育老师（更极端情况）
        var data = CreatePEHeavyData(6);
        var solver = new CpSatScheduleSolver();
        var result = solver.Solve(CreateProblem(data));

        var peEntries = result.Entries.Where(e => e.Subject == "体育").ToList();
        _output.WriteLine($"6个班体育课共 {peEntries.Count} 节");

        int maxConcurrent = 0;
        for (int d = 0; d < data.Settings.DaysPerWeek; d++)
            for (int p = 1; p <= data.Settings.PeriodsPerDay; p++)
            {
                int concurrent = peEntries.Count(e => e.DayIndex == d && e.PeriodIndex == p);
                maxConcurrent = Math.Max(maxConcurrent, concurrent);
            }

        Assert.True(maxConcurrent <= 2,
            $"6班情况下体育教师同一时段最多2个班，实际={maxConcurrent}");
        _output.WriteLine($"6班最大并发: {maxConcurrent} ≤ 2 ✓");
    }

    #endregion

    #region SolveWithLocks 锁定求解

    [Fact]
    public void SolveWithLocks_LockedEntry_StaysInPlace()
    {
        var data = CreatePEHeavyData(2);
        var solver = new CpSatScheduleSolver();
        var problem = CreateProblem(data);

        // 先正常求解获取一个结果
        var firstResult = solver.Solve(problem);
        Assert.NotEmpty(firstResult.Entries);

        // 选取一个非固定条目作为锁定目标
        var target = firstResult.Entries.First(e => !e.IsFixed && e.Subject == "语文");
        int lockedDay = target.DayIndex;
        int lockedPeriod = target.PeriodIndex;

        // 锁定该条目
        var locks = new List<LockedLesson>
        {
            new LockedLesson
            {
                RequirementId = target.RequirementId,
                EntryId = target.Id,
                DayIndex = lockedDay,
                PeriodIndex = lockedPeriod
            }
        };

        // 重新求解
        var secondResult = solver.SolveWithLocks(problem, locks);
        Assert.NotEmpty(secondResult.Entries);

        // 验证锁定的条目位置不变
        var lockedEntry = secondResult.Entries.FirstOrDefault(e =>
            e.RequirementId == target.RequirementId &&
            e.DayIndex == lockedDay &&
            e.PeriodIndex == lockedPeriod);

        Assert.NotNull(lockedEntry);
        _output.WriteLine($"锁定条目: {lockedEntry.Subject} {lockedEntry.ClassName} 周{lockedDay + 1}第{lockedPeriod}节 → 位置保持 ✓");
    }

    [Fact]
    public void SolveWithLocks_MultipleLocks_AllPreserved()
    {
        var data = CreatePEHeavyData(2);
        var solver = new CpSatScheduleSolver();
        var problem = CreateProblem(data);

        var firstResult = solver.Solve(problem);

        // 锁定3个不同科目
        var targets = firstResult.Entries
            .Where(e => !e.IsFixed)
            .GroupBy(e => e.Subject)
            .Take(3)
            .Select(g => g.First())
            .ToList();

        var locks = targets.Select(t => new LockedLesson
        {
            RequirementId = t.RequirementId,
            EntryId = t.Id,
            DayIndex = t.DayIndex,
            PeriodIndex = t.PeriodIndex
        }).ToList();

        var secondResult = solver.SolveWithLocks(problem, locks);

        foreach (var t in targets)
        {
            var found = secondResult.Entries.Any(e =>
                e.RequirementId == t.RequirementId &&
                e.DayIndex == t.DayIndex &&
                e.PeriodIndex == t.PeriodIndex);
            Assert.True(found, $"锁定 {t.Subject} 周{t.DayIndex + 1}第{t.PeriodIndex}节 未保持");
            _output.WriteLine($"  {t.Subject} {t.ClassName} 周{t.DayIndex + 1}第{t.PeriodIndex}节 锁定保持 ✓");
        }
    }

    #endregion

    #region relaxConsecutiveDays 放松连天约束

    [Fact]
    public void RelaxConsecutiveDays_ProducesValidSchedule()
    {
        var data = CreatePEHeavyData(2);
        var solver = new CpSatScheduleSolver();
        var problem = CreateProblem(data);

        // 使用 relaxConsecutiveDays 求解
        var result = solver.SolveWithLocks(problem, new List<LockedLesson>(), relaxConsecutiveDays: true);

        Assert.NotEmpty(result.Entries);
        // 基本验证：每个需求都有对应课时数
        foreach (var req in data.Requirements)
        {
            int count = result.Entries.Count(e => e.RequirementId == req.Id);
            Assert.Equal(req.WeeklyCount, count);
        }
        _output.WriteLine($"relaxConsecutiveDays 求解成功，共 {result.Entries.Count} 节课 ✓");
    }

    [Fact]
    public void RelaxConsecutiveDays_WithLocks_Works()
    {
        var data = CreatePEHeavyData(2);
        var solver = new CpSatScheduleSolver();
        var problem = CreateProblem(data);

        var firstResult = solver.Solve(problem);
        var target = firstResult.Entries.First(e => !e.IsFixed);

        var locks = new List<LockedLesson>
        {
            new LockedLesson
            {
                RequirementId = target.RequirementId,
                EntryId = target.Id,
                DayIndex = target.DayIndex,
                PeriodIndex = target.PeriodIndex
            }
        };

        // 同时使用 locks + relaxConsecutiveDays
        var result = solver.SolveWithLocks(problem, locks, relaxConsecutiveDays: true);

        Assert.NotEmpty(result.Entries);

        // 锁定位置保持
        var lockedEntry = result.Entries.Any(e =>
            e.RequirementId == target.RequirementId &&
            e.DayIndex == target.DayIndex &&
            e.PeriodIndex == target.PeriodIndex);
        Assert.True(lockedEntry, "relax模式下锁定位置应保持");
        _output.WriteLine($"relax+locks 求解成功，锁定保持 ✓");
    }

    [Fact]
    public void StrictConsecutiveDays_2PerWeek_NoConsecutiveDays()
    {
        // 验证严格模式下，每周2节的科目不连天
        var data = new SchoolData
        {
            Settings = new ScheduleSettings
            {
                DaysPerWeek = 5,
                PeriodsPerDay = 8,
                MorningPeriods = 4,
                AfternoonPeriods = 4
            }
        };
        data.Classes.Add(new SchoolClass { GradeName = "七年级", ClassNumber = 1, Name = "七1班" });
        data.Subjects.AddRange(new[]
        {
            new SubjectDefinition { Name = "语文", Category = "主科", DefaultWeeklyCount = 7 },
            new SubjectDefinition { Name = "数学", Category = "主科", DefaultWeeklyCount = 7 },
            new SubjectDefinition { Name = "英语", Category = "主科", DefaultWeeklyCount = 6 },
            new SubjectDefinition { Name = "历史", Category = "文科", DefaultWeeklyCount = 2 },
            new SubjectDefinition { Name = "地理", Category = "文科", DefaultWeeklyCount = 2 },
            new SubjectDefinition { Name = "体育", Category = "副科", DefaultWeeklyCount = 3 },
            new SubjectDefinition { Name = "音乐", Category = "副科", DefaultWeeklyCount = 1 },
            new SubjectDefinition { Name = "美术", Category = "副科", DefaultWeeklyCount = 1 },
            new SubjectDefinition { Name = "道德", Category = "文科", DefaultWeeklyCount = 2 }
        });

        var cls = data.Classes[0];
        AddReq(data, cls, "语文", "语老师", 7);
        AddReq(data, cls, "数学", "数老师", 7);
        AddReq(data, cls, "英语", "英老师", 6);
        AddReq(data, cls, "历史", "历老师", 2);
        AddReq(data, cls, "地理", "地老师", 2);
        AddReq(data, cls, "体育", "体老师", 3);
        AddReq(data, cls, "音乐", "音老师", 1);
        AddReq(data, cls, "美术", "美老师", 1);
        AddReq(data, cls, "道德", "道老师", 2);

        var solver = new CpSatScheduleSolver();
        var result = solver.Solve(CreateProblem(data));

        // 检查每周2节的科目（历史、地理、道德）不连天
        string[] twoPerWeek = { "历史", "地理", "道德" };
        foreach (string subj in twoPerWeek)
        {
            var days = result.Entries
                .Where(e => e.Subject == subj && e.ClassName == "七1班")
                .Select(e => e.DayIndex)
                .Distinct()
                .OrderBy(d => d)
                .ToList();

            _output.WriteLine($"{subj}: 出现在周 {string.Join(",", days.Select(d => d + 1))}");

            for (int i = 1; i < days.Count; i++)
            {
                Assert.True(days[i] != days[i - 1] + 1,
                    $"{subj} 出现在连续两天: 周{days[i - 1] + 1}和周{days[i] + 1}");
            }
        }
        _output.WriteLine("严格模式：2节/周科目不连天 ✓");
    }

    #endregion

    #region ScheduleGridCell.DisplayText

    [Fact]
    public void DisplayText_SingleEntry_ShowsSubjectAndClass()
    {
        var cell = new ScheduleGridCell
        {
            Subject = "体育",
            ClassName = "七1班",
            Entry = new ScheduleEntry { Subject = "体育", ClassName = "七1班" }
        };
        cell.AllEntries.Add(cell.Entry);

        Assert.Equal("体育 七1班", cell.DisplayText);
    }

    [Fact]
    public void DisplayText_MultipleEntries_ShowsJoinedClasses()
    {
        var entry1 = new ScheduleEntry { Subject = "体育", ClassName = "七1班" };
        var entry2 = new ScheduleEntry { Subject = "体育", ClassName = "七2班" };

        var cell = new ScheduleGridCell
        {
            Subject = "体育",
            ClassName = "七1班",
            Entry = entry1,
            AllEntries = new List<ScheduleEntry> { entry1, entry2 }
        };

        Assert.Equal("体育 七1班+七2班", cell.DisplayText);
    }

    [Fact]
    public void DisplayText_Empty_ReturnsEmpty()
    {
        var cell = new ScheduleGridCell();
        Assert.Equal(string.Empty, cell.DisplayText);
        Assert.True(cell.IsEmpty);
    }

    #endregion

    #region 非体育教师仍然互斥

    [Fact]
    public void NonPE_Teacher_NoConcurrentClasses()
    {
        // 验证非体育教师不能同时段有多班
        var data = CreatePEHeavyData(3);
        var solver = new CpSatScheduleSolver();
        var result = solver.Solve(CreateProblem(data));

        // 检查语文教师（同一教师教多班）
        var chineseEntries = result.Entries.Where(e => e.Subject == "语文").ToList();
        for (int d = 0; d < data.Settings.DaysPerWeek; d++)
            for (int p = 1; p <= data.Settings.PeriodsPerDay; p++)
            {
                int concurrent = chineseEntries.Count(e => e.DayIndex == d && e.PeriodIndex == p);
                Assert.True(concurrent <= 1,
                    $"语文教师周{d + 1}第{p}节有{concurrent}个班同时上课，应≤1");
            }

        _output.WriteLine("非体育教师严格互斥（同时段≤1班） ✓");
    }

    #endregion
}
