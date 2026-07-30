using Automatic_class_schedule.Models;
using Automatic_class_schedule.Services;
using Automatic_class_schedule.Solver;
using Xunit.Abstractions;

namespace Automatic_class_schedule.Tests;

/// <summary>
/// 验证新需求的测试集：
/// 1. 教师分配规则（语数英2班/物化3/地生历道2/音美信劳1/体育6全校）
/// 2. 体育第4节及之后才可排
/// 3. 第3节允许文科/理科/信息
/// 4. 冲突模型中文化+具体对象
/// 5. 体育合班不报冲突
/// </summary>
public sealed class TeacherAssignmentRuleTests
{
    private readonly ITestOutputHelper _output;
    private readonly ScheduleService _service = new();

    public TeacherAssignmentRuleTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static List<SubjectDefinition> CreateFullSubjects()
    {
        return new List<SubjectDefinition>
        {
            new() { Name = "语文", Category = "主科", DefaultWeeklyCount = 6, DistributionRule = "每天一次" },
            new() { Name = "数学", Category = "主科", DefaultWeeklyCount = 6, DistributionRule = "每天一次" },
            new() { Name = "英语", Category = "主科", DefaultWeeklyCount = 5, DistributionRule = "每天一次" },
            new() { Name = "物理", Category = "理科", DefaultWeeklyCount = 3, DistributionRule = "均衡分布" },
            new() { Name = "化学", Category = "理科", DefaultWeeklyCount = 3, DistributionRule = "均衡分布" },
            new() { Name = "生物", Category = "理科", DefaultWeeklyCount = 2, DistributionRule = "均衡分布" },
            new() { Name = "历史", Category = "文科", DefaultWeeklyCount = 2, DistributionRule = "均衡分布" },
            new() { Name = "地理", Category = "文科", DefaultWeeklyCount = 2, DistributionRule = "均衡分布" },
            new() { Name = "道德", Category = "文科", DefaultWeeklyCount = 2, DistributionRule = "均衡分布" },
            new() { Name = "体育", Category = "副科", DefaultWeeklyCount = 3, DistributionRule = "均衡分布" },
            new() { Name = "音乐", Category = "副科", DefaultWeeklyCount = 1, DistributionRule = "均衡分布" },
            new() { Name = "美术", Category = "副科", DefaultWeeklyCount = 1, DistributionRule = "均衡分布" },
            new() { Name = "信息", Category = "副科", DefaultWeeklyCount = 1, DistributionRule = "均衡分布" },
            new() { Name = "劳动", Category = "副科", DefaultWeeklyCount = 1, DistributionRule = "均衡分布" }
        };
    }

    private static List<SchoolClass> CreateClasses(int count, string gradeName)
    {
        string shortGrade = gradeName.Replace("年级", "");
        var classes = new List<SchoolClass>();
        for (int i = 1; i <= count; i++)
            classes.Add(new SchoolClass { GradeName = gradeName, ClassNumber = i, Name = $"{shortGrade}{i}班" });
        return classes;
    }

    [Fact]
    public void MainSubject_EachTeacherHandles2Classes()
    {
        var classes = CreateClasses(8, "七年级");
        var subjects = CreateFullSubjects();
        var assignments = new List<TeacherAssignment>();

        _service.GenerateAssignments(assignments, subjects, classes);

        // 语文：8班/2=4位老师
        var chineseTeachers = assignments.Where(a => a.Subject == "语文").ToList();
        Assert.Equal(4, chineseTeachers.Count);
        foreach (var t in chineseTeachers)
        {
            int classCount = t.ClassNames.Split('、').Length;
            Assert.True(classCount <= 2, $"{t.TeacherName} 带了{classCount}个班，应≤2");
        }

        // 数学同理
        var mathTeachers = assignments.Where(a => a.Subject == "数学").ToList();
        Assert.Equal(4, mathTeachers.Count);

        // 英语同理
        var engTeachers = assignments.Where(a => a.Subject == "英语").ToList();
        Assert.Equal(4, engTeachers.Count);

        _output.WriteLine($"语文教师: {chineseTeachers.Count}位, 每人带班: {string.Join(",", chineseTeachers.Select(t => t.ClassNames.Split('、').Length))}");
    }

