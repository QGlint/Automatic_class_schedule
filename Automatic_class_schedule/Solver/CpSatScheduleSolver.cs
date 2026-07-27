using Automatic_class_schedule.Models;
using Automatic_class_schedule.Services;
using Google.OrTools.Sat;

namespace Automatic_class_schedule.Solver;

/// <summary>
/// 基于 Google OR-Tools CP-SAT 的整体式排课求解器。
/// 设计思路：以班级课表为整体建模，而非逐课程独立放置。
/// 核心约束来自人工课表分析：
///   - 主科(语数英)每天上午最多1节，多出排下午
///   - 同科目绝不连排（相邻节次不同科）
///   - 第1节在一周内轮换（语/数/英交替）
///   - 体育/副科尽量排下午
///   - 教师时间不冲突
/// </summary>
public sealed class CpSatScheduleSolver : IScheduleSolver
{
    private readonly ConflictService _conflictService = new();

    private static readonly HashSet<string> MainSubjects = new() { "语文", "数学", "英语" };
    private static readonly HashSet<string> AfternoonSubjects = new() { "音乐", "美术", "信息", "劳动" };
    /// <summary>第3节允许的非主科科目：文科+理科+信息</summary>
    private static readonly HashSet<string> Period3Allowed = new() { "物理", "化学", "生物", "历史", "地理", "道德", "信息" };

    public ScheduleResult Solve(ScheduleProblem problem, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        return SolveInternal(problem, Array.Empty<LockedLesson>(), progress, ct);
    }

    public ScheduleResult SolveWithLocks(ScheduleProblem problem, List<LockedLesson> locks, IProgress<double>? progress = null, CancellationToken ct = default, int relaxLevel = 0)
    {
        return SolveInternal(problem, locks, progress, ct, relaxLevel);
    }

