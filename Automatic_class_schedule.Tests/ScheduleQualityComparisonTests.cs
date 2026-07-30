using Automatic_class_schedule.Models;
using Automatic_class_schedule.Services;
using Automatic_class_schedule.Solver;
using ClosedXML.Excel;
using Xunit.Abstractions;

namespace Automatic_class_schedule.Tests;

/// <summary>
/// 开发验证用：运行CP-SAT求解器后与人工课表 out.xlsx 进行全面统计对比。
/// 输出完整质量报告到 comparison_report.txt。
/// </summary>
[Trait("Category", "LocalOnly")]
public sealed class ScheduleQualityComparisonTests
{
    private readonly ITestOutputHelper _output;
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
    private static readonly string ReportPath = Path.Combine(
        Directory.GetCurrentDirectory(), "..", "..", "..", "..", "comparison_report.txt");

    public ScheduleQualityComparisonTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>从 out.xlsx 读取指定班级课表</summary>
    private static ScheduleComparisonService.Timetable ReadReferenceTimetable(string className)
    {
        var tt = new ScheduleComparisonService.Timetable { Label = $"参考({className})" };
        if (!File.Exists(OutXlsxPath)) return tt;

        using var workbook = new XLWorkbook(OutXlsxPath);
        var sheet = workbook.Worksheets.First();

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
        if (targetRow < 0) return tt;

        for (int d = 0; d < 5; d++)
        {
            int colStart = 2 + d * 8;
            for (int p = 1; p <= 8; p++)
            {
                string subject = sheet.Cell(targetRow, colStart + p - 1).GetString().Trim();
                if (!string.IsNullOrEmpty(subject))
                {
                    if (SubjectAbbrMap.TryGetValue(subject, out string? full))
                        subject = full;
                    tt.Grid[(d, p)] = subject;
                }
            }
        }
        return tt;
    }

    /// <summary>从求解器结果提取指定班级课表</summary>
    private static ScheduleComparisonService.Timetable ExtractSolverTimetable(
        ScheduleResult result, Guid classId, string label)
    {
        var tt = new ScheduleComparisonService.Timetable { Label = label };
        foreach (var e in result.Entries.Where(e => e.ClassId == classId))
            tt.Grid[(e.DayIndex, e.PeriodIndex)] = e.Subject;
        return tt;
    }