    [Fact]
    public void Science_3TeachersPerGrade()
    {
        var classes = CreateClasses(8, "八年级");
        var subjects = CreateFullSubjects();
        var assignments = new List<TeacherAssignment>();

        _service.GenerateAssignments(assignments, subjects, classes);

        var physicsTeachers = assignments.Where(a => a.Subject == "物理").ToList();
        var chemTeachers = assignments.Where(a => a.Subject == "化学").ToList();

        Assert.Equal(3, physicsTeachers.Count);
        Assert.Equal(3, chemTeachers.Count);

        _output.WriteLine($"物理教师: {physicsTeachers.Count}位");
        foreach (var t in physicsTeachers)
            _output.WriteLine($"  {t.TeacherName}: {t.ClassNames}");
    }

    [Fact]
    public void MinorSubjects_2TeachersPerGrade()
    {
        var classes = CreateClasses(8, "七年级");
        var subjects = CreateFullSubjects();
        var assignments = new List<TeacherAssignment>();

        _service.GenerateAssignments(assignments, subjects, classes);

        foreach (string subj in new[] { "地理", "生物", "历史", "道德" })
        {
            var teachers = assignments.Where(a => a.Subject == subj).ToList();
            Assert.Equal(2, teachers.Count);
            _output.WriteLine($"{subj}教师: {teachers.Count}位");
        }
    }

    [Fact]
    public void ArtSubjects_1TeacherPerGrade()
    {
        var classes = CreateClasses(8, "七年级");
        var subjects = CreateFullSubjects();
        var assignments = new List<TeacherAssignment>();

        _service.GenerateAssignments(assignments, subjects, classes);

        foreach (string subj in new[] { "音乐", "美术", "信息", "劳动" })
        {
            var teachers = assignments.Where(a => a.Subject == subj).ToList();
            Assert.Single(teachers);
            _output.WriteLine($"{subj}教师: {teachers[0].TeacherName} → {teachers[0].ClassNames}");
        }
    }

    [Fact]
    public void PE_6TeachersWholeSchool_EvenDistribution()
    {
        var classes = new List<SchoolClass>();
        classes.AddRange(CreateClasses(8, "七年级"));
        classes.AddRange(CreateClasses(8, "八年级"));
        classes.AddRange(CreateClasses(6, "九年级"));
        var subjects = CreateFullSubjects();
        var assignments = new List<TeacherAssignment>();

        _service.GenerateAssignments(assignments, subjects, classes);

        var peTeachers = assignments.Where(a => a.Subject == "体育").ToList();
        Assert.Equal(6, peTeachers.Count);

        // 验证平均分配（23班/6人=每人3-4班）
        int totalAssigned = 0;
        foreach (var t in peTeachers)
        {
            int count = t.ClassNames.Split('、').Length;
            Assert.InRange(count, 3, 4);
            totalAssigned += count;
            _output.WriteLine($"{t.TeacherName}: {count}个班 → {t.ClassNames}");
        }
        Assert.Equal(22, totalAssigned); // 所有班级都被分配(8+8+6=22)
        _output.WriteLine($"体育教师共{peTeachers.Count}位，共分配{totalAssigned}个班");
    }

    [Fact]
    public void PE_NotPerGrade_SchoolWide()
    {
        var classes = new List<SchoolClass>();
        classes.AddRange(CreateClasses(8, "七年级"));
        classes.AddRange(CreateClasses(8, "八年级"));
        var subjects = CreateFullSubjects();
        var assignments = new List<TeacherAssignment>();

        _service.GenerateAssignments(assignments, subjects, classes);

        var peTeachers = assignments.Where(a => a.Subject == "体育").ToList();
        // 体育老师应该跨年级
        Assert.All(peTeachers, t => Assert.Equal("全校", t.GradeName));
        // 至少有一位体育老师同时教七和八年级
        bool crossGrade = peTeachers.Any(t =>
            t.ClassNames.Contains("七") && t.ClassNames.Contains("八"));
        Assert.True(crossGrade, "应有体育老师跨年级分配");
        _output.WriteLine("体育教师跨年级分配验证通过");
    }
}

public sealed class SolverConstraintTests
{
    private readonly ITestOutputHelper _output;

