using Automatic_class_schedule.Models;
using Automatic_class_schedule.Services;

namespace Automatic_class_schedule.Solver;

public sealed class GreedyScheduleSolver : IScheduleSolver
{
    private readonly ConflictService _conflictService = new();

    public ScheduleResult Solve(ScheduleProblem problem, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        return SolveInternal(problem, Array.Empty<LockedLesson>(), progress, ct);
    }

    public ScheduleResult SolveWithLocks(ScheduleProblem problem, List<LockedLesson> locks, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        return SolveInternal(problem, locks, progress, ct);
    }

    private ScheduleResult SolveInternal(ScheduleProblem problem, IReadOnlyCollection<LockedLesson> locks, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        ScheduleResult result = new();
        List<ScheduleEntry> placed = new();
        Dictionary<Guid, LessonRequirement> requirementMap = problem.Requirements.ToDictionary(x => x.Id, x => x);
        Dictionary<Guid, int> lockedCounts = locks.GroupBy(x => x.RequirementId).ToDictionary(x => x.Key, x => x.Count());

        List<LessonInstance> instances = BuildInstances(problem.Requirements, lockedCounts).OrderByDescending(GetPriority).ToList();
        int total = instances.Count;
        int completed = 0;

        foreach (LockedLesson locked in locks)
        {
            ct.ThrowIfCancellationRequested();

            if (!requirementMap.TryGetValue(locked.RequirementId, out LessonRequirement? requirement))
            {
                continue;
            }

            ScheduleEntry entry = CreateEntry(requirement, locked.DayIndex, locked.PeriodIndex, locked.EntryId ?? Guid.NewGuid(), true, "锁定课程");
            IReadOnlyList<ScheduleConflict> conflicts = _conflictService.ValidatePlacement(problem, placed, entry, locked.DayIndex, locked.PeriodIndex);
            if (conflicts.Count > 0)
            {
                result.Conflicts.AddRange(conflicts);
                continue;
            }

            placed.Add(entry);
            result.Entries.Add(entry);
        }

        foreach (LessonInstance instance in instances)
        {
            ct.ThrowIfCancellationRequested();

            LessonRequirement requirement = problem.Requirements.First(x => x.Id == instance.RequirementId);
            ScheduleEntry? bestEntry = FindBestSlot(problem, placed, requirement, instance);
            if (bestEntry is null)
            {
                result.UnscheduledLessons.Add($"{instance.ClassName}-{instance.Subject}-{instance.Sequence}");
                result.Conflicts.Add(new ScheduleConflict
                {
                    Severity = ScheduleConflictSeverity.Warning,
                    Type = ScheduleConflictType.UnscheduledLesson,
                    Message = $"无法安排 {instance.ClassName} {instance.Subject} 第 {instance.Sequence} 节",
                    Scope = instance.ClassName
                });
            }
            else
            {
                placed.Add(bestEntry);
                result.Entries.Add(bestEntry);
            }

            completed++;
            progress?.Report((double)completed / total);
        }

        result.Conflicts.AddRange(_conflictService.Analyze(problem, result.Entries));
        ApplySoftPreferenceConflicts(problem, result);
        return result;
    }

