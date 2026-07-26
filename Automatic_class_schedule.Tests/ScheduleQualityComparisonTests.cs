using Automatic_class_schedule.Models;
using Automatic_class_schedule.Services;
using Automatic_class_schedule.Solver;
using ClosedXML.Excel;
using Xunit.Abstractions;

namespace Automatic_class_schedule.Tests;

/// <summary>
/// 运行CP-SAT求解器后，与人工课表 out.xlsx 进行多样性和规律性对比。
/// 指标：
///   1. 第3节非主科数量（目标0-2天/周）
///   2. 主科在第1-2节的多样性（不应总是语+英）
///   3. 数学在各节次的分布（不应集中在第4节）
///   4. 第1节科目轮换（一周内不同科目数）
///   5. 主科上午占比
/// </summary>
public sealed class ScheduleQualityComparisonTests
{
    private readonly ITestOutputHelper _output;
    private readonly List<string> _outputBuffer = new();
    private static readonly HashSet<string> MainSubjects = new() { "语文", "数学", "英语" };
    private static readonly Dictionary<string, string> SubjectAbbrMap = new()
    {
        ["语"] = "语文", ["数"] = "数学", ["英"] = "英语",
        ["物"] = "物理", ["化"] = "化学", ["生"] = "生物",
        ["历"] = "历史", ["地"] = "地理", ["道"] = "道德",
        ["体"] = "体育", ["音"] = "音乐", ["美"] = "美术",
        ["信"] = "信息", ["劳"] = "劳动"
    };
    private static readonly string OutXlsxPath = Path.Combine(
        Directory.GetCurrentDirectory(), "..", "..", "..", "..", "out.xlsx");

    public ScheduleQualityComparisonTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>从 out.xlsx 读取指定班级课表，返回 [day, period] → subject</summary>
    private static Dictionary<(int day, int period), string> ReadReferenceTimetable(string className)
    {
        var result = new Dictionary<(int, int), string>();
        if (!File.Exists(OutXlsxPath)) return result;
    
        using var workbook = new XLWorkbook(OutXlsxPath);
        var sheet = workbook.Worksheets.First();
    
        // 格式：第1列=班级名，列2-9=周一P1-P8，列10-17=周二...列34-41=周五
        int targetRow = -1;
        for (int r = 2; r <= 30; r++)
        {
            string name = sheet.Cell(r, 1).GetString().Trim();
            if (name == className || name.Replace("班", "") == className)
            {
                targetRow = r;
                break;
            }
        }
        if (targetRow < 0) return result;
    
        for (int d = 0; d < 5; d++)
        {
            int colStart = 2 + d * 8;
            for (int p = 1; p <= 8; p++)
            {
                string subject = sheet.Cell(targetRow, colStart + p - 1).GetString().Trim();
                if (!string.IsNullOrEmpty(subject))
                {
                    // 规范化缩写→全名
                    if (SubjectAbbrMap.TryGetValue(subject, out string? full))
                        subject = full;
                    result[(d, p)] = subject;
                }
            }
        }
        return result;
    }

    /// <summary>分析课表质量指标</summary>
    private static QualityMetrics AnalyzeMetrics(Dictionary<(int day, int period), string> timetable, int days = 5, int periods = 8)
    {
        var metrics = new QualityMetrics();

        // 1. 第3节非主科天数
        int period3NonMainDays = 0;
        var period3Subjects = new List<string>();
        for (int d = 0; d < days; d++)
        {
            if (timetable.TryGetValue((d, 3), out string? subj))
            {
                period3Subjects.Add($"{DayName(d)}:{subj}");
                if (!MainSubjects.Contains(subj))
                    period3NonMainDays++;
            }
        }
        metrics.Period3NonMainDays = period3NonMainDays;
        metrics.Period3Detail = string.Join(", ", period3Subjects);

        // 2. 第1-2节主科组合多样性
        var top2Combos = new List<string>();
        for (int d = 0; d < days; d++)
        {
            timetable.TryGetValue((d, 1), out string? p1);
            timetable.TryGetValue((d, 2), out string? p2);
            top2Combos.Add($"{p1 ?? "?"}+{p2 ?? "?"}");
        }
        metrics.Top2Combos = string.Join(", ", top2Combos);
        metrics.Top2UniqueCombos = top2Combos.Distinct().Count();

        // 3. 数学在各节次的分布
        var mathPeriodDist = new int[periods + 1];
        foreach (var kvp in timetable)
        {
            if (kvp.Value == "数学")
                mathPeriodDist[kvp.Key.period]++;
        }
        metrics.MathDistribution = string.Join(", ",
            Enumerable.Range(1, periods).Select(p => $"P{p}={mathPeriodDist[p]}"));
        metrics.MathAtPeriod4Plus = Enumerable.Range(4, periods - 3).Sum(p => mathPeriodDist[p]);
        metrics.MathTotal = mathPeriodDist.Sum();

        // 4. 第1节科目轮换
        var firstPeriodSubjects = new List<string>();
        for (int d = 0; d < days; d++)
        {
            if (timetable.TryGetValue((d, 1), out string? subj))
                firstPeriodSubjects.Add(subj);
        }
        metrics.FirstPeriodUnique = firstPeriodSubjects.Distinct().Count();
        metrics.FirstPeriodDetail = string.Join(", ",
            Enumerable.Range(0, days).Select(d => $"{DayName(d)}:{(timetable.TryGetValue((d, 1), out var s) ? s : "?")}"));

        // 5. 主科上午占比（period 1-4）
        int mainTotal = 0, mainMorning = 0;
        foreach (var kvp in timetable)
        {
            if (MainSubjects.Contains(kvp.Value))
            {
                mainTotal++;
                if (kvp.Key.period <= 4) mainMorning++;
            }
        }
        metrics.MainTotal = mainTotal;
        metrics.MainMorning = mainMorning;
        metrics.MainMorningRatio = mainTotal > 0 ? (double)mainMorning / mainTotal : 0;

        // 6. 每天主科数量分布
        var dailyMainCount = new int[days];
        foreach (var kvp in timetable)
        {
            if (MainSubjects.Contains(kvp.Value))
                dailyMainCount[kvp.Key.day]++;
        }
        metrics.DailyMainCounts = string.Join(", ", dailyMainCount.Select((c, i) => $"{DayName(i)}={c}"));

        return metrics;
    }

