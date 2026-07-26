using System.Text;

namespace Automatic_class_schedule.Tests;

/// <summary>
/// 课表统计比较引擎 — 纯开发验证用，不进入正式功能。
/// 提取双方课表的统计特征并计算相似度评分。
/// </summary>
public static class ScheduleComparisonService
{
    private static readonly HashSet<string> MainSubjects = new() { "语文", "数学", "英语" };
    private static readonly HashSet<string> FixedSubjects = new() { "周会", "社团", "活动", "教育" };

    /// <summary>课表格子: (day 0-based, period 1-based) → subject</summary>
    public sealed class Timetable
    {
        public Dictionary<(int day, int period), string> Grid { get; } = new();
        public int Days { get; init; } = 5;
        public int Periods { get; init; } = 8;
        public int MorningPeriods { get; init; } = 4;
        public string Label { get; init; } = "";

        public string Get(int day, int period) => Grid.TryGetValue((day, period), out var s) ? s : "";
        public IEnumerable<string> AllSubjects => Grid.Values.Where(v => !FixedSubjects.Contains(v));
    }

    // ==================== 统计特征模型 ====================

    public sealed class MetricSet
    {
        /// <summary>科目时间分布: subject → period → count</summary>
        public Dictionary<string, int[]> SubjectPeriodDistribution { get; set; } = new();
        /// <summary>每天主科数量</summary>
        public int[] DailyMainCount { get; set; } = Array.Empty<int>();
        /// <summary>每天非主科数量</summary>
        public int[] DailyNonMainCount { get; set; } = Array.Empty<int>();
        /// <summary>主科上午占比</summary>
        public double MainMorningRatio { get; set; }
        /// <summary>非主科下午占比</summary>
        public double NonMainAfternoonRatio { get; set; }
        /// <summary>P1-P2主科占比（应=100%）</summary>
        public double Top2MainRatio { get; set; }
        /// <summary>第3节非主科天数</summary>
        public int Period3NonMainDays { get; set; }
        /// <summary>科目间隔统计: subject → [间隔天数列表]</summary>
        public Dictionary<string, List<int>> SubjectIntervals { get; set; } = new();
        /// <summary>2节科目是否连天</summary>
        public Dictionary<string, bool> TwoLessonConsecutive { get; set; } = new();
        /// <summary>3节科目最大连天数</summary>
        public Dictionary<string, int> ThreeLessonMaxConsecutive { get; set; } = new();
        /// <summary>连堂情况（同科目相邻节次）次数</summary>
        public int ConsecutiveLessonCount { get; set; }
        /// <summary>上午(1-4)科目类别分布: 主科/文科/理科/副科</summary>
        public Dictionary<string, int> MorningCategoryDist { get; set; } = new();
        /// <summary>下午(5-8)科目类别分布</summary>
        public Dictionary<string, int> AfternoonCategoryDist { get; set; } = new();
        /// <summary>P1科目种类数</summary>
        public int P1UniqueSubjects { get; set; }
        /// <summary>P1-P2组合种类数</summary>
        public int P1P2UniqueCombos { get; set; }
        /// <summary>每天科目种类数（多样性）</summary>
        public double AvgDailySubjectVariety { get; set; }
    }

    public sealed class ComparisonScore
    {
        public string MetricName { get; init; } = "";
        public double Score { get; init; }  // 0-100
        public string Detail { get; init; } = "";
    }

    public sealed class ComparisonReport
    {
        public double TotalScore { get; set; }
        public List<ComparisonScore> Scores { get; set; } = new();
        public string WorstMetric { get; set; } = "";
        public string RawStats { get; set; } = "";
    }

    // ==================== 特征提取 ====================