    public SolverConstraintTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static SchoolData CreateTestData()
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
        data.Classes.AddRange(new[]
        {
            new SchoolClass { GradeName = "七年级", ClassNumber = 1, Name = "七1班" },
            new SchoolClass { GradeName = "七年级", ClassNumber = 2, Name = "七2班" }
        });
        data.Subjects.AddRange(new[]
        {
            new SubjectDefinition { Name = "语文", Category = "主科", DefaultWeeklyCount = 6 },
            new SubjectDefinition { Name = "数学", Category = "主科", DefaultWeeklyCount = 6 },
            new SubjectDefinition { Name = "英语", Category = "主科", DefaultWeeklyCount = 5 },
            new SubjectDefinition { Name = "物理", Category = "理科", DefaultWeeklyCount = 3 },
            new SubjectDefinition { Name = "体育", Category = "副科", DefaultWeeklyCount = 3 },
            new SubjectDefinition { Name = "音乐", Category = "副科", DefaultWeeklyCount = 1 },
            new SubjectDefinition { Name = "历史", Category = "文科", DefaultWeeklyCount = 2 }
        });

        // 手动创建需求
        var cls1 = data.Classes[0];
        var cls2 = data.Classes[1];
        AddReq(data, cls1, "语文", "语老师", 6);
        AddReq(data, cls1, "数学", "数老师", 6);
        AddReq(data, cls1, "英语", "英老师", 5);
        AddReq(data, cls1, "物理", "物老师", 3);
        AddReq(data, cls1, "体育", "体老师", 3);
        AddReq(data, cls1, "音乐", "音老师", 1);
        AddReq(data, cls1, "历史", "历老师", 2);
        AddReq(data, cls2, "语文", "语老师", 6);
        AddReq(data, cls2, "数学", "数老师", 6);
        AddReq(data, cls2, "英语", "英老师", 5);
        AddReq(data, cls2, "物理", "物老师", 3);
        AddReq(data, cls2, "体育", "体老师", 3);
        AddReq(data, cls2, "音乐", "音老师", 1);
        AddReq(data, cls2, "历史", "历老师", 2);

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

    [Fact]
    public void PE_OnlyPeriod4OrLater()
    {
        var data = CreateTestData();
        var solver = new CpSatScheduleSolver();
        var result = solver.Solve(new ScheduleProblem
        {
            Settings = data.Settings,
            Classes = data.Classes,
            Requirements = data.Requirements,
            FixedLessons = new List<FixedLesson>()
        });

        var peEntries = result.Entries.Where(e => e.Subject == "体育").ToList();
        Assert.NotEmpty(peEntries);

        foreach (var pe in peEntries)
        {
            Assert.True(pe.PeriodIndex >= 4,
                $"体育课排在第{pe.PeriodIndex}节（{pe.ClassName} 周{pe.DayIndex + 1}），应>=4");
        }
        _output.WriteLine($"体育课共{peEntries.Count}节，全部在第4节及之后 ✓");
        foreach (var pe in peEntries)
            _output.WriteLine($"  {pe.ClassName} 周{pe.DayIndex + 1} 第{pe.PeriodIndex}节");
    }

    [Fact]
    public void MainSubject_AtMost1PerMorning()
    {
        var data = CreateTestData();
        var solver = new CpSatScheduleSolver();
        var result = solver.Solve(new ScheduleProblem
        {
            Settings = data.Settings,
            Classes = data.Classes,
            Requirements = data.Requirements,
            FixedLessons = new List<FixedLesson>()
        });

        int morning = data.Settings.MorningPeriods;
        foreach (var cls in data.Classes)
        {
            foreach (string subj in new[] { "语文", "数学", "英语" })
            {
                for (int d = 0; d < data.Settings.DaysPerWeek; d++)
                {
                    int count = result.Entries.Count(e =>
                        e.ClassName == cls.Name && e.Subject == subj &&
                        e.DayIndex == d && e.PeriodIndex <= morning);
                    Assert.True(count <= 1,
                        $"{cls.Name} {subj} 周{d + 1}上午有{count}节，应≤1");
                }
            }
        }
        _output.WriteLine("主科每天上午≤1节 验证通过 ✓");
    }

    [Fact]
    public void NoConsecutiveSameSubject()
    {
        var data = CreateTestData();
        var solver = new CpSatScheduleSolver();
        var result = solver.Solve(new ScheduleProblem
        {
            Settings = data.Settings,
            Classes = data.Classes,
            Requirements = data.Requirements,
            FixedLessons = new List<FixedLesson>()
        });

        foreach (var cls in data.Classes)
        {
            var classEntries = result.Entries.Where(e => e.ClassName == cls.Name).ToList();
            foreach (var group in classEntries.GroupBy(e => e.Subject))
            {
                for (int d = 0; d < data.Settings.DaysPerWeek; d++)
                {
                    var periods = group.Where(e => e.DayIndex == d)
                        .Select(e => e.PeriodIndex).OrderBy(p => p).ToList();
                    for (int i = 1; i < periods.Count; i++)
                    {
                        Assert.True(periods[i] != periods[i - 1] + 1,
                            $"{cls.Name} {group.Key} 周{d + 1}第{periods[i - 1]}-{periods[i]}节连排");
                    }
                }
            }
        }
        _output.WriteLine("同科目不连排 验证通过 ✓");
    }