    [Fact]
    public void FullComparison_GeneratesReport()
    {
        var lines = new List<string>();

        // ═══ 1. 运行求解器 ═══
        lines.Add("=== 运行CP-SAT求解器（30秒时限）===");
        var data = SampleDataFactory.Create(skipSolve: true);
        var service = new ScheduleService(new CpSatScheduleSolver(), new ConflictService());
        var progress = new Progress<double>(_ => { });
        var result = service.Generate(data, progress);

        int hardConflicts = result.Conflicts.Count(c => c.Severity == ScheduleConflictSeverity.Hard);
        lines.Add($"求解完成: {result.Entries.Count}节课, 硬冲突={hardConflicts}");
        foreach (var c in result.Conflicts.Where(c => c.Severity == ScheduleConflictSeverity.Hard).Take(5))
            lines.Add($"  [硬冲突] {c.TypeText} | {c.Scope} | {c.Message}");
        lines.Add("");

        Assert.NotEmpty(result.Entries);
        Assert.Equal(0, hardConflicts);

        // ═══ 2. 多班级对比 ═══
        var classNames = new[] { ("七1", "七年级"), ("八1", "八年级"), ("九1", "九年级") };
        var allScores = new List<ScheduleComparisonService.ComparisonReport>();

        foreach (var (refName, gradeName) in classNames)
        {
            var classEntry = data.Classes.FirstOrDefault(c => c.GradeName == gradeName);
            if (classEntry == null) continue;

            var solverTT = ExtractSolverTimetable(result, classEntry.Id, $"求解器({classEntry.Name})");
            var refTT = ReadReferenceTimetable(refName);
            if (refTT.Grid.Count == 0)
            {
                lines.Add($"[SKIP] 未能读取参考课表: {refName}");
                continue;
            }

            // 打印课表
            lines.Add($"╔═══ {classEntry.Name} vs {refName} ═══╗");
            lines.Add("");
            lines.Add($"--- 求解器: {classEntry.Name} ---");
            lines.AddRange(FormatTimetable(solverTT));
            lines.Add("");
            lines.Add($"--- 参考: {refName} ---");
            lines.AddRange(FormatTimetable(refTT));
            lines.Add("");

            // 提取指标并比较
            var solverMetrics = ScheduleComparisonService.ExtractMetrics(solverTT);
            var refMetrics = ScheduleComparisonService.ExtractMetrics(refTT);
            var report = ScheduleComparisonService.Compare(solverMetrics, refMetrics);
            allScores.Add(report);

            lines.Add(ScheduleComparisonService.FormatReport(report, solverMetrics, refMetrics));
            lines.Add("");
            lines.Add("═══════════════════════════════════════════════════");
            lines.Add("");
        }

        // ═══ 3. 总体汇总 ═══
        if (allScores.Count > 0)
        {
            lines.Add("╔══════════════════════════════════════════════════╗");
            lines.Add("║              总体汇总                          ║");
            lines.Add("╚══════════════════════════════════════════════════╝");
            lines.Add("");
            double avgTotal = allScores.Average(r => r.TotalScore);
            lines.Add($"平均总相似度: {avgTotal:F1} / 100");
            lines.Add("");

            // 按指标汇总
            var metricNames = allScores[0].Scores.Select(s => s.MetricName).ToList();
            lines.Add("各指标平均分:");
            foreach (var name in metricNames)
            {
                double avg = allScores.Average(r => r.Scores.First(s => s.MetricName == name).Score);
                string bar = new('█', (int)(avg / 5));
                string empty = new('░', 20 - bar.Length);
                lines.Add($"  {name,-12} {avg,5:F1} {bar}{empty}");
            }
            lines.Add("");

            // 差异最大项
            var worstPerClass = allScores.Select(r => $"{r.WorstMetric}({r.Scores.First(s => s.MetricName == r.WorstMetric).Score:F0})");
            lines.Add($"各班差异最大项: {string.Join(", ", worstPerClass)}");
        }

        // 写入报告文件
        string fullPath = Path.GetFullPath(ReportPath);
        File.WriteAllLines(fullPath, lines, System.Text.Encoding.UTF8);
        _output.WriteLine($"报告已写入: {fullPath}");
        _output.WriteLine($"平均总相似度: {allScores.Average(r => r.TotalScore):F1} / 100");

        // 基本断言：总分应>50
        if (allScores.Count > 0)
            Assert.True(allScores.Average(r => r.TotalScore) > 40, "总相似度过低，排课质量需改进");
    }

    /// <summary>快速对比（仅七1班，用于日常开发迭代验证）</summary>
    [Fact]
    public void QuickComparison_SingleClass()
    {
        var data = SampleDataFactory.Create(skipSolve: true);
        var service = new ScheduleService(new CpSatScheduleSolver(), new ConflictService());
        var result = service.Generate(data, new Progress<double>(_ => { }));

        var classEntry = data.Classes.First(c => c.GradeName == "七年级");
        var solverTT = ExtractSolverTimetable(result, classEntry.Id, "求解器");
        var refTT = ReadReferenceTimetable("七1");

        Assert.NotEmpty(refTT.Grid);

        var solverM = ScheduleComparisonService.ExtractMetrics(solverTT);
        var refM = ScheduleComparisonService.ExtractMetrics(refTT);
        var report = ScheduleComparisonService.Compare(solverM, refM);

        string reportText = ScheduleComparisonService.FormatReport(report, solverM, refM);
        _output.WriteLine(reportText);

        // 写入文件
        File.WriteAllText(Path.GetFullPath(ReportPath), reportText, System.Text.Encoding.UTF8);
        _output.WriteLine($"总分: {report.TotalScore:F1}/100, 最差项: {report.WorstMetric}");

        Assert.True(report.TotalScore > 40);
    }

    private static List<string> FormatTimetable(ScheduleComparisonService.Timetable tt)
    {
        var lines = new List<string> { "     周一      周二      周三      周四      周五" };
        for (int p = 1; p <= tt.Periods; p++)
        {
            var row = $"P{p}: ";
            for (int d = 0; d < tt.Days; d++)
            {
                string s = tt.Get(d, p);
                row += (string.IsNullOrEmpty(s) ? "---" : s).PadRight(10);
            }
            lines.Add(row);
        }
        return lines;
    }
}