    public static MetricSet ExtractMetrics(Timetable tt)
    {
        var m = new MetricSet();
        int days = tt.Days, periods = tt.Periods, morning = tt.MorningPeriods;

        // 1. 科目时间分布
        foreach (var kvp in tt.Grid)
        {
            string subj = kvp.Value;
            if (FixedSubjects.Contains(subj)) continue;
            if (!m.SubjectPeriodDistribution.ContainsKey(subj))
                m.SubjectPeriodDistribution[subj] = new int[periods + 1];
            m.SubjectPeriodDistribution[subj][kvp.Key.period]++;
        }

        // 2. 每天主科/非主科数量
        m.DailyMainCount = new int[days];
        m.DailyNonMainCount = new int[days];
        foreach (var kvp in tt.Grid)
        {
            if (FixedSubjects.Contains(kvp.Value)) continue;
            if (MainSubjects.Contains(kvp.Value))
                m.DailyMainCount[kvp.Key.day]++;
            else
                m.DailyNonMainCount[kvp.Key.day]++;
        }

        // 3. 主科上午占比
        int mainTotal = 0, mainMorning = 0;
        int nonMainTotal = 0, nonMainAfternoon = 0;
        foreach (var kvp in tt.Grid)
        {
            if (FixedSubjects.Contains(kvp.Value)) continue;
            bool isMorning = kvp.Key.period <= morning;
            if (MainSubjects.Contains(kvp.Value))
            {
                mainTotal++;
                if (isMorning) mainMorning++;
            }
            else
            {
                nonMainTotal++;
                if (!isMorning) nonMainAfternoon++;
            }
        }
        m.MainMorningRatio = mainTotal > 0 ? (double)mainMorning / mainTotal : 0;
        m.NonMainAfternoonRatio = nonMainTotal > 0 ? (double)nonMainAfternoon / nonMainTotal : 0;

        // 4. P1-P2主科占比
        int top2Total = 0, top2Main = 0;
        for (int d = 0; d < days; d++)
            for (int p = 1; p <= 2; p++)
            {
                string s = tt.Get(d, p);
                if (string.IsNullOrEmpty(s) || FixedSubjects.Contains(s)) continue;
                top2Total++;
                if (MainSubjects.Contains(s)) top2Main++;
            }
        m.Top2MainRatio = top2Total > 0 ? (double)top2Main / top2Total : 0;

        // 5. 第3节非主科天数
        for (int d = 0; d < days; d++)
        {
            string s = tt.Get(d, 3);
            if (!string.IsNullOrEmpty(s) && !MainSubjects.Contains(s) && !FixedSubjects.Contains(s))
                m.Period3NonMainDays++;
        }

        // 6. 科目间隔规律
        var subjectDays = new Dictionary<string, List<int>>();
        foreach (var kvp in tt.Grid)
        {
            if (FixedSubjects.Contains(kvp.Value)) continue;
            if (!subjectDays.ContainsKey(kvp.Value))
                subjectDays[kvp.Value] = new List<int>();
            if (!subjectDays[kvp.Value].Contains(kvp.Key.day))
                subjectDays[kvp.Value].Add(kvp.Key.day);
        }
        foreach (var sd in subjectDays)
        {
            var sorted = sd.Value.OrderBy(x => x).ToList();
            var intervals = new List<int>();
            for (int i = 1; i < sorted.Count; i++)
                intervals.Add(sorted[i] - sorted[i - 1]);
            m.SubjectIntervals[sd.Key] = intervals;
        }

        // 7. 连天检查
        foreach (var sd in subjectDays)
        {
            var sorted = sd.Value.OrderBy(x => x).ToList();
            int weeklyCount = tt.Grid.Count(kvp => kvp.Value == sd.Key);
            if (weeklyCount == 2)
            {
                bool consecutive = sorted.Count == 2 && sorted[1] - sorted[0] == 1;
                m.TwoLessonConsecutive[sd.Key] = consecutive;
            }
            else if (weeklyCount == 3)
            {
                int maxConsec = 1, cur = 1;
                for (int i = 1; i < sorted.Count; i++)
                {
                    if (sorted[i] - sorted[i - 1] == 1) { cur++; maxConsec = Math.Max(maxConsec, cur); }
                    else cur = 1;
                }
                m.ThreeLessonMaxConsecutive[sd.Key] = maxConsec;
            }
        }

        // 8. 连堂情况
        int consecCount = 0;
        for (int d = 0; d < days; d++)
            for (int p = 1; p < periods; p++)
            {
                string s1 = tt.Get(d, p), s2 = tt.Get(d, p + 1);
                if (!string.IsNullOrEmpty(s1) && s1 == s2 && !FixedSubjects.Contains(s1))
                    consecCount++;
            }
        m.ConsecutiveLessonCount = consecCount;

        // 9. 上午/下午类别分布
        var catMap = new Dictionary<string, string>
        {
            ["语文"] = "主科", ["数学"] = "主科", ["英语"] = "主科",
            ["物理"] = "理科", ["化学"] = "理科", ["生物"] = "理科",
            ["历史"] = "文科", ["地理"] = "文科", ["道德"] = "文科",
            ["体育"] = "副科", ["音乐"] = "副科", ["美术"] = "副科",
            ["信息"] = "副科", ["劳动"] = "副科"
        };
        m.MorningCategoryDist = new() { ["主科"] = 0, ["文科"] = 0, ["理科"] = 0, ["副科"] = 0 };
        m.AfternoonCategoryDist = new() { ["主科"] = 0, ["文科"] = 0, ["理科"] = 0, ["副科"] = 0 };
        foreach (var kvp in tt.Grid)
        {
            if (FixedSubjects.Contains(kvp.Value)) continue;
            string cat = catMap.GetValueOrDefault(kvp.Value, "其他");
            if (kvp.Key.period <= morning)
                m.MorningCategoryDist[cat] = m.MorningCategoryDist.GetValueOrDefault(cat) + 1;
            else
                m.AfternoonCategoryDist[cat] = m.AfternoonCategoryDist.GetValueOrDefault(cat) + 1;
        }

        // 10. P1多样性 & P1-P2组合
        var p1Set = new HashSet<string>();
        var comboSet = new HashSet<string>();
        for (int d = 0; d < days; d++)
        {
            string s1 = tt.Get(d, 1), s2 = tt.Get(d, 2);
            if (!string.IsNullOrEmpty(s1)) p1Set.Add(s1);
            comboSet.Add($"{s1}+{s2}");
        }
        m.P1UniqueSubjects = p1Set.Count;
        m.P1P2UniqueCombos = comboSet.Count;

        // 11. 每天科目种类数
        double totalVariety = 0;
        for (int d = 0; d < days; d++)
        {
            var daySubjects = new HashSet<string>();
            for (int p = 1; p <= periods; p++)
            {
                string s = tt.Get(d, p);
                if (!string.IsNullOrEmpty(s) && !FixedSubjects.Contains(s))
                    daySubjects.Add(s);
            }
            totalVariety += daySubjects.Count;
        }
        m.AvgDailySubjectVariety = totalVariety / days;

        return m;
    }