    private static string DayName(int d) => d switch
    {
        0 => "周一", 1 => "周二", 2 => "周三", 3 => "周四", 4 => "周五", _ => $"D{d}"
    };

    [Fact]
    public void DumpOutXlsx_Structure()
    {
        string outPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "out_dump.txt");
        Assert.True(File.Exists(OutXlsxPath), $"out.xlsx not found at {Path.GetFullPath(OutXlsxPath)}");

        var lines = new List<string>();
        using var wb = new XLWorkbook(OutXlsxPath);
        foreach (var s in wb.Worksheets)
        {
            int rows = s.LastRowUsed()?.RowNumber() ?? 0;
            int cols = s.LastColumnUsed()?.ColumnNumber() ?? 0;
            lines.Add($"Sheet: [{s.Name}] Rows={rows} Cols={cols}");
            for (int r = 1; r <= Math.Min(24, rows); r++)
            {
                var row = $"R{r:D2}: ";
                for (int c = 1; c <= Math.Min(41, cols); c++)
                {
                    string val = s.Cell(r, c).GetString();
                    row += val.PadRight(6);
                }
                lines.Add(row);
            }
        }
        File.WriteAllLines(Path.GetFullPath(outPath), lines, System.Text.Encoding.UTF8);
        _output.WriteLine($"Dumped to {Path.GetFullPath(outPath)}");
    }

    [Fact]
    public void CompareSolverOutput_WithReference_OutputsMetrics()
    {
        // 1. 准备数据（不求解，手动调用求解器）
        var data = SampleDataFactory.Create(skipSolve: true);
        var service = new ScheduleService(new CpSatScheduleSolver(), new ConflictService());

        _output.WriteLine("=== 运行CP-SAT求解器（30秒时限）===");
        _outputBuffer.Add("=== 运行CP-SAT求解器（30秒时限）===");
        var progress = new Progress<double>(p => { });
        var result = service.Generate(data, progress);

        Assert.NotEmpty(result.Entries);
        int hardConflicts = result.Conflicts.Count(c => c.Severity == ScheduleConflictSeverity.Hard);
        var summary = $"求解完成: {result.Entries.Count}节课, 硬冲突={hardConflicts}";
        _output.WriteLine(summary);
        _outputBuffer.Add(summary);
        // 输出硬冲突详情
        foreach (var c in result.Conflicts.Where(c => c.Severity == ScheduleConflictSeverity.Hard).Take(10))
        {
            var line = $"  [硬冲突] {c.TypeText} | {c.Scope} | {c.Message}";
            _output.WriteLine(line);
            _outputBuffer.Add(line);
        }

        // 2. 提取七年级1班的课表
        var classEntry = data.Classes.First(c => c.Name.Contains("七") && c.Name.Contains("1"));

        // 诊断：输出九年级课时明细
        var grade9Class = data.Classes.First(c => c.GradeName == "九年级");
        var g9Reqs = data.Requirements.Where(r => r.ClassId == grade9Class.Id).ToList();
        _outputBuffer.Add($"\n=== 九年级课时明细 ({grade9Class.Name}) ===");
        foreach (var r in g9Reqs)
            _outputBuffer.Add($"  {r.Subject}: {r.WeeklyCount}节 ({r.TeacherName})");
        _outputBuffer.Add($"  总计: {g9Reqs.Sum(r => r.WeeklyCount)}节");
        var solverTimetable = new Dictionary<(int, int), string>();
        foreach (var e in result.Entries.Where(e => e.ClassId == classEntry.Id))
            solverTimetable[(e.DayIndex, e.PeriodIndex)] = e.Subject;

        _output.WriteLine($"\n=== 求解器结果: {classEntry.Name} ===");
        _outputBuffer.Add($"\n=== 求解器结果: {classEntry.Name} ===");
        PrintTimetable(solverTimetable);
        var solverMetrics = AnalyzeMetrics(solverTimetable);
        PrintMetrics("求解器", solverMetrics);

        // 3. 读取 out.xlsx 参考课表
        var refTimetable = ReadReferenceTimetable("七1");
        if (refTimetable.Count > 0)
        {
            _output.WriteLine($"\n=== 参考课表(out.xlsx): 七1 ===");
            _outputBuffer.Add("\n=== 参考课表(out.xlsx): 七1 ===");
            PrintTimetable(refTimetable);
            var refMetrics = AnalyzeMetrics(refTimetable);
            PrintMetrics("参考", refMetrics);

            // 4. 对比总结
            var comparisons = new[]
            {
                "\n=== 对比总结 ===",
                $"第3节非主科天数: 求解器={solverMetrics.Period3NonMainDays} vs 参考={refMetrics.Period3NonMainDays} (目标≤2)",
                $"第1-2节组合多样性: 求解器={solverMetrics.Top2UniqueCombos}种 vs 参考={refMetrics.Top2UniqueCombos}种",
                $"数学P4及之后: 求解器={solverMetrics.MathAtPeriod4Plus}/{solverMetrics.MathTotal} vs 参考={refMetrics.MathAtPeriod4Plus}/{refMetrics.MathTotal}",
                $"第1节科目种类: 求解器={solverMetrics.FirstPeriodUnique}种 vs 参考={refMetrics.FirstPeriodUnique}种",
                $"主科上午占比: 求解器={solverMetrics.MainMorningRatio:P0} vs 参考={refMetrics.MainMorningRatio:P0}"
            };
            foreach (var line in comparisons)
            {
                _output.WriteLine(line);
                _outputBuffer.Add(line);
            }
        }
        else
        {
            _output.WriteLine($"\n[WARN] 未能从out.xlsx读取七1课表，文件路径: {Path.GetFullPath(OutXlsxPath)}");
        }

        // 输出到文件避免控制台编码问题
        string dumpPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "quality_comparison.txt");
        File.WriteAllLines(Path.GetFullPath(dumpPath), _outputBuffer, System.Text.Encoding.UTF8);
        _output.WriteLine($"\n详细结果已写入: {Path.GetFullPath(dumpPath)}");
    }

    private void PrintTimetable(Dictionary<(int, int), string> timetable)
    {
        var header = "     周一    周二    周三    周四    周五";
        _output.WriteLine(header);
        _outputBuffer.Add(header);
        for (int p = 1; p <= 8; p++)
        {
            var row = $"P{p}: ";
            for (int d = 0; d < 5; d++)
            {
                timetable.TryGetValue((d, p), out string? subj);
                row += (subj ?? "---").PadRight(8);
            }
            _output.WriteLine(row);
            _outputBuffer.Add(row);
        }
    }

    private void PrintMetrics(string label, QualityMetrics m)
    {
        var lines = new[]
        {
            $"[{label}] 第3节: {m.Period3Detail} → 非主科{m.Period3NonMainDays}天",
            $"[{label}] 1-2节组合: {m.Top2Combos} → {m.Top2UniqueCombos}种",
            $"[{label}] 数学分布: {m.MathDistribution} (P4+={m.MathAtPeriod4Plus}/{m.MathTotal})",
            $"[{label}] 第1节: {m.FirstPeriodDetail} → {m.FirstPeriodUnique}种",
            $"[{label}] 主科上午: {m.MainMorning}/{m.MainTotal} = {m.MainMorningRatio:P0}",
            $"[{label}] 每日主科: {m.DailyMainCounts}"
        };
        foreach (var line in lines)
        {
            _output.WriteLine(line);
            _outputBuffer.Add(line);
        }
    }

    private sealed class QualityMetrics
    {
        public int Period3NonMainDays { get; set; }
        public string Period3Detail { get; set; } = "";
        public string Top2Combos { get; set; } = "";
        public int Top2UniqueCombos { get; set; }
        public string MathDistribution { get; set; } = "";
        public int MathAtPeriod4Plus { get; set; }
        public int MathTotal { get; set; }
        public int FirstPeriodUnique { get; set; }
        public string FirstPeriodDetail { get; set; } = "";
        public int MainTotal { get; set; }
        public int MainMorning { get; set; }
        public double MainMorningRatio { get; set; }
        public string DailyMainCounts { get; set; } = "";
    }
}
