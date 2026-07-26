using Automatic_class_schedule.Models;
using Automatic_class_schedule.Services;
using Google.OrTools.Sat;

namespace Automatic_class_schedule.Solver;

/// <summary>
/// 基于 Google OR-Tools CP-SAT 的排课求解器。
/// 硬约束：班级/教师时间冲突、固定课程占位、每周课时数满足。
/// 软约束（优化目标）：均匀分布、上午偏好、首节多样性、避免连排、下午体育等。
/// </summary>
public sealed class CpSatScheduleSolver : IScheduleSolver
{
    private readonly ConflictService _conflictService = new();

    // 主科集合：允许偶尔连排
    private static readonly HashSet<string> MainSubjects = new() { "语文", "数学", "英语" };
    // 绝不连排的科目
    private static readonly HashSet<string> NoConsecutiveSubjects = new() { "物理", "化学", "体育", "生物" };
    // 下午优先科目
    private static readonly HashSet<string> AfternoonSubjects = new() { "体育" };
    // 偏周五/下午的副科
    private static readonly HashSet<string> PreferFridaySubjects = new() { "美术", "音乐", "信息" };

    public ScheduleResult Solve(ScheduleProblem problem, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        return SolveInternal(problem, Array.Empty<LockedLesson>(), progress, ct);
    }

    public ScheduleResult SolveWithLocks(ScheduleProblem problem, List<LockedLesson> locks, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        return SolveInternal(problem, locks, progress, ct);
    }