    // ==================== 相似度计算 ====================

    public static ComparisonReport Compare(MetricSet solver, MetricSet reference)
    {
        var report = new ComparisonReport();
        var scores = new List<ComparisonScore>();

        // S1: 主科上午占比相似度
        scores.Add(new ComparisonScore
        {
            MetricName = "主科上午占比",
            Score = RatioScore(solver.MainMorningRatio, reference.MainMorningRatio),
            Detail = $"求解器={solver.MainMorningRatio:P0}, 参考={reference.MainMorningRatio:P0}"
        });

        // S2: 非主科下午占比
        scores.Add(new ComparisonScore
        {
            MetricName = "非主科下午占比",
            Score = RatioScore(solver.NonMainAfternoonRatio, reference.NonMainAfternoonRatio),
            Detail = $"求解器={solver.NonMainAfternoonRatio:P0}, 参考={reference.NonMainAfternoonRatio:P0}"
        });

        // S3: P1-P2主科纯度
        scores.Add(new ComparisonScore
        {
            MetricName = "P1-P2主科纯度",
            Score = RatioScore(solver.Top2MainRatio, reference.Top2MainRatio),
            Detail = $"求解器={solver.Top2MainRatio:P0}, 参考={reference.Top2MainRatio:P0}"
        });

        // S4: 第3节非主科天数
        scores.Add(new ComparisonScore
        {
            MetricName = "第3节非主科天数",
            Score = IntScore(solver.Period3NonMainDays, reference.Period3NonMainDays, 5),
            Detail = $"求解器={solver.Period3NonMainDays}天, 参考={reference.Period3NonMainDays}天"
        });

        // S5: P1科目多样性
        scores.Add(new ComparisonScore
        {
            MetricName = "P1科目多样性",
            Score = IntScore(solver.P1UniqueSubjects, reference.P1UniqueSubjects, 5),
            Detail = $"求解器={solver.P1UniqueSubjects}种, 参考={reference.P1UniqueSubjects}种"
        });

        // S6: P1-P2组合多样性
        scores.Add(new ComparisonScore
        {
            MetricName = "P1-P2组合多样性",
            Score = IntScore(solver.P1P2UniqueCombos, reference.P1P2UniqueCombos, 5),
            Detail = $"求解器={solver.P1P2UniqueCombos}种, 参考={reference.P1P2UniqueCombos}种"
        });

        // S7: 连堂控制
        scores.Add(new ComparisonScore
        {
            MetricName = "连堂控制",
            Score = IntScore(solver.ConsecutiveLessonCount, reference.ConsecutiveLessonCount, 5),
            Detail = $"求解器={solver.ConsecutiveLessonCount}次, 参考={reference.ConsecutiveLessonCount}次"
        });

        // S8: 每日主科均衡度
        double solverMainVar = Variance(solver.DailyMainCount);
        double refMainVar = Variance(reference.DailyMainCount);
        scores.Add(new ComparisonScore
        {
            MetricName = "每日主科均衡度",
            Score = RatioScore(solverMainVar, refMainVar, invert: true),
            Detail = $"求解器方差={solverMainVar:F2}, 参考方差={refMainVar:F2}"
        });

        // S9: 科目间隔规律相似度
        double intervalScore = CompareIntervals(solver.SubjectIntervals, reference.SubjectIntervals);
        scores.Add(new ComparisonScore
        {
            MetricName = "科目间隔规律",
            Score = intervalScore,
            Detail = FormatIntervalDetail(solver.SubjectIntervals, reference.SubjectIntervals)
        });

        // S10: 连天规则遵守
        double consecDayScore = CompareConsecutiveDays(solver, reference);
        scores.Add(new ComparisonScore
        {
            MetricName = "连天规则遵守",
            Score = consecDayScore,
            Detail = FormatConsecDayDetail(solver, reference)
        });

        // S11: 上午类别分布
        double morningCatScore = CompareCategoryDist(solver.MorningCategoryDist, reference.MorningCategoryDist);
        scores.Add(new ComparisonScore
        {
            MetricName = "上午类别分布",
            Score = morningCatScore,
            Detail = $"求解器={FormatCatDist(solver.MorningCategoryDist)}, 参考={FormatCatDist(reference.MorningCategoryDist)}"
        });

        // S12: 下午类别分布
        double afternoonCatScore = CompareCategoryDist(solver.AfternoonCategoryDist, reference.AfternoonCategoryDist);
        scores.Add(new ComparisonScore
        {
            MetricName = "下午类别分布",
            Score = afternoonCatScore,
            Detail = $"求解器={FormatCatDist(solver.AfternoonCategoryDist)}, 参考={FormatCatDist(reference.AfternoonCategoryDist)}"
        });

        // S13: 每日科目多样性
        scores.Add(new ComparisonScore
        {
            MetricName = "每日科目多样性",
            Score = RatioScore(solver.AvgDailySubjectVariety, reference.AvgDailySubjectVariety),
            Detail = $"求解器={solver.AvgDailySubjectVariety:F1}种/天, 参考={reference.AvgDailySubjectVariety:F1}种/天"
        });

        // S14: 科目时间分布相似度（余弦相似度）
        double periodDistScore = ComparePeriodDistributions(solver.SubjectPeriodDistribution, reference.SubjectPeriodDistribution);
        scores.Add(new ComparisonScore
        {
            MetricName = "科目时间分布",
            Score = periodDistScore,
            Detail = "基于各科在各节次分布向量的余弦相似度"
        });

        report.Scores = scores;
        report.TotalScore = scores.Average(s => s.Score);
        report.WorstMetric = scores.OrderBy(s => s.Score).First().MetricName;

        return report;
    }

