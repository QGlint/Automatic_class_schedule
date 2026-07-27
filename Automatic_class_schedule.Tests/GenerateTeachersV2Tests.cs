using Automatic_class_schedule.Models;
using Automatic_class_schedule.ViewModels;
using Xunit.Abstractions;

namespace Automatic_class_schedule.Tests;

/// <summary>
/// 生成教师V2配置逻辑测试：
/// 1. 默认模式识别（按班/按年级）
/// 2. 默认数值配置
/// 3. "按班"模式：数值=每位教师所带班数 → 自动算教师数
/// 4. "按年级"模式：数值=该年级该科教师数
/// 5. 多年级独立配置
/// 6. 所有班级均被覆盖
/// </summary>
public sealed class GenerateTeachersV2Tests
{
    private readonly ITestOutputHelper _output;

    public GenerateTeachersV2Tests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static List<SchoolClass> CreateClasses(int count, string gradeName)
    {
        string shortGrade = gradeName.Replace("年级", "");
        var classes = new List<SchoolClass>();
        for (int i = 1; i <= count; i++)
            classes.Add(new SchoolClass { GradeName = gradeName, ClassNumber = i, Name = $"{shortGrade}{i}班" });
        return classes;
    }

    private static List<SubjectDefinition> CreateSubjects()
    {
        return new List<SubjectDefinition>
        {
            new() { Name = "语文", Category = "主科", DefaultWeeklyCount = 6 },
            new() { Name = "数学", Category = "主科", DefaultWeeklyCount = 6 },
            new() { Name = "英语", Category = "主科", DefaultWeeklyCount = 5 },
            new() { Name = "物理", Category = "理科", DefaultWeeklyCount = 3 },
            new() { Name = "化学", Category = "理科", DefaultWeeklyCount = 3 },
            new() { Name = "体育", Category = "副科", DefaultWeeklyCount = 3 },
            new() { Name = "音乐", Category = "副科", DefaultWeeklyCount = 1 },
        };
    }

    // ===== IsDefaultByGrade 测试 =====

    [Theory]
    [InlineData("物理", true)]
    [InlineData("化学", true)]
    [InlineData("地理", true)]
    [InlineData("生物", true)]
    [InlineData("历史", true)]
    [InlineData("道德", true)]
    [InlineData("音乐", true)]
    [InlineData("美术", true)]
    [InlineData("信息", true)]
    [InlineData("劳动", true)]
    [InlineData("体育", true)]
    [InlineData("语文", false)]
    [InlineData("数学", false)]
    [InlineData("英语", false)]
    public void IsDefaultByGrade_CorrectMode(string subject, bool expectedByGrade)
    {
        Assert.Equal(expectedByGrade, MainViewModel.IsDefaultByGrade(subject));
    }

    // ===== GetDefaultTeacherConfig 测试 =====

    [Theory]
    [InlineData("语文", true, 2)]   // 按班模式：每人带2班
    [InlineData("数学", true, 2)]
    [InlineData("英语", true, 2)]
    [InlineData("物理", true, 3)]
    [InlineData("化学", true, 3)]
    [InlineData("地理", true, 4)]
    [InlineData("音乐", true, 8)]
    [InlineData("体育", true, 4)]
    [InlineData("语文", false, 4)]  // 按年级模式：4位教师
    [InlineData("物理", false, 3)]
    [InlineData("地理", false, 2)]
    [InlineData("音乐", false, 1)]
    [InlineData("体育", false, 6)]
    public void GetDefaultTeacherConfig_ReturnsExpectedValue(string subject, bool byClass, int expected)
    {
        int result = MainViewModel.GetDefaultTeacherConfig(subject, byClass);
        Assert.Equal(expected, result);
        _output.WriteLine($"{subject} 模式={(byClass ? "按班" : "按年级")} → 默认值={result}");
    }

    // ===== GenerateTeachersWithConfigV2 "按班"模式 =====

    [Fact]
    public void ByClass_Mode_8Classes_2PerTeacher_Generates4Teachers()
    {
        var classes = CreateClasses(8, "七年级");
        var subjects = CreateSubjects().Where(s => s.Name == "语文").ToList();
        var configMap = new Dictionary<string, (int Value, bool ByGrade)>
        {
            ["七年级|语文"] = (2, false) // 按班，每人带2班
        };

        var assignments = new List<TeacherAssignment>();
        MainViewModel.GenerateTeachersWithConfigV2(assignments, subjects, classes, configMap);

        Assert.Equal(4, assignments.Count); // 8/2=4位
        foreach (var a in assignments)
        {
            int classCount = a.ClassNames.Split(',').Length;
            Assert.Equal(2, classCount);
            _output.WriteLine($"{a.TeacherName}: {a.ClassNames} ({classCount}班)");
        }
    }