    private ScheduleResult SolveInternal(ScheduleProblem problem, IReadOnlyCollection<LockedLesson> locks, IProgress<double>? progress, CancellationToken ct)
    {
        progress?.Report(0.05);

        int days = problem.Settings.DaysPerWeek;
        int periods = problem.Settings.PeriodsPerDay;
        int morningPeriods = problem.Settings.MorningPeriods;

        // 预放置固定课程，构建被占用的时间槽
        var fixedSlots = BuildFixedSlots(problem);
        var lockedMap = BuildLockedMap(locks, problem);

        // 需要排课的需求列表（排除已被锁定完全覆盖的）
        var requirements = problem.Requirements.ToList();

        progress?.Report(0.1);

        // ========== 构建 CP-SAT 模型 ==========
        CpModel model = new();

        // 决策变量: x[r, d, p] = 1 表示需求 r 在周 d 第 p 节有一节课
        // r: requirement index, d: 0-based day, p: 1-based period
        var x = new BoolVar[requirements.Count, days, periods + 1];
        for (int r = 0; r < requirements.Count; r++)
        {
            for (int d = 0; d < days; d++)
            {
                for (int p = 1; p <= periods; p++)
                {
                    x[r, d, p] = model.NewBoolVar($"x_{r}_{d}_{p}");
                }
            }
        }

        progress?.Report(0.15);

        // ========== 硬约束 ==========

        // H1: 每个需求的总课时 == WeeklyCount（减去已锁定数量）
        for (int r = 0; r < requirements.Count; r++)
        {
            LessonRequirement req = requirements[r];
            int lockedCount = lockedMap.TryGetValue(req.Id, out int lc) ? lc : 0;
            int needed = Math.Max(0, req.WeeklyCount - lockedCount);

            List<ILiteral> allSlots = new();
            for (int d = 0; d < days; d++)
                for (int p = 1; p <= periods; p++)
                    allSlots.Add(x[r, d, p]);

            model.Add(LinearExpr.Sum(allSlots) == needed);
        }

        // H2: 每个班级在每个时间槽最多一节课
        var classGroups = requirements
            .Select((req, idx) => (req, idx))
            .GroupBy(t => t.req.ClassId)
            .ToList();

        foreach (var group in classGroups)
        {
            var indices = group.Select(t => t.idx).ToList();
            for (int d = 0; d < days; d++)
            {
                for (int p = 1; p <= periods; p++)
                {
                    List<ILiteral> slotVars = indices.Select(i => (ILiteral)x[i, d, p]).ToList();
                    model.AddAtMostOne(slotVars);
                }
            }
        }

        // H3: 每个教师在每个时间槽最多一节课（体育教师除外——支持合班上课）
        var teacherGroups = requirements
            .Select((req, idx) => (req, idx))
            .Where(t => t.req.TeacherId != Guid.Empty)
            .GroupBy(t => t.req.TeacherId)
            .ToList();

        foreach (var group in teacherGroups)
        {
            // 如果该教师所有课都是体育，则跳过教师冲突约束（允许合班）
            bool isPeTeacher = group.All(t => t.req.Subject == "体育");
            if (isPeTeacher) continue;

            var indices = group.Select(t => t.idx).ToList();
            for (int d = 0; d < days; d++)
            {
                for (int p = 1; p <= periods; p++)
                {
                    List<ILiteral> slotVars = indices.Select(i => (ILiteral)x[i, d, p]).ToList();
                    model.AddAtMostOne(slotVars);
                }
            }
        }

        // H4: 固定课程/锁定课程占位 → 禁止冲突
        for (int r = 0; r < requirements.Count; r++)
        {
            LessonRequirement req = requirements[r];
            for (int d = 0; d < days; d++)
            {
                for (int p = 1; p <= periods; p++)
                {
                    if (IsSlotBlocked(fixedSlots, req, d, p))
                    {
                        model.Add(x[r, d, p] == 0);
                    }
                }
            }
        }

        // H5: 锁定课程强制放置
        foreach (var (reqId, slots) in lockedMap)
        {
            int rIdx = requirements.FindIndex(rq => rq.Id == reqId);
            if (rIdx < 0) continue;
            foreach (var (ld, lp) in GetLockedSlots(locks, reqId))
            {
                if (ld >= 0 && ld < days && lp >= 1 && lp <= periods)
                    model.Add(x[rIdx, ld, lp] == 1);
            }
        }

        progress?.Report(0.25);

        // ========== 分布约束（硬/软结合） ==========

        // D1: 每天每科最多 N 节（防止同一天堆太多）
        for (int r = 0; r < requirements.Count; r++)
        {
            LessonRequirement req = requirements[r];
            int maxPerDay = GetMaxPerDay(req.WeeklyCount, days);

            for (int d = 0; d < days; d++)
            {
                List<ILiteral> dayVars = new();
                for (int p = 1; p <= periods; p++)
                    dayVars.Add(x[r, d, p]);

                model.Add(LinearExpr.Sum(dayVars) <= maxPerDay);
            }
        }

        // D2: 周课时 <= 天数的科目，强制每天最多1节（硬约束保证散开）
        for (int r = 0; r < requirements.Count; r++)
        {
            LessonRequirement req = requirements[r];
            if (req.WeeklyCount <= days && req.WeeklyCount >= 2)
            {
                for (int d = 0; d < days; d++)
                {
                    List<ILiteral> dayVars = new();
                    for (int p = 1; p <= periods; p++)
                        dayVars.Add(x[r, d, p]);
                    model.Add(LinearExpr.Sum(dayVars) <= 1);
                }
            }
        }

        progress?.Report(0.35);

        // ========== 软约束（优化目标） ==========
        List<LinearExpr> objectiveTerms = new();
        List<int> objectiveWeights = new();

        // S1: 上午偏好 — PreferMorning 的科目排在上午加分
        for (int r = 0; r < requirements.Count; r++)
        {
            LessonRequirement req = requirements[r];
            if (!req.PreferMorning) continue;

            for (int d = 0; d < days; d++)
            {
                for (int p = 1; p <= Math.Min(morningPeriods, periods); p++)
                {
                    objectiveTerms.Add(x[r, d, p]);
                    objectiveWeights.Add(8); // 上午加分
                }
            }
        }

        // S2: 避免最后一节
        for (int r = 0; r < requirements.Count; r++)
        {
            LessonRequirement req = requirements[r];
            if (!req.AvoidLastPeriod) continue;

            for (int d = 0; d < days; d++)
            {
                objectiveTerms.Add(x[r, d, periods]);
                objectiveWeights.Add(-20); // 末节惩罚
            }
        }

        // S3: 体育/副科下午优先
        for (int r = 0; r < requirements.Count; r++)
        {
            LessonRequirement req = requirements[r];
            if (!AfternoonSubjects.Contains(req.Subject)) continue;

            for (int d = 0; d < days; d++)
            {
                for (int p = morningPeriods + 1; p <= periods; p++)
                {
                    objectiveTerms.Add(x[r, d, p]);
                    objectiveWeights.Add(12); // 下午加分
                }
                // 上午惩罚
                for (int p = 1; p <= morningPeriods; p++)
                {
                    objectiveTerms.Add(x[r, d, p]);
                    objectiveWeights.Add(-10);
                }
            }
        }

        // S4: 美术/音乐/信息 偏周五下午
        for (int r = 0; r < requirements.Count; r++)
        {
            LessonRequirement req = requirements[r];
            if (!PreferFridaySubjects.Contains(req.Subject)) continue;

            int friday = days - 1; // 最后一天
            for (int p = morningPeriods + 1; p <= periods; p++)
            {
                objectiveTerms.Add(x[r, friday, p]);
                objectiveWeights.Add(10);
            }
        }

        progress?.Report(0.45);

        // S5: 首节多样性 — 同一科目在第1节出现次数过多则惩罚
        // 按科目分组，对每个科目在 period=1 的使用总数施加惩罚
        var subjectGroups = requirements
            .Select((req, idx) => (req, idx))
            .GroupBy(t => t.req.Subject)
            .ToList();

        foreach (var group in subjectGroups)
        {
            var indices = group.Select(t => t.idx).ToList();
            // 对每个班级-科目组合，如果在第1节排了课，给一个小惩罚以鼓励多样性
            // 但如果该科目是主科且preferMorning，则不惩罚（主科本身适合第1节）
            if (group.Key is "语文" or "数学" or "英语") continue;

            foreach (int idx in indices)
            {
                for (int d = 0; d < days; d++)
                {
                    objectiveTerms.Add(x[idx, d, 1]);
                    objectiveWeights.Add(-3); // 非主科排第1节小惩罚
                }
            }
        }

        // S6: 避免连排（非主科）— 同一天相邻节次不能都是同一需求
        for (int r = 0; r < requirements.Count; r++)
        {
            LessonRequirement req = requirements[r];
            if (!NoConsecutiveSubjects.Contains(req.Subject)) continue;

            for (int d = 0; d < days; d++)
            {
                for (int p = 1; p < periods; p++)
                {
                    // x[r,d,p] + x[r,d,p+1] <= 1
                    model.Add(x[r, d, p] + x[r, d, p + 1] <= 1);
                }
            }
        }

        // S7: 主科偶尔连排奖励（语数英允许2节连上，给小奖励）
        for (int r = 0; r < requirements.Count; r++)
        {
            LessonRequirement req = requirements[r];
            if (!MainSubjects.Contains(req.Subject)) continue;
            if (req.WeeklyCount < 6) continue; // 只有课时多的主科才考虑连排

            for (int d = 0; d < days; d++)
            {
                for (int p = 1; p < periods; p++)
                {
                    // 连排奖励（很小，只是轻微鼓励）
                    BoolVar bothVar = model.NewBoolVar($"consec_{r}_{d}_{p}");
                    model.AddBoolAnd(new ILiteral[] { x[r, d, p], x[r, d, p + 1] }).OnlyEnforceIf(bothVar);
                    model.AddBoolOr(new ILiteral[] { x[r, d, p].Not(), x[r, d, p + 1].Not() }).OnlyEnforceIf(bothVar.Not());
                    objectiveTerms.Add(bothVar);
                    objectiveWeights.Add(2);
                }
            }
        }

        progress?.Report(0.55);

        // S8: 同一天课程多样性 — 同一班级同一天同一科目最多出现次数限制（软）
        // 对于课时数>天数的科目（如语7），允许一天2节但惩罚第2节
        foreach (var group in classGroups)
        {
            var classReqs = group.ToList();
            // 按科目分组
            var bySubject = classReqs.GroupBy(t => t.req.Subject).ToList();
            foreach (var subjGroup in bySubject)
            {
                var subjIndices = subjGroup.Select(t => t.idx).ToList();
                if (subjIndices.Count == 0) continue;

                for (int d = 0; d < days; d++)
                {
                    // 统计该班级该科目这天的总节数
                    List<ILiteral> daySubjectVars = new();
                    foreach (int idx in subjIndices)
                        for (int p = 1; p <= periods; p++)
                            daySubjectVars.Add(x[idx, d, p]);

                    // 如果超过1节，给惩罚（鼓励分散）
                    if (daySubjectVars.Count > 1)
                    {
                        // overflow >= sum - 1, overflow >= 0 → 最小化时 overflow = max(0, sum-1)
                        IntVar overflow = model.NewIntVar(0, daySubjectVars.Count - 1, $"ov_{subjGroup.Key}_{d}_{group.Key}");
                        model.Add(overflow >= LinearExpr.Sum(daySubjectVars) - 1);
                        objectiveTerms.Add(overflow);
                        objectiveWeights.Add(-6); // 同天同科目多余惩罚
                    }
                }
            }
        }

        progress?.Report(0.65);

        // S9: 主科上午前2节优先（第1、2节额外加分）
        for (int r = 0; r < requirements.Count; r++)
        {
            LessonRequirement req = requirements[r];
            if (!MainSubjects.Contains(req.Subject) || !req.PreferMorning) continue;

            for (int d = 0; d < days; d++)
            {
                for (int p = 1; p <= Math.Min(2, periods); p++)
                {
                    objectiveTerms.Add(x[r, d, p]);
                    objectiveWeights.Add(5); // 前2节额外加分
                }
            }
        }

        // ========== 求解 ==========
        model.Maximize(LinearExpr.WeightedSum(objectiveTerms.ToArray(), objectiveWeights.ToArray()));

        CpSolver solver = new();
        solver.StringParameters = "max_time_in_seconds:30;num_workers:4;random_seed:42;";

        progress?.Report(0.7);

        // 使用回调报告进度
        var solutionCallback = new ProgressCallback(progress, ct);
        CpSolverStatus status = solver.Solve(model, solutionCallback);

        ct.ThrowIfCancellationRequested();
        progress?.Report(0.9);

        // ========== 提取结果 ==========
        ScheduleResult result = new();

        // 先放入固定课程
        PlaceFixedLessons(problem, result);

        // 放入锁定课程
        foreach (var locked in locks)
        {
            LessonRequirement? req = requirements.FirstOrDefault(rq => rq.Id == locked.RequirementId);
            if (req is null) continue;
            result.Entries.Add(CreateEntry(req, locked.DayIndex, locked.PeriodIndex, locked.EntryId ?? Guid.NewGuid(), true, "锁定课程"));
        }

        if (status is CpSolverStatus.Optimal or CpSolverStatus.Feasible)
        {
            for (int r = 0; r < requirements.Count; r++)
            {
                LessonRequirement req = requirements[r];
                for (int d = 0; d < days; d++)
                {
                    for (int p = 1; p <= periods; p++)
                    {
                        if (solver.BooleanValue(x[r, d, p]))
                        {
                            result.Entries.Add(CreateEntry(req, d, p, Guid.NewGuid(), false, null));
                        }
                    }
                }
            }
        }
        else
        {
            // 求解失败，回退到贪心
            var greedy = new GreedyScheduleSolver();
            return greedy.SolveWithLocks(problem, locks.ToList(), progress, ct);
        }

        // 冲突分析
        result.Conflicts.AddRange(_conflictService.Analyze(problem, result.Entries));
        progress?.Report(1.0);
        return result;
    }