    private ScheduleEntry? FindBestSlot(ScheduleProblem problem, IReadOnlyCollection<ScheduleEntry> placed, LessonRequirement requirement, LessonInstance instance)
    {
        List<(ScheduleEntry Entry, int Score)> candidates = new();

        for (int day = 0; day < problem.Settings.DaysPerWeek; day++)
        {
            for (int period = 1; period <= problem.Settings.PeriodsPerDay; period++)
            {
                ScheduleEntry candidate = CreateEntry(requirement, day, period, Guid.NewGuid(), false, $"第{instance.Sequence}课次");
                IReadOnlyList<ScheduleConflict> conflicts = _conflictService.ValidatePlacement(problem, placed, candidate, day, period);
                if (conflicts.Any(x => x.Severity == ScheduleConflictSeverity.Hard))
                {
                    continue;
                }

                int score = 0;
                if (instance.PreferMorning && period <= Math.Min(2, problem.Settings.PeriodsPerDay))
                {
                    score += 20;
                }

                if (instance.AvoidLastPeriod && period == problem.Settings.PeriodsPerDay)
                {
                    score -= 15;
                }

                if (string.Equals(instance.DistributionRule, "每天一次", StringComparison.OrdinalIgnoreCase) && placed.Any(x => x.ClassId == instance.ClassId && x.DayIndex == day && string.Equals(x.Subject, instance.Subject, StringComparison.OrdinalIgnoreCase)))
                {
                    score -= 25;
                }

                if (period == 1 || period == 2)
                {
                    score += 3;
                }

                candidates.Add((candidate, score));
            }
        }

        return candidates.OrderByDescending(x => x.Score).ThenBy(x => x.Entry.DayIndex).ThenBy(x => x.Entry.PeriodIndex).Select(x => x.Entry).FirstOrDefault();
    }

    private static IEnumerable<LessonInstance> BuildInstances(IEnumerable<LessonRequirement> requirements, IReadOnlyDictionary<Guid, int> lockedCounts)
    {
        foreach (LessonRequirement requirement in requirements)
        {
            int lockedCount = lockedCounts.TryGetValue(requirement.Id, out int count) ? count : 0;
            for (int i = 1; i <= Math.Max(0, requirement.WeeklyCount); i++)
            {
                if (i <= lockedCount)
                {
                    continue;
                }

                yield return new LessonInstance
                {
                    RequirementId = requirement.Id,
                    ClassId = requirement.ClassId,
                    TeacherId = requirement.TeacherId,
                    ClassName = requirement.ClassName,
                    TeacherName = requirement.TeacherName,
                    Subject = requirement.Subject,
                    Sequence = i,
                    DistributionRule = requirement.DistributionRule,
                    PreferMorning = requirement.PreferMorning,
                    AvoidLastPeriod = requirement.AvoidLastPeriod
                };
            }
        }
    }

    private static int GetPriority(LessonInstance instance)
    {
        int priority = 0;
        if (instance.PreferMorning)
        {
            priority += 5;
        }

        if (instance.AvoidLastPeriod)
        {
            priority += 5;
        }

        if (string.Equals(instance.DistributionRule, "每天一次", StringComparison.OrdinalIgnoreCase))
        {
            priority += 10;
        }

        return priority;
    }

    private static ScheduleEntry CreateEntry(LessonRequirement requirement, int dayIndex, int periodIndex, Guid id, bool locked, string? note = null)
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

    private static void ApplySoftPreferenceConflicts(ScheduleProblem problem, ScheduleResult result)
    {
        foreach (ScheduleEntry entry in result.Entries)
        {
            LessonRequirement? requirement = problem.Requirements.FirstOrDefault(x => x.Id == entry.RequirementId);
            if (requirement is null)
            {
                continue;
            }

            if (requirement.PreferMorning && entry.PeriodIndex > Math.Min(2, problem.Settings.PeriodsPerDay))
            {
                result.Conflicts.Add(new ScheduleConflict
                {
                    Severity = ScheduleConflictSeverity.Warning,
                    Type = ScheduleConflictType.PreferenceConflict,
                    Message = $"{entry.ClassName} {entry.Subject} 未排在上午",
                    Scope = entry.SlotLabel
                });
            }

            if (requirement.AvoidLastPeriod && entry.PeriodIndex == problem.Settings.PeriodsPerDay)
            {
                result.Conflicts.Add(new ScheduleConflict
                {
                    Severity = ScheduleConflictSeverity.Warning,
                    Type = ScheduleConflictType.PreferenceConflict,
                    Message = $"{entry.ClassName} {entry.Subject} 位于最后一节",
                    Scope = entry.SlotLabel
                });
            }
        }
    }
}