    [Fact]
    [Trait("Category", "LocalOnly")]
    public void Period3_HasNonMainSubjects()
    {
        // 使用完整示例数据验证第3节有文/理科
        var data = SampleDataFactory.Create();
        var solver = new CpSatScheduleSolver();
        var service = new ScheduleService(solver, new ConflictService());
        var result = service.Generate(data);

        string[] period3Allowed = { "物理", "化学", "生物", "历史", "地理", "道德", "信息" };
        var firstClass = result.Entries.Where(e => !e.IsFixed).Select(e => e.ClassName).Distinct().First();

        int period3NonMain = 0;
        int period3Total = 0;
        for (int d = 0; d < data.Settings.DaysPerWeek; d++)
        {
            var p3 = result.Entries.FirstOrDefault(e =>
                e.ClassName == firstClass && e.DayIndex == d && e.PeriodIndex == 3);
            if (p3 != null)
            {
                period3Total++;
                if (period3Allowed.Contains(p3.Subject))
                    period3NonMain++;
                _output.WriteLine($"周{d + 1}第3节: {p3.Subject} {(period3Allowed.Contains(p3.Subject) ? "✓文/理科" : "")}");
            }
        }

        _output.WriteLine($"\n{firstClass} 第3节共{period3Total}节，其中文/理科/信息{period3NonMain}节");
        // 一周5天中至少有1天第3节是非主科
        Assert.True(period3NonMain >= 1, "一周内第3节应至少有1天安排文科/理科/信息");
    }
}

public sealed class ConflictModelTests
{
    private readonly ITestOutputHelper _output;

    public ConflictModelTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void SeverityText_Chinese()
    {
        var info = new ScheduleConflict { Severity = ScheduleConflictSeverity.Info };
        var warn = new ScheduleConflict { Severity = ScheduleConflictSeverity.Warning };
        var hard = new ScheduleConflict { Severity = ScheduleConflictSeverity.Hard };

        Assert.Equal("信息", info.SeverityText);
        Assert.Equal("警告", warn.SeverityText);
        Assert.Equal("错误", hard.SeverityText);
        _output.WriteLine($"级别: {info.SeverityText}/{warn.SeverityText}/{hard.SeverityText} ✓");
    }

    [Fact]
    public void TypeText_Chinese()
    {
        Assert.Equal("教师冲突", new ScheduleConflict { Type = ScheduleConflictType.TeacherConflict }.TypeText);
        Assert.Equal("班级冲突", new ScheduleConflict { Type = ScheduleConflictType.ClassConflict }.TypeText);
        Assert.Equal("固定课冲突", new ScheduleConflict { Type = ScheduleConflictType.FixedLessonConflict }.TypeText);
        Assert.Equal("未排课程", new ScheduleConflict { Type = ScheduleConflictType.UnscheduledLesson }.TypeText);
        Assert.Equal("偏好冲突", new ScheduleConflict { Type = ScheduleConflictType.PreferenceConflict }.TypeText);
        _output.WriteLine("类型中文化 ✓");
    }

    [Fact]
    public void TeacherConflict_ContainsTeacherName()
    {
        var conflictService = new ConflictService();
        var problem = new ScheduleProblem
        {
            Settings = new ScheduleSettings { DaysPerWeek = 5, PeriodsPerDay = 8, MorningPeriods = 4, AfternoonPeriods = 4 },
            Classes = new List<SchoolClass>(),
            Requirements = new List<LessonRequirement>(),
            FixedLessons = new List<FixedLesson>()
        };

        var teacherId = Guid.NewGuid();
        var entries = new List<ScheduleEntry>
        {
            new() { ClassId = Guid.NewGuid(), TeacherId = teacherId, DayIndex = 0, PeriodIndex = 1, Subject = "语文", TeacherName = "张老师", ClassName = "七1班" },
            new() { ClassId = Guid.NewGuid(), TeacherId = teacherId, DayIndex = 0, PeriodIndex = 1, Subject = "语文", TeacherName = "张老师", ClassName = "七2班" }
        };

        var conflicts = conflictService.Analyze(problem, entries);
        var teacherConflict = conflicts.FirstOrDefault(c => c.Type == ScheduleConflictType.TeacherConflict);

        Assert.NotNull(teacherConflict);
        Assert.Equal("张老师", teacherConflict.Target);
        Assert.Contains("张老师", teacherConflict.Message);
        Assert.Contains("七1班", teacherConflict.Message);
        Assert.Contains("七2班", teacherConflict.Message);
        _output.WriteLine($"Target: {teacherConflict.Target}");
        _output.WriteLine($"Message: {teacherConflict.Message} ✓");
    }