    private ScheduleResult SolveInternal(ScheduleProblem problem, IReadOnlyCollection<LockedLesson> locks, IProgress<double>? progress, CancellationToken ct, int relaxLevel = 0)
    {
        bool relaxConsecutiveDays = relaxLevel >= 1;
        progress?.Report(0.05);

        int days = problem.Settings.DaysPerWeek;
        int periods = problem.Settings.PeriodsPerDay;
        int morning = problem.Settings.MorningPeriods; // 上午节次数(通常4)

        var fixedSlots = BuildFixedSlots(problem);
        var lockedMap = locks.GroupBy(x => x.RequirementId).ToDictionary(x => x.Key, x => x.Count());
        var requirements = problem.Requirements.ToList();

        // ========== 容量预检 ==========
        var preConflicts = ValidateCapacity(problem, requirements, fixedSlots, days, periods);
        bool hasOverflow = preConflicts.Any(c => c.Severity == ScheduleConflictSeverity.Hard);

        progress?.Report(0.1);

        // ========== 构建 CP-SAT 模型 ==========
        CpModel model = new();

        // 决策变量: x[r, d, p] = 1 表示需求 r 在第 d 天第 p 节上课
        int reqCount = requirements.Count;
        var x = new BoolVar[reqCount, days, periods + 1]; // period 1-based
        for (int r = 0; r < reqCount; r++)
            for (int d = 0; d < days; d++)
                for (int p = 1; p <= periods; p++)
                    x[r, d, p] = model.NewBoolVar($"x_{r}_{d}_{p}");

        progress?.Report(0.15);

        // 软约束目标（提前声明，C9放松模式需要）
        List<LinearExpr> objTerms = new();
        List<int> objWeights = new();

        // ==================== 硬约束 ====================

        // H1: 课时数约束
        for (int r = 0; r < reqCount; r++)
        {
            int lockedCount = lockedMap.TryGetValue(requirements[r].Id, out int lc) ? lc : 0;
            int needed = Math.Max(0, requirements[r].WeeklyCount - lockedCount);

            List<ILiteral> all = new();
            for (int d = 0; d < days; d++)
                for (int p = 1; p <= periods; p++)
                    all.Add(x[r, d, p]);

            if (hasOverflow)
                model.Add(LinearExpr.Sum(all) <= needed);
            else
                model.Add(LinearExpr.Sum(all) == needed);
        }

        // H2: 班级时间槽互斥 — 每个班级每个时间槽最多1节课
        var classGroups = requirements
            .Select((req, idx) => (req, idx))
            .GroupBy(t => t.req.ClassId)
            .ToList();

        foreach (var group in classGroups)
        {
            var indices = group.Select(t => t.idx).ToList();
            for (int d = 0; d < days; d++)
                for (int p = 1; p <= periods; p++)
                    model.AddAtMostOne(indices.Select(i => (ILiteral)x[i, d, p]).ToList());
        }

        // H3: 教师时间槽互斥（体育教师允许同年级相邻班号连班，最多2班）
        var teacherGroups = requirements
            .Select((req, idx) => (req, idx))
            .Where(t => t.req.TeacherId != Guid.Empty)
            .GroupBy(t => t.req.TeacherId)
            .ToList();

        foreach (var group in teacherGroups)
        {
            var indices = group.Select(t => t.idx).ToList();
            bool isPE = group.All(t => t.req.Subject == "体育");

            if (!isPE)
            {
                for (int d = 0; d < days; d++)
                    for (int p = 1; p <= periods; p++)
                        model.AddAtMostOne(indices.Select(i => (ILiteral)x[i, d, p]).ToList());
            }
            else
            {
                // 体育教师：允许同时段最多2班（同年级相邻连号）
                for (int d = 0; d < days; d++)
                    for (int p = 1; p <= periods; p++)
                        model.Add(LinearExpr.Sum(indices.Select(i => (ILiteral)x[i, d, p]).ToList()) <= 2);

                // 禁止非相邻班号的班级同时段（班号差>1不允许，3连班自然由相邻对组成）
                var peItems = group.ToList();
                for (int a = 0; a < peItems.Count; a++)
                {
                    for (int b = a + 1; b < peItems.Count; b++)
                    {
                        if (!AreConsecutiveClasses(peItems[a].req.ClassName, peItems[b].req.ClassName))
                        {
                            int ia = peItems[a].idx, ib = peItems[b].idx;
                            for (int d = 0; d < days; d++)
                                for (int p = 1; p <= periods; p++)
                                    model.Add(x[ia, d, p] + x[ib, d, p] <= 1);
                        }
                        else
                        {
                            // 连号班允许同时段，但加软约束惩罚以减少连班概率（轻度惩罚，不影响可行性）
                            int ia = peItems[a].idx, ib = peItems[b].idx;
                            for (int d = 0; d < days; d++)
                                for (int p = 1; p <= periods; p++)
                                {
                                    var overlap = model.NewBoolVar($"pe_overlap_{ia}_{ib}_{d}_{p}");
                                    model.Add(x[ia, d, p] + x[ib, d, p] == 2).OnlyEnforceIf(overlap);
                                    model.Add(x[ia, d, p] + x[ib, d, p] <= 1).OnlyEnforceIf(overlap.Not());
                                    objTerms.Add(overlap);
                                    objWeights.Add(-3);
                                }
                        }
                    }
                }
            }
        }

        // H4: 固定课程占位
        for (int r = 0; r < reqCount; r++)
            for (int d = 0; d < days; d++)
                for (int p = 1; p <= periods; p++)
                    if (IsSlotBlocked(fixedSlots, requirements[r], d, p))
                        model.Add(x[r, d, p] == 0);

        // H5: 锁定课程强制放置
        foreach (var locked in locks)
        {
            int rIdx = requirements.FindIndex(rq => rq.Id == locked.RequirementId);
            if (rIdx < 0) continue;
            if (locked.DayIndex >= 0 && locked.DayIndex < days && locked.PeriodIndex >= 1 && locked.PeriodIndex <= periods)
                model.Add(x[rIdx, locked.DayIndex, locked.PeriodIndex] == 1);
        }

        progress?.Report(0.25);

        // ==================== 整体式分布约束（以班级为单位） ====================

        foreach (var group in classGroups)
        {
            var classReqs = group.ToList();

            // 按科目分组（整体视角）
            var bySubject = classReqs.GroupBy(t => t.req.Subject).ToList();

            foreach (var subjGroup in bySubject)
            {
                string subject = subjGroup.Key;
                var subjIndices = subjGroup.Select(t => t.idx).ToList();
                int totalWeekly = subjGroup.Sum(t => t.req.WeeklyCount);
                bool isMain = MainSubjects.Contains(subject);

                for (int d = 0; d < days; d++)
                {
                    // 收集该班级该科目当天所有时间槽的变量
                    List<ILiteral> morningVars = new();
                    List<ILiteral> afternoonVars = new();
                    List<ILiteral> allDayVars = new();

                    for (int p = 1; p <= periods; p++)
                    {
                        foreach (int idx in subjIndices)
                        {
                            allDayVars.Add(x[idx, d, p]);
                            if (p <= morning)
                                morningVars.Add(x[idx, d, p]);
                            else
                                afternoonVars.Add(x[idx, d, p]);
                        }
                    }

                    // C1: 主科每天上午最多1节（核心约束！）
                    if (isMain)
                    {
                        model.Add(LinearExpr.Sum(morningVars) <= 1);
                    }

                    // C2: 每天每科最多2节（任何科目都不应在同一天出现3+次）
                    model.Add(LinearExpr.Sum(allDayVars) <= 2);

                    // C3: 周课时<=天数的科目，每天最多1节（强制散开）
                    if (totalWeekly <= days && totalWeekly >= 2)
                    {
                        model.Add(LinearExpr.Sum(allDayVars) <= 1);
                    }
                }

                // C4: 同科目绝不连排（对ALL科目生效）
                // 同一天相邻节次不能都是同一科目
                for (int d = 0; d < days; d++)
                {
                    for (int p = 1; p < periods; p++)
                    {
                        // 收集 p 和 p+1 节中属于该科目的所有变量
                        List<ILiteral> slotP = subjIndices.Select(idx => (ILiteral)x[idx, d, p]).ToList();
                        List<ILiteral> slotP1 = subjIndices.Select(idx => (ILiteral)x[idx, d, p + 1]).ToList();

                        // 如果 p 节有该科目 且 p+1 节也有该科目 → 禁止
                        // 即: sum(slotP) + sum(slotP1) <= 1
                        List<ILiteral> combined = new();
                        combined.AddRange(slotP);
                        combined.AddRange(slotP1);
                        model.Add(LinearExpr.Sum(combined) <= 1);
                    }
                }

                // C6: 体育只能排第4节及之后（硬约束）
                if (subject == "体育")
                {
                    for (int d = 0; d < days; d++)
                        for (int p = 1; p < Math.Min(4, periods + 1); p++)
                            foreach (int idx in subjIndices)
                                model.Add(x[idx, d, p] == 0);
                }

                // C8: 前两节只排主科（relaxLevel>=1时降级为软约束，relaxLevel>=2惩罚更低）
                if (!isMain)
                {
                    if (relaxConsecutiveDays)
                    {
                        int c8Penalty = relaxLevel >= 2 ? -10 : -20;
                        for (int d = 0; d < days; d++)
                            for (int p = 1; p <= Math.Min(2, periods); p++)
                                foreach (int idx in subjIndices)
                                {
                                    objTerms.Add(x[idx, d, p]);
                                    objWeights.Add(c8Penalty);
                                }
                    }
                    else
                    {
                        for (int d = 0; d < days; d++)
                            for (int p = 1; p <= Math.Min(2, periods); p++)
                                foreach (int idx in subjIndices)
                                    model.Add(x[idx, d, p] == 0);
                    }
                }

                // C9: 连天约束（relaxLevel>=1时降级为软约束，relaxLevel>=2副科进一步放宽）
                if (totalWeekly == 2 && days >= 3)
                {
                    // 每周2节的科目不能出现在连续两天
                    List<ILiteral> dayHasSubj = new();
                    for (int d = 0; d < days; d++)
                    {
                        List<ILiteral> dayVars = new();
                        foreach (int idx in subjIndices)
                            for (int p = 1; p <= periods; p++)
                                dayVars.Add(x[idx, d, p]);
                        var hasDay = model.NewBoolVar($"day_{subject}_{group.Key}_{d}");
                        model.Add(LinearExpr.Sum(dayVars) >= 1).OnlyEnforceIf(hasDay);
                        model.Add(LinearExpr.Sum(dayVars) == 0).OnlyEnforceIf(hasDay.Not());
                        dayHasSubj.Add(hasDay);
                    }
                    if (relaxConsecutiveDays)
                    {
                        // 软约束：违反时惩罚（副科在relaxLevel>=2时惩罚更小）
                        int penalty = (!isMain && relaxLevel >= 2) ? -2 : -5;
                        for (int d = 0; d < days - 1; d++)
                        {
                            var viol = model.NewBoolVar($"viol2_{subject}_{group.Key}_{d}");
                            model.Add(LinearExpr.Sum(new ILiteral[] { dayHasSubj[d], dayHasSubj[d + 1] }) == 2).OnlyEnforceIf(viol);
                            model.Add(LinearExpr.Sum(new ILiteral[] { dayHasSubj[d], dayHasSubj[d + 1] }) <= 1).OnlyEnforceIf(viol.Not());
                            objTerms.Add(viol);
                            objWeights.Add(penalty);
                        }
                    }
                    else
                    {
                        for (int d = 0; d < days - 1; d++)
                            model.Add(LinearExpr.Sum(new ILiteral[] { dayHasSubj[d], dayHasSubj[d + 1] }) <= 1);
                    }

                    // C9b: 副科第一天必须在周一到周三（relaxLevel>=2时作为软约束）
                    if (!isMain && relaxLevel >= 2 && days >= 4)
                    {
                        // 如果副科只在周四/周五出现（day0-2都没有），惩罚
                        var appearsLate = model.NewBoolVar($"late_{subject}_{group.Key}");
                        model.Add(LinearExpr.Sum(dayHasSubj.Take(3).ToArray()) == 0).OnlyEnforceIf(appearsLate);
                        model.Add(LinearExpr.Sum(dayHasSubj.Take(3).ToArray()) >= 1).OnlyEnforceIf(appearsLate.Not());
                        objTerms.Add(appearsLate);
                        objWeights.Add(-3);
                    }
                }
                else if (totalWeekly == 3 && days >= 4)
                {
                    // 每周3节的科目最多1对连天（即最多2天连续）
                    List<ILiteral> dayHasSubj3 = new();
                    for (int d = 0; d < days; d++)
                    {
                        List<ILiteral> dayVars = new();
                        foreach (int idx in subjIndices)
                            for (int p = 1; p <= periods; p++)
                                dayVars.Add(x[idx, d, p]);
                        var hasDay = model.NewBoolVar($"day3_{subject}_{group.Key}_{d}");
                        model.Add(LinearExpr.Sum(dayVars) >= 1).OnlyEnforceIf(hasDay);
                        model.Add(LinearExpr.Sum(dayVars) == 0).OnlyEnforceIf(hasDay.Not());
                        dayHasSubj3.Add(hasDay);
                    }
                    List<ILiteral> consecutivePairs = new();
                    for (int d = 0; d < days - 1; d++)
                    {
                        var pair = model.NewBoolVar($"pair3_{subject}_{group.Key}_{d}");
                        model.Add(LinearExpr.Sum(new ILiteral[] { dayHasSubj3[d], dayHasSubj3[d + 1] }) == 2).OnlyEnforceIf(pair);
                        model.Add(LinearExpr.Sum(new ILiteral[] { dayHasSubj3[d], dayHasSubj3[d + 1] }) <= 1).OnlyEnforceIf(pair.Not());
                        consecutivePairs.Add(pair);
                    }
                    if (relaxConsecutiveDays)
                    {
                        // 软约束：允许超过1对但惩罚
                        var excess = model.NewIntVar(0, days, $"excess3_{subject}_{group.Key}");
                        model.Add(LinearExpr.Sum(consecutivePairs) <= 1 + excess);
                        objTerms.Add(excess);
                        objWeights.Add(-5);
                    }
                    else
                    {
                        model.Add(LinearExpr.Sum(consecutivePairs) <= 1);
                    }
                }
            }

            // C5: 前三节多样性 — 一周内同一节次不应全是同一科目
            // 对主科: 一周内P1/P2/P3各最多出现 ceil(days*0.6) 次
            foreach (var subjGroup in bySubject.Where(g => MainSubjects.Contains(g.Key)))
            {
                var subjIndices = subjGroup.Select(t => t.idx).ToList();

                // P1多样性
                List<ILiteral> p1Vars = new();
                for (int d = 0; d < days; d++)
                    foreach (int idx in subjIndices)
                        p1Vars.Add(x[idx, d, 1]);
                model.Add(LinearExpr.Sum(p1Vars) <= (int)Math.Ceiling(days * 0.6));

                // P2多样性
                List<ILiteral> p2Vars = new();
                for (int d = 0; d < days; d++)
                    foreach (int idx in subjIndices)
                        p2Vars.Add(x[idx, d, 2]);
                model.Add(LinearExpr.Sum(p2Vars) <= (int)Math.Ceiling(days * 0.6));

                // P3多样性
                if (periods >= 3)
                {
                    List<ILiteral> p3Vars = new();
                    for (int d = 0; d < days; d++)
                        foreach (int idx in subjIndices)
                            p3Vars.Add(x[idx, d, 3]);
                    model.Add(LinearExpr.Sum(p3Vars) <= (int)Math.Ceiling(days * 0.6));
                }

                // C7: 第1-2节多样性 — 同一主科不应每天都占据1-2节
                // 一周内第1-2节最多出现 ceil(days*0.8) 天（5天中最多4天）
                List<ILiteral> top2Vars = new();
                for (int d = 0; d < days; d++)
                {
                    List<ILiteral> dayTop2 = new();
                    foreach (int idx in subjIndices)
                    {
                        dayTop2.Add(x[idx, d, 1]);
                        dayTop2.Add(x[idx, d, 2]);
                    }
                    var dayHasTop2 = model.NewBoolVar($"top2_{subjGroup.Key}_{d}");
                    model.Add(LinearExpr.Sum(dayTop2) >= 1).OnlyEnforceIf(dayHasTop2);
                    model.Add(LinearExpr.Sum(dayTop2) == 0).OnlyEnforceIf(dayHasTop2.Not());
                    top2Vars.Add(dayHasTop2);
                }
                model.Add(LinearExpr.Sum(top2Vars) <= (int)Math.Ceiling(days * 0.8));
            }
        }

        progress?.Report(0.4);

        // ==================== 软约束（优化目标） ====================

        foreach (var group in classGroups)
        {
            var classReqs = group.ToList();
            var bySubject = classReqs.GroupBy(t => t.req.Subject).ToList();

            foreach (var subjGroup in bySubject)
            {
                string subject = subjGroup.Key;
                var subjIndices = subjGroup.Select(t => t.idx).ToList();
                bool isMain = MainSubjects.Contains(subject);
                bool isAfternoon = AfternoonSubjects.Contains(subject);

                foreach (int idx in subjIndices)
                {
                    for (int d = 0; d < days; d++)
                    {
                        for (int p = 1; p <= periods; p++)
                        {
                            if (isMain)
                            {
                                // 主科：1-2节高分，第3节中高分，第4节中分，下午低分
                                if (p <= 2)
                                {
                                    objTerms.Add(x[idx, d, p]);
                                    objWeights.Add(8);
                                }
                                else if (p == 3)
                                {
                                    // 第3节主科保持较高奖励，避免全被副科占据
                                    objTerms.Add(x[idx, d, p]);
                                    objWeights.Add(8);
                                }
                                else if (p <= morning)
                                {
                                    objTerms.Add(x[idx, d, p]);
                                    objWeights.Add(7);
                                }
                                else
                                {
                                    objTerms.Add(x[idx, d, p]);
                                    objWeights.Add(2);
                                }
                            }
                            else if (subject == "体育")
                            {
                                // 体育：第4节及之后都可以，偏好下午
                                if (p > morning)
                                {
                                    objTerms.Add(x[idx, d, p]);
                                    objWeights.Add(15);
                                }
                                else
                                {
                                    // p>=4 但仍在上午（第4节）
                                    objTerms.Add(x[idx, d, p]);
                                    objWeights.Add(10);
                                }
                            }
                            else if (isAfternoon)
                            {
                                // 音/美/信/劳：下午偏好，上午中性
                                if (p > morning)
                                {
                                    objTerms.Add(x[idx, d, p]);
                                    objWeights.Add(10);
                                }
                                else if (p == 3 && Period3Allowed.Contains(subject))
                                {
                                    // 信息可排第3节（低奖励，自然只有0-2天）
                                    objTerms.Add(x[idx, d, p]);
                                    objWeights.Add(2);
                                }
                            }
                            else
                            {
                                // 其他副科（道/历/地/生/物/化）：上午中性，下午微偏好
                                if (p > morning)
                                {
                                    objTerms.Add(x[idx, d, p]);
                                    objWeights.Add(2);
                                }
                                else if (p == morning)
                                {
                                    // 第4节小奖励，鼓励填满
                                    objTerms.Add(x[idx, d, p]);
                                    objWeights.Add(4);
                                }
                                else if (p == 3 && Period3Allowed.Contains(subject))
                                {
                                    // 第3节文科/理科低奖励（自然只有0-2天）
                                    objTerms.Add(x[idx, d, p]);
                                    objWeights.Add(2);
                                }
                            }
                        }
                    }
                }

                // 主科第1节轮换奖励：不同天主科第1节加分（鼓励多样性）
                if (isMain)
                {
                    foreach (int idx in subjIndices)
                        for (int d = 0; d < days; d++)
                        {
                            objTerms.Add(x[idx, d, 1]);
                            objWeights.Add(2); // 小奖励，配合硬约束C5
                        }
                }
            }
        }

        // 避免最后一节：所有科目轻微惩罚
        for (int r = 0; r < reqCount; r++)
        {
            if (requirements[r].AvoidLastPeriod)
            {
                for (int d = 0; d < days; d++)
                {
                    objTerms.Add(x[r, d, periods]);
                    objWeights.Add(-15);
                }
            }
        }

        progress?.Report(0.55);

        // ==================== 求解 ====================
        model.Maximize(LinearExpr.WeightedSum(objTerms.ToArray(), objWeights.ToArray()));

        CpSolver solver = new();
        int timeLimit = relaxLevel >= 1 ? 15 : 30;
        solver.StringParameters = $"max_time_in_seconds:{timeLimit};num_workers:4;random_seed:42;";

        progress?.Report(0.6);
        var callback = new ProgressCallback(progress, ct);
        CpSolverStatus status = solver.Solve(model, callback);

        ct.ThrowIfCancellationRequested();
        progress?.Report(0.9);

        // ==================== 提取结果 ====================
        ScheduleResult result = new();
        PlaceFixedLessons(problem, result);

        foreach (var locked in locks)
        {
            LessonRequirement? req = requirements.FirstOrDefault(rq => rq.Id == locked.RequirementId);
            if (req is null) continue;
            result.Entries.Add(CreateEntry(req, locked.DayIndex, locked.PeriodIndex, locked.EntryId ?? Guid.NewGuid(), true, "锁定课程"));
        }

        if (status is CpSolverStatus.Optimal or CpSolverStatus.Feasible)
        {
            for (int r = 0; r < reqCount; r++)
                for (int d = 0; d < days; d++)
                    for (int p = 1; p <= periods; p++)
                        if (solver.BooleanValue(x[r, d, p]))
                            result.Entries.Add(CreateEntry(requirements[r], d, p, Guid.NewGuid(), false, null));
        }
        else
        {
            result.Conflicts.AddRange(preConflicts);
            result.Conflicts.Add(new ScheduleConflict
            {
                Severity = ScheduleConflictSeverity.Warning,
                Type = ScheduleConflictType.UnscheduledLesson,
                Message = "CP-SAT 未找到可行解，请调整约束或手动排课",
                Scope = "全局"
            });
            progress?.Report(1.0);
            return result;
        }

        result.Conflicts.AddRange(preConflicts);
        result.Conflicts.AddRange(_conflictService.Analyze(problem, result.Entries));
        progress?.Report(1.0);
        return result;
    }