    // ==================== 报告格式化 ====================

    public static string FormatReport(ComparisonReport report, MetricSet solver, MetricSet reference)
    {
        var sb = new StringBuilder();
        sb.AppendLine("╔══════════════════════════════════════════════════╗");
        sb.AppendLine("║       排课质量对比报告 (开发测试用)            ║");
        sb.AppendLine("╚══════════════════════════════════════════════════╝");
        sb.AppendLine();
        sb.AppendLine($"总相似度评分: {report.TotalScore:F1} / 100");
        sb.AppendLine($"差异最大项: {report.WorstMetric}");
        sb.AppendLine();
        sb.AppendLine("┌─── 各项指标评分 ───┐");
        foreach (var s in report.Scores.OrderByDescending(s => s.Score))
        {
            string bar = new('█', (int)(s.Score / 5));
            string empty = new('░', 20 - bar.Length);
            sb.AppendLine($"  {s.MetricName,-12} {s.Score,5:F1} {bar}{empty} | {s.Detail}");
        }
        sb.AppendLine();

        // 详细统计
        sb.AppendLine("┌─── 详细统计数据 ───┐");
        sb.AppendLine();
        sb.AppendLine("[科目时间分布 - 求解器]");
        foreach (var kvp in solver.SubjectPeriodDistribution.OrderBy(k => k.Key))
            sb.AppendLine($"  {kvp.Key}: {string.Join(" ", kvp.Value.Skip(1).Select((v, i) => $"P{i + 1}={v}"))}");
        sb.AppendLine();
        sb.AppendLine("[科目时间分布 - 参考]");
        foreach (var kvp in reference.SubjectPeriodDistribution.OrderBy(k => k.Key))
            sb.AppendLine($"  {kvp.Key}: {string.Join(" ", kvp.Value.Skip(1).Select((v, i) => $"P{i + 1}={v}"))}");
        sb.AppendLine();

        sb.AppendLine("[科目间隔(天) - 求解器]");
        foreach (var kvp in solver.SubjectIntervals.Where(k => k.Value.Count > 0).OrderBy(k => k.Key))
            sb.AppendLine($"  {kvp.Key}: [{string.Join(",", kvp.Value)}] 平均={kvp.Value.Average():F1}");
        sb.AppendLine();
        sb.AppendLine("[科目间隔(天) - 参考]");
        foreach (var kvp in reference.SubjectIntervals.Where(k => k.Value.Count > 0).OrderBy(k => k.Key))
            sb.AppendLine($"  {kvp.Key}: [{string.Join(",", kvp.Value)}] 平均={kvp.Value.Average():F1}");
        sb.AppendLine();

        sb.AppendLine("[连天检查 - 求解器]");
        foreach (var kvp in solver.TwoLessonConsecutive)
            sb.AppendLine($"  {kvp.Key}(2节): {(kvp.Value ? "连天✗" : "不连天✓")}");
        foreach (var kvp in solver.ThreeLessonMaxConsecutive)
            sb.AppendLine($"  {kvp.Key}(3节): 最大连天={kvp.Value} {(kvp.Value <= 2 ? "✓" : "✗")}");
        sb.AppendLine();
        sb.AppendLine("[连天检查 - 参考]");
        foreach (var kvp in reference.TwoLessonConsecutive)
            sb.AppendLine($"  {kvp.Key}(2节): {(kvp.Value ? "连天✗" : "不连天✓")}");
        foreach (var kvp in reference.ThreeLessonMaxConsecutive)
            sb.AppendLine($"  {kvp.Key}(3节): 最大连天={kvp.Value} {(kvp.Value <= 2 ? "✓" : "✗")}");

        return sb.ToString();
    }

