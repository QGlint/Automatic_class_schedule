using Automatic_class_schedule.Infrastructure;
using Automatic_class_schedule.Models;
using Automatic_class_schedule.Services;

namespace Automatic_class_schedule.Tests;

/// <summary>使用真实项目135数据诊断局部调整是否能解决冲突</summary>
[Trait("Category", "LocalOnly")]
public class Project135DiagnosticTests
{
    private static readonly string Project135Path =
        @"C:\Users\EGlint\Documents\ACS\Projects\135_test\135.acsproj";

    [Fact]
    public void LoadProject135_DetectConflicts_RunLocalAdjust()
    {
        // 1. 加载项目
        Assert.True(File.Exists(Project135Path), $"项目文件不存在: {Project135Path}");
        var data = SchoolDataSerializer.DeserializeFromDirectory(Project135Path);
        Assert.NotNull(data);
        Assert.True(data.ScheduleEntries.Count > 0, "课表为空");

        Console.WriteLine($"=== 项目135诊断 ===");
        Console.WriteLine($"课程条目数: {data.ScheduleEntries.Count}");
        Console.WriteLine($"班级数: {data.Classes.Count}");
        Console.WriteLine($"教师数: {data.Teachers.Count}");
        Console.WriteLine($"需求数: {data.Requirements.Count}");

        // 2. 检测教师时间槽冲突（体育2连班除外）
        var conflictGroups = data.ScheduleEntries
            .Where(e => e.TeacherId != Guid.Empty && !e.IsFixed)
            .GroupBy(e => (e.TeacherName, e.DayIndex, e.PeriodIndex))
            .Where(g => g.Select(e => e.ClassId).Distinct().Count() > 1)
            .Where(g => !(g.All(e => e.Subject == "体育") && g.Count() == 2
                && AreAdjacent(g.First().ClassName, g.Last().ClassName)))
            .ToList();

        Console.WriteLine($"\n冲突数: {conflictGroups.Count}");
        foreach (var g in conflictGroups.Take(10))
        {
            var classes = string.Join(", ", g.Select(e => $"{e.ClassName}({e.Subject})").Distinct());
            Console.WriteLine($"  {g.Key.TeacherName} 周{g.Key.DayIndex + 1} 第{g.Key.PeriodIndex}节 → {classes}");
        }

        if (conflictGroups.Count == 0)
        {
            Console.WriteLine("无冲突，无需调整");
            return;
        }

        // 3. 模拟局部调整第3轮（冲突班级全部课程解锁，relaxLevel=1）
        var conflictTeacherNames = conflictGroups.Select(g => g.Key.TeacherName).ToHashSet();
        var conflictClassIds = conflictGroups.SelectMany(g => g.Select(e => e.ClassId)).ToHashSet();
        var allEntries = data.ScheduleEntries.ToList();

        // 第3轮：解锁冲突班级全部课程
        var unlockIds = allEntries
            .Where(e => !e.IsFixed && e.RequirementId != Guid.Empty && conflictClassIds.Contains(e.ClassId))
            .Select(e => e.Id).ToHashSet();

        var locks = allEntries
            .Where(e => !e.IsFixed && e.RequirementId != Guid.Empty && !unlockIds.Contains(e.Id))
            .Select(e => new LockedLesson
            {
                RequirementId = e.RequirementId,
                EntryId = e.Id,
                DayIndex = e.DayIndex,
                PeriodIndex = e.PeriodIndex
            }).ToList();

        Console.WriteLine($"\n第3轮：解锁 {unlockIds.Count} 条，锁定 {locks.Count} 条");

        var service = new ScheduleService();
        var result = service.GenerateWithLocks(data, locks, null, CancellationToken.None, relaxLevel: 1);

        Console.WriteLine($"求解结果条目数: {result.Entries.Count} (期望 >= {allEntries.Count})");
        Console.WriteLine($"求解冲突信息: {result.Conflicts.Count}");
        foreach (var c in result.Conflicts.Take(5))
            Console.WriteLine($"  [{c.Severity}] {c.Message}");

        // 4. 检查结果中的冲突
        int resultConflicts = result.Entries
            .Where(e => e.TeacherId != Guid.Empty && !e.IsFixed)
            .GroupBy(e => (e.TeacherName, e.DayIndex, e.PeriodIndex))
            .Where(g => g.Select(e => e.ClassId).Distinct().Count() > 1)
            .Count(g => !(g.All(e => e.Subject == "体育") && g.Count() == 2
                && AreAdjacent(g.First().ClassName, g.Last().ClassName)));

        Console.WriteLine($"\n求解后剩余冲突: {resultConflicts}");

        // 5. 如果第3轮失败，尝试第4轮（全部解锁）
        if (resultConflicts > 0 || result.Entries.Count < allEntries.Count)
        {
            Console.WriteLine("\n--- 尝试第4轮：全部解锁 ---");
            var unlockAll = allEntries
                .Where(e => !e.IsFixed && e.RequirementId != Guid.Empty)
                .Select(e => e.Id).ToHashSet();
            var locks4 = allEntries
                .Where(e => !e.IsFixed && e.RequirementId != Guid.Empty && !unlockAll.Contains(e.Id))
                .Select(e => new LockedLesson
                {
                    RequirementId = e.RequirementId,
                    EntryId = e.Id,
                    DayIndex = e.DayIndex,
                    PeriodIndex = e.PeriodIndex
                }).ToList();

            Console.WriteLine($"第4轮：解锁 {unlockAll.Count} 条，锁定 {locks4.Count} 条");
            var result4 = service.GenerateWithLocks(data, locks4, null, CancellationToken.None, relaxLevel: 1);
            Console.WriteLine($"求解结果条目数: {result4.Entries.Count}");

            int conflicts4 = result4.Entries
                .Where(e => e.TeacherId != Guid.Empty && !e.IsFixed)
                .GroupBy(e => (e.TeacherName, e.DayIndex, e.PeriodIndex))
                .Where(g => g.Select(e => e.ClassId).Distinct().Count() > 1)
                .Count(g => !(g.All(e => e.Subject == "体育") && g.Count() == 2
                    && AreAdjacent(g.First().ClassName, g.Last().ClassName)));
            Console.WriteLine($"第4轮后剩余冲突: {conflicts4}");

            Assert.True(conflicts4 == 0, $"全部解锁后仍有 {conflicts4} 个冲突");
        }
        else
        {
            Assert.True(resultConflicts == 0, $"第3轮后仍有 {resultConflicts} 个冲突");
        }
    }

    private static bool AreAdjacent(string classA, string classB)
    {
        var (gA, nA) = Parse(classA);
        var (gB, nB) = Parse(classB);
        return gA == gB && nA >= 0 && nB >= 0 && Math.Abs(nA - nB) == 1;
    }

    private static (string, int) Parse(string className)
    {
        if (string.IsNullOrEmpty(className)) return ("", -1);
        string grade = new string(className.Where(char.IsLetter).Take(1).ToArray());
        var numStr = new string(className.Where(char.IsDigit).ToArray());
        return int.TryParse(numStr, out int n) ? (grade, n) : (grade, -1);
    }
}