    #region 辅助方法

    private static List<ScheduleConflict> ValidateCapacity(
        ScheduleProblem problem, List<LessonRequirement> requirements,
        HashSet<(int Day, int Period, string Scope)> fixedSlots, int days, int periods)
    {
        List<ScheduleConflict> conflicts = new();
        int totalSlots = days * periods;

        // 班级容量检查
        foreach (var group in requirements.GroupBy(r => r.ClassId))
        {
            string className = group.First().ClassName;
            int requiredLessons = group.Sum(r => r.WeeklyCount);
            int fixedOccupied = CountFixedSlotsForClass(fixedSlots, className, days, periods);
            int available = totalSlots - fixedOccupied;

            if (requiredLessons > available)
            {
                conflicts.Add(new ScheduleConflict
                {
                    Severity = ScheduleConflictSeverity.Hard,
                    Type = ScheduleConflictType.UnscheduledLesson,
                    Message = $"{className} 课程超出：需要 {requiredLessons} 节，可用槽位仅 {available} 节（总{totalSlots} - 固定课{fixedOccupied}），超出 {requiredLessons - available} 节",
                    Scope = className,
                    Target = className
                });
            }
        }

        // 教师负荷检查
        foreach (var group in requirements.Where(r => r.TeacherId != Guid.Empty).GroupBy(r => r.TeacherId))
        {
            string teacherName = group.First().TeacherName;
            int totalLoad = group.Sum(r => r.WeeklyCount);

            if (totalLoad > totalSlots)
            {
                string classes = string.Join("、", group.Select(r => r.ClassName).Distinct());
                conflicts.Add(new ScheduleConflict
                {
                    Severity = ScheduleConflictSeverity.Hard,
                    Type = ScheduleConflictType.TeacherConflict,
                    Message = $"{teacherName} 课程冲突：周课时总量 {totalLoad} 节超过可用时间槽 {totalSlots} 节（涉及班级：{classes}）",
                    Scope = teacherName,
                    Target = teacherName
                });
            }
        }

        return conflicts;
    }