    // ==================== 评分辅助方法 ====================

    /// <summary>比率相似度: 两者越接近分越高</summary>
    private static double RatioScore(double a, double b, bool invert = false)
    {
        if (invert)
        {
            // 方差等越小越好的指标
            double max = Math.Max(a, b);
            if (max < 0.001) return 100;
            double min = Math.Min(a, b);
            return Math.Max(0, 100 * min / max);
        }
        double diff = Math.Abs(a - b);
        return Math.Max(0, 100 - diff * 200); // 差0.5→0分
    }

    /// <summary>整数相似度</summary>
    private static double IntScore(int a, int b, int maxScale)
    {
        double diff = Math.Abs(a - b);
        return Math.Max(0, 100 - diff * (100.0 / maxScale));
    }

    private static double Variance(int[] arr)
    {
        if (arr.Length == 0) return 0;
        double avg = arr.Average();
        return arr.Select(x => (x - avg) * (x - avg)).Average();
    }

    /// <summary>科目间隔相似度: 比较各科平均间隔</summary>
    private static double CompareIntervals(Dictionary<string, List<int>> solver, Dictionary<string, List<int>> reference)
    {
        var common = solver.Keys.Intersect(reference.Keys)
            .Where(k => solver[k].Count > 0 && reference[k].Count > 0).ToList();
        if (common.Count == 0) return 50;
        double totalScore = 0;
        foreach (var subj in common)
        {
            double sAvg = solver[subj].Average();
            double rAvg = reference[subj].Average();
            double diff = Math.Abs(sAvg - rAvg);
            totalScore += Math.Max(0, 100 - diff * 50); // 差2天→0分
        }
        return totalScore / common.Count;
    }