    [Fact]
    public void ByClass_Mode_7Classes_3PerTeacher_Generates3Teachers()
    {
        var classes = CreateClasses(7, "八年级");
        var subjects = CreateSubjects().Where(s => s.Name == "物理").ToList();
        var configMap = new Dictionary<string, (int Value, bool ByGrade)>
        {
            ["八年级|物理"] = (3, false) // 按班，每人带3班
        };

        var assignments = new List<TeacherAssignment>();
        MainViewModel.GenerateTeachersWithConfigV2(assignments, subjects, classes, configMap);

        // ceil(7/3)=3位教师
        Assert.Equal(3, assignments.Count);
        // 前两位各3班，最后一位1班
        int totalClasses = assignments.Sum(a => a.ClassNames.Split(',').Length);
        Assert.Equal(7, totalClasses);
        _output.WriteLine($"物理: {assignments.Count}位教师, 共覆盖{totalClasses}班");
        foreach (var a in assignments)
            _output.WriteLine($"  {a.TeacherName}: {a.ClassNames}");
    }

    // ===== GenerateTeachersWithConfigV2 "按年级"模式 =====

    [Fact]
    public void ByGrade_Mode_DirectTeacherCount()
    {
        var classes = CreateClasses(8, "七年级");
        var subjects = CreateSubjects().Where(s => s.Name == "物理").ToList();
        var configMap = new Dictionary<string, (int Value, bool ByGrade)>
        {
            ["七年级|物理"] = (3, true) // 按年级，3位教师
        };

        var assignments = new List<TeacherAssignment>();
        MainViewModel.GenerateTeachersWithConfigV2(assignments, subjects, classes, configMap);

        Assert.Equal(3, assignments.Count);
        // 8班/3人 → ceil=3, 前2人各3班, 第3人2班
        int totalClasses = assignments.Sum(a => a.ClassNames.Split(',').Length);
        Assert.Equal(8, totalClasses);
        _output.WriteLine($"物理按年级3人: 共覆盖{totalClasses}班");
        foreach (var a in assignments)
            _output.WriteLine($"  {a.TeacherName}: {a.ClassNames}");
    }

    [Fact]
    public void ByGrade_Mode_1Teacher_CoversAllClasses()
    {
        var classes = CreateClasses(6, "九年级");
        var subjects = CreateSubjects().Where(s => s.Name == "音乐").ToList();
        var configMap = new Dictionary<string, (int Value, bool ByGrade)>
        {
            ["九年级|音乐"] = (1, true) // 1位教师教全年级
        };

        var assignments = new List<TeacherAssignment>();
        MainViewModel.GenerateTeachersWithConfigV2(assignments, subjects, classes, configMap);

        Assert.Single(assignments);
        Assert.Equal(6, assignments[0].ClassNames.Split(',').Length);
        _output.WriteLine($"音乐: {assignments[0].TeacherName} → {assignments[0].ClassNames}");
    }

    // ===== 多年级独立配置 =====

    [Fact]
    public void MultiGrade_IndependentConfig()
    {
        var classes = new List<SchoolClass>();
        classes.AddRange(CreateClasses(8, "七年级"));
        classes.AddRange(CreateClasses(6, "八年级"));

        var subjects = CreateSubjects().Where(s => s.Name == "语文").ToList();
        var configMap = new Dictionary<string, (int Value, bool ByGrade)>
        {
            ["七年级|语文"] = (2, false), // 按班，每人2班 → 4位
            ["八年级|语文"] = (3, true),  // 按年级，3位教师
        };

        var assignments = new List<TeacherAssignment>();
        MainViewModel.GenerateTeachersWithConfigV2(assignments, subjects, classes, configMap);

        var grade7 = assignments.Where(a => a.GradeName == "七年级").ToList();
        var grade8 = assignments.Where(a => a.GradeName == "八年级").ToList();

        Assert.Equal(4, grade7.Count); // 8/2=4
        Assert.Equal(3, grade8.Count); // 直接3位

        _output.WriteLine($"七年级语文: {grade7.Count}位教师");
        _output.WriteLine($"八年级语文: {grade8.Count}位教师");
    }

    // ===== 所有班级覆盖验证 =====