    #region 辅助方法

    /// <summary>计算每天最大课时数</summary>
    private static int GetMaxPerDay(int weeklyCount, int days)
    {
        if (weeklyCount <= days) return 1;
        return (int)Math.Ceiling((double)weeklyCount / days);
    }

    /// <summary>构建固定课程占用表: (dayIndex0Based, periodIndex, className/gradeScope)</summary>
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

    /// <summary>判断某需求在某时间槽是否被固定课程阻挡</summary>
    private static bool IsSlotBlocked(HashSet<(int Day, int Period, string Scope)> fixedSlots, LessonRequirement req, int day, int period)
    {
        foreach (var (fd, fp, scope) in fixedSlots)
        {
            if (fd != day || fp != period) continue;
            if (string.IsNullOrWhiteSpace(scope) || scope == "全校") return true;

            // 支持 "七+八年级" 格式
            string[] parts = scope.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (string part in parts)
            {
                string grade = part.EndsWith("年级") ? part : part + "年级";
                if (req.ClassName.StartsWith(grade, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        return false;
    }

    /// <summary>构建锁定课程映射: requirementId -> lockedCount</summary>
    private static Dictionary<Guid, int> BuildLockedMap(IReadOnlyCollection<LockedLesson> locks, ScheduleProblem problem)
    {
        return locks.GroupBy(x => x.RequirementId).ToDictionary(x => x.Key, x => x.Count());
    }

    /// <summary>获取某需求的所有锁定时间槽</summary>
    private static IEnumerable<(int Day, int Period)> GetLockedSlots(IReadOnlyCollection<LockedLesson> locks, Guid requirementId)
    {
        return locks.Where(l => l.RequirementId == requirementId).Select(l => (l.DayIndex, l.PeriodIndex));
    }

    /// <summary>将固定课程转化为 ScheduleEntry</summary>
    private static void PlaceFixedLessons(ScheduleProblem problem, ScheduleResult result)
    {
        foreach (FixedLesson fl in problem.FixedLessons)
        {
            IEnumerable<SchoolClass> affectedClasses = GetAffectedClasses(problem.Classes, fl.ScopeValue);
            foreach (SchoolClass cls in affectedClasses)
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
        if (string.IsNullOrWhiteSpace(scopeValue) || scopeValue == "全校")
            return classes;

        string[] gradeParts = scopeValue.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        HashSet<string> gradeNames = new();
        foreach (string part in gradeParts)
        {
            string grade = part.EndsWith("年级") ? part : part + "年级";
            gradeNames.Add(grade);
        }
        return classes.Where(c => gradeNames.Contains(c.GradeName));
    }

    private static ScheduleEntry CreateEntry(LessonRequirement requirement, int dayIndex, int periodIndex, Guid id, bool locked, string? note)
    {
        return new ScheduleEntry
        {
            Id = id,
            RequirementId = requirement.Id,
            ClassId = requirement.ClassId,
            TeacherId = requirement.TeacherId,
            ClassName = requirement.ClassName,
            TeacherName = requirement.TeacherName,
            Subject = requirement.Subject,
            DayIndex = dayIndex,
            PeriodIndex = periodIndex,
            Locked = locked,
            IsFixed = locked,
            Note = note ?? string.Empty
        };
    }

    #endregion

    /// <summary>CP-SAT 求解进度回调</summary>
    private sealed class ProgressCallback : CpSolverSolutionCallback
    {
        private readonly IProgress<double>? _progress;
        private readonly CancellationToken _ct;
        private int _solutionCount;

        public ProgressCallback(IProgress<double>? progress, CancellationToken ct)
        {
            _progress = progress;
            _ct = ct;
        }

        public override void OnSolutionCallback()
        {
            _solutionCount++;
            // 在 0.7 ~ 0.9 之间报告进度
            double p = 0.7 + Math.Min(0.2, _solutionCount * 0.02);
            _progress?.Report(p);

            if (_ct.IsCancellationRequested)
            {
                StopSearch();
            }
        }
    }
}
