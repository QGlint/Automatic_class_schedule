using Automatic_class_schedule.Models;
using Automatic_class_schedule.Services;
using Automatic_class_schedule.Solver;
using ClosedXML.Excel;
using Xunit.Abstractions;

namespace Automatic_class_schedule.Tests;

public sealed class ExcelAnalysisTests
{
    private readonly ITestOutputHelper _output;

    public ExcelAnalysisTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    [Trait("Category", "LocalOnly")]
    public void ReadOutXlsx_PrintContent()
    {
        var path = @"c:\Project_Repository\winproject\Automatic_class_schedule\out.xlsx";
        Assert.True(File.Exists(path), "out.xlsx not found");

        using var wb = new XLWorkbook(path);
        foreach (var ws in wb.Worksheets)
        {
            _output.WriteLine($"=== Sheet: {ws.Name} ===");
            var range = ws.RangeUsed();
            if (range == null) continue;
            int rowCount = range.RowCount();
            int colCount = range.ColumnCount();
            int maxRow = rowCount < 30 ? rowCount : 30;
            int maxCol = colCount; // 读取所有列
            for (int r = 1; r <= maxRow; r++)
            {
                var row = new List<string>();
                for (int c = 1; c <= maxCol; c++)
                {
                    row.Add(ws.Cell(r, c).GetString().PadRight(6));
                }
                _output.WriteLine(string.Join("|", row));
            }
            _output.WriteLine("");
        }
    }

    [Fact]
    public void CompareSolverOutput_WithHumanSchedule()
    {
        // 用示例数据生成课表
        var data = SampleDataFactory.Create();
        var solver = new CpSatScheduleSolver();
        var service = new ScheduleService(solver, new ConflictService());
        var result = service.Generate(data);

        _output.WriteLine($"=== 算法输出: {result.Entries.Count} 节课 ===");
        _output.WriteLine($"硬冲突: {result.Conflicts.Count(c => c.Severity == ScheduleConflictSeverity.Hard)}");
        _output.WriteLine($"警告: {result.Conflicts.Count(c => c.Severity == ScheduleConflictSeverity.Warning)}");
        foreach (var c in result.Conflicts.Where(c => c.Severity == ScheduleConflictSeverity.Hard).Take(5))
            _output.WriteLine($"  [硬] {c.Message}");
        foreach (var c in result.Conflicts.Where(c => c.Severity == ScheduleConflictSeverity.Warning).Take(5))
            _output.WriteLine($"  [警] {c.Message}");

        // 取第一个班级详细输出
        var firstClass = result.Entries.Where(e => !e.IsFixed).Select(e => e.ClassName).Distinct().FirstOrDefault();
        if (firstClass == null) { _output.WriteLine("无排课结果"); return; }

        _output.WriteLine($"\n=== {firstClass} 课表 ===");
        int days = data.Settings.DaysPerWeek;
        int periods = data.Settings.PeriodsPerDay;
        string[] dayNames = { "周一", "周二", "周三", "周四", "周五" };

        for (int d = 0; d < days; d++)
        {
            var dayEntries = result.Entries
                .Where(e => e.ClassName == firstClass && e.DayIndex == d)
                .OrderBy(e => e.PeriodIndex)
                .ToList();
            var slots = dayEntries.Select(e => $"{e.PeriodIndex}:{e.Subject}").ToList();
            _output.WriteLine($"{dayNames[d]}: {string.Join(" ", slots)}");
        }

        // 统计主科上午占比
        _output.WriteLine("\n=== 主科分布统计 ===");
        int morning = data.Settings.MorningPeriods;
        foreach (string subj in new[] { "语文", "数学", "英语" })
        {
            var subjEntries = result.Entries.Where(e => e.ClassName == firstClass && e.Subject == subj).ToList();
            int morningCount = subjEntries.Count(e => e.PeriodIndex <= morning);
            int afternoonCount = subjEntries.Count - morningCount;

            // 统计每天上午最多几节
            int maxMorningPerDay = 0;
            for (int d = 0; d < days; d++)
            {
                int mCount = subjEntries.Count(e => e.DayIndex == d && e.PeriodIndex <= morning);
                if (mCount > maxMorningPerDay) maxMorningPerDay = mCount;
            }

            // 统计连排次数
            int consecutive = 0;
            for (int d = 0; d < days; d++)
            {
                var dayPeriods = subjEntries.Where(e => e.DayIndex == d).Select(e => e.PeriodIndex).OrderBy(p => p).ToList();
                for (int i = 1; i < dayPeriods.Count; i++)
                    if (dayPeriods[i] == dayPeriods[i - 1] + 1) consecutive++;
            }

            _output.WriteLine($"{subj}: 总{subjEntries.Count}节, 上午{morningCount}/下午{afternoonCount}, 上午最大/天={maxMorningPerDay}, 连排={consecutive}");
        }

        // 统计第1节多样性
        _output.WriteLine("\n=== 第1节分布 ===");
        for (int d = 0; d < days; d++)
        {
            var p1 = result.Entries.FirstOrDefault(e => e.ClassName == firstClass && e.DayIndex == d && e.PeriodIndex == 1);
            _output.WriteLine($"{dayNames[d]}第1节: {p1?.Subject ?? "无"}");
        }

        // 体育课位置
        _output.WriteLine("\n=== 体育课位置 ===");
        var peEntries = result.Entries.Where(e => e.ClassName == firstClass && e.Subject == "体育").ToList();
        foreach (var pe in peEntries)
            _output.WriteLine($"  周{pe.DayIndex + 1} 第{pe.PeriodIndex}节 {(pe.PeriodIndex > morning ? "下午" : "上午!")}");

        Assert.NotEmpty(result.Entries);
    }
}