    [Fact]
    public void AllClasses_Covered_NoDuplicate()
    {
        var classes = new List<SchoolClass>();
        classes.AddRange(CreateClasses(8, "七年级"));
        classes.AddRange(CreateClasses(8, "八年级"));
        classes.AddRange(CreateClasses(6, "九年级"));

        var subjects = CreateSubjects();
        var configMap = new Dictionary<string, (int Value, bool ByGrade)>();

        // 为每个年级每个科目配置
        foreach (string grade in new[] { "七年级", "八年级", "九年级" })
        {
            configMap[$"{grade}|语文"] = (2, false);
            configMap[$"{grade}|数学"] = (2, false);
            configMap[$"{grade}|英语"] = (2, false);
            configMap[$"{grade}|物理"] = (3, true);
            configMap[$"{grade}|化学"] = (3, true);
            configMap[$"{grade}|体育"] = (2, true);
            configMap[$"{grade}|音乐"] = (1, true);
        }

        var assignments = new List<TeacherAssignment>();
        MainViewModel.GenerateTeachersWithConfigV2(assignments, subjects, classes, configMap);

        // 验证每个年级每个科目所有班级都被覆盖且无重复
        foreach (string grade in new[] { "七年级", "八年级", "九年级" })
        {
            int expectedClassCount = grade == "九年级" ? 6 : 8;
            foreach (var subj in subjects)
            {
                var gradeAssignments = assignments.Where(a => a.GradeName == grade && a.Subject == subj.Name).ToList();
                var allClassNames = gradeAssignments.SelectMany(a => a.ClassNames.Split(',')).ToList();

                Assert.Equal(expectedClassCount, allClassNames.Count);
                Assert.Equal(allClassNames.Count, allClassNames.Distinct().Count());
                _output.WriteLine($"{grade} {subj.Name}: {gradeAssignments.Count}位教师, {allClassNames.Count}班全覆盖 ✓");
            }
        }
    }

    // ===== 教师命名规范（七数一 格式） =====

    [Fact]
    public void TeacherNaming_FollowsConvention()
    {
        var classes = CreateClasses(4, "七年级");
        var subjects = CreateSubjects().Where(s => s.Name == "数学").ToList();
        var configMap = new Dictionary<string, (int Value, bool ByGrade)>
        {
            ["七年级|数学"] = (2, false)
        };

        var assignments = new List<TeacherAssignment>();
        MainViewModel.GenerateTeachersWithConfigV2(assignments, subjects, classes, configMap);

        Assert.Equal(2, assignments.Count);
        Assert.Equal("七数一", assignments[0].TeacherName);
        Assert.Equal("七数二", assignments[1].TeacherName);
        Assert.All(assignments, a => Assert.Equal("七年级", a.GradeName));
        _output.WriteLine($"命名: {string.Join(", ", assignments.Select(a => a.TeacherName))} ✓");
    }

    [Fact]
    public void TeacherNaming_MultiSubject_CorrectAbbreviation()
    {
        var classes = CreateClasses(6, "八年级");
        var subjects = CreateSubjects().Where(s => s.Name is "语文" or "物理" or "体育").ToList();
        var configMap = new Dictionary<string, (int Value, bool ByGrade)>
        {
            ["八年级|语文"] = (3, false),  // 2位
            ["八年级|物理"] = (2, true),   // 2位
            ["八年级|体育"] = (3, true),   // 3位
        };

        var assignments = new List<TeacherAssignment>();
        MainViewModel.GenerateTeachersWithConfigV2(assignments, subjects, classes, configMap);

        var chinese = assignments.Where(a => a.Subject == "语文").ToList();
        var physics = assignments.Where(a => a.Subject == "物理").ToList();
        var pe = assignments.Where(a => a.Subject == "体育").ToList();

        Assert.Equal("八语一", chinese[0].TeacherName);
        Assert.Equal("八语二", chinese[1].TeacherName);
        Assert.Equal("八物一", physics[0].TeacherName);
        Assert.Equal("八物二", physics[1].TeacherName);
        Assert.Equal("八体一", pe[0].TeacherName);
        Assert.Equal("八体二", pe[1].TeacherName);
        Assert.Equal("八体三", pe[2].TeacherName);
        _output.WriteLine($"多科目命名: {string.Join(", ", assignments.Select(a => a.TeacherName))} ✓");
    }

    [Fact]
    public void ToChineseNumeral_CorrectConversion()
    {
        Assert.Equal("一", MainViewModel.ToChineseNumeral(1));
        Assert.Equal("二", MainViewModel.ToChineseNumeral(2));
        Assert.Equal("九", MainViewModel.ToChineseNumeral(9));
        Assert.Equal("十", MainViewModel.ToChineseNumeral(10));
        Assert.Equal("十一", MainViewModel.ToChineseNumeral(11));
        Assert.Equal("二十", MainViewModel.ToChineseNumeral(20));
        Assert.Equal("二十三", MainViewModel.ToChineseNumeral(23));
        _output.WriteLine("中文数字转换 ✓");
    }