    private static string FormatIntervalDetail(Dictionary<string, List<int>> solver, Dictionary<string, List<int>> reference)
    {
        var common = solver.Keys.Intersect(reference.Keys)
            .Where(k => solver[k].Count > 0 && reference[k].Count > 0).Take(5).ToList();
        var parts = common.Select(s =>
            $"{s}:求解{solver[s].Average():F1}vs参考{reference[s].Average():F1}");
        return string.Join("; ", parts);
    }

    /// <summary>连天规则比较</summary>
    private static double CompareConsecutiveDays(MetricSet solver, MetricSet reference)
    {
        // 2节科目不连天得分
        int solverTwoViolations = solver.TwoLessonConsecutive.Values.Count(v => v);
        int refTwoViolations = reference.TwoLessonConsecutive.Values.Count(v => v);
        // 3节科目最大连天
        int solverThreeMax = solver.ThreeLessonMaxConsecutive.Values.DefaultIfEmpty(0).Max();
        int refThreeMax = reference.ThreeLessonMaxConsecutive.Values.DefaultIfEmpty(0).Max();

        double score = 100;
        score -= solverTwoViolations * 15; // 每个2节连天扣15
        if (solverThreeMax > 2) score -= 20;
        // 与参考的差距
        score -= Math.Abs(solverTwoViolations - refTwoViolations) * 10;
        score -= Math.Abs(solverThreeMax - refThreeMax) * 10;
        return Math.Max(0, Math.Min(100, score));
    }

    private static string FormatConsecDayDetail(MetricSet solver, MetricSet reference)
    {
        int sTwo = solver.TwoLessonConsecutive.Values.Count(v => v);
        int rTwo = reference.TwoLessonConsecutive.Values.Count(v => v);
        int sThree = solver.ThreeLessonMaxConsecutive.Values.DefaultIfEmpty(0).Max();
        int rThree = reference.ThreeLessonMaxConsecutive.Values.DefaultIfEmpty(0).Max();
        return $"2节连天:求解{sTwo}/参考{rTwo}; 3节最大连天:求解{sThree}/参考{rThree}";
    }

    /// <summary>类别分布相似度（余弦）</summary>
    private static double CompareCategoryDist(Dictionary<string, int> a, Dictionary<string, int> b)
    {
        var keys = a.Keys.Union(b.Keys).ToList();
        double dot = 0, magA = 0, magB = 0;
        foreach (var k in keys)
        {
            double va = a.GetValueOrDefault(k), vb = b.GetValueOrDefault(k);
            dot += va * vb;
            magA += va * va;
            magB += vb * vb;
        }
        if (magA < 0.001 || magB < 0.001) return 50;
        return dot / (Math.Sqrt(magA) * Math.Sqrt(magB)) * 100;
    }

    private static string FormatCatDist(Dictionary<string, int> dist)
        => string.Join("/", dist.Select(kvp => $"{kvp.Key}{kvp.Value}"));

    /// <summary>科目时间分布余弦相似度</summary>
    private static double ComparePeriodDistributions(
        Dictionary<string, int[]> solver, Dictionary<string, int[]> reference)
    {
        var common = solver.Keys.Intersect(reference.Keys).ToList();
        if (common.Count == 0) return 50;
        double totalScore = 0;
        foreach (var subj in common)
        {
            var a = solver[subj];
            var b = reference[subj];
            int len = Math.Min(a.Length, b.Length);
            double dot = 0, magA = 0, magB = 0;
            for (int i = 1; i < len; i++)
            {
                dot += a[i] * b[i];
                magA += a[i] * a[i];
                magB += b[i] * b[i];
            }
            if (magA < 0.001 || magB < 0.001) { totalScore += 50; continue; }
            totalScore += dot / (Math.Sqrt(magA) * Math.Sqrt(magB)) * 100;
        }
        return totalScore / common.Count;
    }
}