    private static int CountFixedSlotsForClass(HashSet<(int Day, int Period, string Scope)> fixedSlots, string className, int days, int periods)
    {
        int count = 0;
        foreach (var (day, period, scope) in fixedSlots)
        {
            if (day < 0 || day >= days || period < 1 || period > periods) continue;
            if (string.IsNullOrWhiteSpace(scope) || scope == "全校") { count++; continue; }

            string[] parts = scope.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (string part in parts)
            {
                string grade = part.EndsWith("年级") ? part : part + "年级";
                if (className.StartsWith(grade, StringComparison.OrdinalIgnoreCase)) { count++; break; }
            }
        }
        return count;
    }

    private static HashSet<(int Day, int Period, string Scope)> BuildFixedSlots(ScheduleProblem problem)
    {
        var slots = new HashSet<(int, int, string)>();
        foreach (FixedLesson fl in problem.FixedLessons)
        {
            int day = Math.Max(0, fl.DayIndex - 1);
            slots.Add((day, fl.PeriodIndex, fl.ScopeValue ?? "全校"));
        }
        return slots;
    }

    /// <summary>判断两个班级是否同年级且班号相邻（如“七1班”和“七2班”）</summary>
    private static bool AreConsecutiveClasses(string classA, string classB)
    {
        var (gradeA, numA) = ParseClassName(classA);
        var (gradeB, numB) = ParseClassName(classB);
        if (gradeA != gradeB || numA < 0 || numB < 0) return false;
        return Math.Abs(numA - numB) == 1;
    }
    