    // ===== 修改配置后结果变化 =====

    [Fact]
    public void ModifiedConfig_ChangesOutput()
    {
        var classes = CreateClasses(8, "七年级");
        var subjects = CreateSubjects().Where(s => s.Name == "语文").ToList();

        // 原始配置：每人带2班 → 4位教师
        var config1 = new Dictionary<string, (int Value, bool ByGrade)> { ["七年级|语文"] = (2, false) };
        var result1 = new List<TeacherAssignment>();
        MainViewModel.GenerateTeachersWithConfigV2(result1, subjects, classes, config1);
        Assert.Equal(4, result1.Count);

        // 修改为每人带4班 → 2位教师
        var config2 = new Dictionary<string, (int Value, bool ByGrade)> { ["七年级|语文"] = (4, false) };
        var result2 = new List<TeacherAssignment>();
        MainViewModel.GenerateTeachersWithConfigV2(result2, subjects, classes, config2);
        Assert.Equal(2, result2.Count);
        Assert.All(result2, a => Assert.Equal(4, a.ClassNames.Split(',').Length));

        // 修改为按年级模式 4位教师（8班/4人=每人2班）
        var config3 = new Dictionary<string, (int Value, bool ByGrade)> { ["七年级|语文"] = (4, true) };
        var result3 = new List<TeacherAssignment>();
        MainViewModel.GenerateTeachersWithConfigV2(result3, subjects, classes, config3);
        Assert.Equal(4, result3.Count);
        Assert.All(result3, a => Assert.Equal(2, a.ClassNames.Split(',').Length));

        _output.WriteLine($"修改前: {result1.Count}位 → 改班数后: {result2.Count}位 → 改模式后: {result3.Count}位 ✓");
    }

    [Fact]
    public void ModifiedConfig_GradeOverride_Works()
    {
        var classes = new List<SchoolClass>();
        classes.AddRange(CreateClasses(8, "七年级"));
        classes.AddRange(CreateClasses(8, "八年级"));
        var subjects = CreateSubjects().Where(s => s.Name == "数学").ToList();

        // 七年级按班模式每人2班，八年级按年级模式4人
        var config = new Dictionary<string, (int Value, bool ByGrade)>
        {
            ["七年级|数学"] = (2, false),
            ["八年级|数学"] = (4, true),
        };

        var assignments = new List<TeacherAssignment>();
        MainViewModel.GenerateTeachersWithConfigV2(assignments, subjects, classes, config);

        var g7 = assignments.Where(a => a.GradeName == "七年级").ToList();
        var g8 = assignments.Where(a => a.GradeName == "八年级").ToList();

        Assert.Equal(4, g7.Count); // 8/2=4
        Assert.Equal(4, g8.Count); // 直接4人
        // 八年级每人带2班 (8/4=2)
        Assert.All(g8, a => Assert.Equal(2, a.ClassNames.Split(',').Length));

        _output.WriteLine($"七年级数学: {g7.Count}位(按班), 八年级数学: {g8.Count}位(按年级) ✓");
    }

    [Fact]
    public void MissingConfig_SkipsSubject()
    {
        var classes = CreateClasses(4, "七年级");
        var subjects = CreateSubjects();
        // 只配置语文，其他科目无配置
        var configMap = new Dictionary<string, (int Value, bool ByGrade)>
        {
            ["七年级|语文"] = (2, false)
        };

        var assignments = new List<TeacherAssignment>();
        MainViewModel.GenerateTeachersWithConfigV2(assignments, subjects, classes, configMap);

        // 只有语文生成了教师
        Assert.All(assignments, a => Assert.Equal("语文", a.Subject));
        Assert.Equal(2, assignments.Count);
        _output.WriteLine($"无配置科目被跳过，仅生成语文{assignments.Count}位 ✓");
    }

    // ===== 空班级列表 =====

    [Fact]
    public void EmptyClasses_NoAssignments()
    {
        var subjects = CreateSubjects();
        var configMap = new Dictionary<string, (int Value, bool ByGrade)>
        {
            ["七年级|语文"] = (2, false)
        };

        var assignments = new List<TeacherAssignment>();
        MainViewModel.GenerateTeachersWithConfigV2(assignments, subjects, new List<SchoolClass>(), configMap);

        Assert.Empty(assignments);
    }
}