    [Fact]
    public void ClassConflict_ContainsClassName()
    {
        var conflictService = new ConflictService();
        var problem = new ScheduleProblem
        {
            Settings = new ScheduleSettings { DaysPerWeek = 5, PeriodsPerDay = 8, MorningPeriods = 4, AfternoonPeriods = 4 },
            Classes = new List<SchoolClass>(),
            Requirements = new List<LessonRequirement>(),
            FixedLessons = new List<FixedLesson>()
        };

        var classId = Guid.NewGuid();
        var entries = new List<ScheduleEntry>
        {
            new() { ClassId = classId, TeacherId = Guid.NewGuid(), DayIndex = 1, PeriodIndex = 3, Subject = "数学", TeacherName = "李老师", ClassName = "八3班" },
            new() { ClassId = classId, TeacherId = Guid.NewGuid(), DayIndex = 1, PeriodIndex = 3, Subject = "英语", TeacherName = "王老师", ClassName = "八3班" }
        };

        var conflicts = conflictService.Analyze(problem, entries);
        var classConflict = conflicts.FirstOrDefault(c => c.Type == ScheduleConflictType.ClassConflict);

        Assert.NotNull(classConflict);
        Assert.Equal("八3班", classConflict.Target);
        Assert.Contains("八3班", classConflict.Message);
        Assert.Contains("数学", classConflict.Message);
        Assert.Contains("英语", classConflict.Message);
        _output.WriteLine($"Target: {classConflict.Target}");
        _output.WriteLine($"Message: {classConflict.Message} ✓");
    }

    [Fact]
    public void PE_TeacherOverlap_NoConflict()
    {
        var conflictService = new ConflictService();
        var problem = new ScheduleProblem
        {
            Settings = new ScheduleSettings { DaysPerWeek = 5, PeriodsPerDay = 8, MorningPeriods = 4, AfternoonPeriods = 4 },
            Classes = new List<SchoolClass>(),
            Requirements = new List<LessonRequirement>(),
            FixedLessons = new List<FixedLesson>()
        };

        var peTeacherId = Guid.NewGuid();
        var entries = new List<ScheduleEntry>
        {
            new() { ClassId = Guid.NewGuid(), TeacherId = peTeacherId, DayIndex = 2, PeriodIndex = 5, Subject = "体育", TeacherName = "体育一", ClassName = "七1班" },
            new() { ClassId = Guid.NewGuid(), TeacherId = peTeacherId, DayIndex = 2, PeriodIndex = 5, Subject = "体育", TeacherName = "体育一", ClassName = "七2班" }
        };

        var conflicts = conflictService.Analyze(problem, entries);
        // 体育合班不应报冲突
        Assert.DoesNotContain(conflicts, c => c.Type == ScheduleConflictType.TeacherConflict);
        _output.WriteLine("体育合班不报冲突 ✓");
    }

    [Fact]
    [Trait("Category", "LocalOnly")]
    public void FullWorkflow_ConflictsHaveTargetAndChineseText()
    {
        var data = SampleDataFactory.Create();
        var service = new ScheduleService(new CpSatScheduleSolver(), new ConflictService());
        var result = service.Generate(data);

        foreach (var conflict in result.Conflicts)
        {
            // 所有冲突必须有中文级别和类型
            Assert.False(string.IsNullOrEmpty(conflict.SeverityText));
            Assert.False(string.IsNullOrEmpty(conflict.TypeText));
            Assert.False(string.IsNullOrEmpty(conflict.Target), $"冲突缺少Target: {conflict.Message}");
            Assert.False(string.IsNullOrEmpty(conflict.Message));

            _output.WriteLine($"[{conflict.SeverityText}] {conflict.TypeText} | {conflict.Target} | {conflict.Message}");
        }
        _output.WriteLine($"\n共{result.Conflicts.Count}条冲突信息，全部包含中文级别/类型/对象 ✓");
    }
}