    /// <summary>判断两个班级是否同年级且班号差≤maxDiff</summary>
    private static bool AreCloseClasses(string classA, string classB, int maxDiff)
    {
        var (gradeA, numA) = ParseClassName(classA);
        var (gradeB, numB) = ParseClassName(classB);
        if (gradeA != gradeB || numA < 0 || numB < 0) return false;
        return Math.Abs(numA - numB) <= maxDiff;
    }

    private static (string Grade, int Number) ParseClassName(string className)
    {
        // 格式: "七1班" / "八2班" / "九3班"
        if (string.IsNullOrEmpty(className)) return ("", -1);
        string trimmed = className.Replace("班", "");
        if (trimmed.Length < 2) return ("", -1);
        // 提取年级部分（非数字前缀）和班号（数字部分）
        int i = 0;
        while (i < trimmed.Length && !char.IsDigit(trimmed[i])) i++;
        if (i == 0 || i >= trimmed.Length) return ("", -1);
        string grade = trimmed[..i];
        if (int.TryParse(trimmed[i..], out int num))
            return (grade, num);
        return (grade, -1);
    }

    private static bool IsSlotBlocked(HashSet<(int Day, int Period, string Scope)> fixedSlots, LessonRequirement req, int day, int period)
    {
        foreach (var (fd, fp, scope) in fixedSlots)
        {
            if (fd != day || fp != period) continue;
            if (string.IsNullOrWhiteSpace(scope) || scope == "全校") return true;

            string[] parts = scope.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (string part in parts)
            {
                string grade = part.EndsWith("年级") ? part : part + "年级";
                if (req.ClassName.StartsWith(grade, StringComparison.OrdinalIgnoreCase)) return true;
            }
        }
        return false;
    }

    private static void PlaceFixedLessons(ScheduleProblem problem, ScheduleResult result)
    {
        foreach (FixedLesson fl in problem.FixedLessons)
        {
            IEnumerable<SchoolClass> affected = GetAffectedClasses(problem.Classes, fl.ScopeValue);
            foreach (SchoolClass cls in affected)
            {
                result.Entries.Add(new ScheduleEntry
                {
                    Id = Guid.NewGuid(),
                    RequirementId = Guid.Empty,
                    ClassId = cls.Id,
                    TeacherId = Guid.Empty,
                    ClassName = cls.DisplayName,
                    TeacherName = fl.TeacherName,
                    Subject = fl.Subject,
                    DayIndex = Math.Max(0, fl.DayIndex - 1),
                    PeriodIndex = fl.PeriodIndex,
                    Locked = true,
                    IsFixed = true,
                    Note = fl.Reason
                });
            }
        }
    }

    private static IEnumerable<SchoolClass> GetAffectedClasses(IReadOnlyList<SchoolClass> classes, string scopeValue)
    {
        if (string.IsNullOrWhiteSpace(scopeValue) || scopeValue == "全校") return classes;
        string[] parts = scopeValue.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        HashSet<string> grades = new();
        foreach (string part in parts)
            grades.Add(part.EndsWith("年级") ? part : part + "年级");
        return classes.Where(c => grades.Contains(c.GradeName));
    }

    private static ScheduleEntry CreateEntry(LessonRequirement req, int dayIndex, int periodIndex, Guid id, bool locked, string? note)
    {
        return new ScheduleEntry
        {
            Id = id,
            RequirementId = req.Id,
            ClassId = req.ClassId,
            TeacherId = req.TeacherId,
            ClassName = req.ClassName,
            TeacherName = req.TeacherName,
            Subject = req.Subject,
            DayIndex = dayIndex,
            PeriodIndex = periodIndex,
            Locked = locked,
            IsFixed = false,
            Note = note ?? string.Empty
        };
    }

    #endregion

    private sealed class ProgressCallback : CpSolverSolutionCallback
    {
        private readonly IProgress<double>? _progress;
        private readonly CancellationToken _ct;
        private int _count;

        public ProgressCallback(IProgress<double>? progress, CancellationToken ct)
        {
            _progress = progress;
            _ct = ct;
        }

        public override void OnSolutionCallback()
        {
            _count++;
            _progress?.Report(0.6 + Math.Min(0.3, _count * 0.03));
            if (_ct.IsCancellationRequested) StopSearch();
        }
    }
}
