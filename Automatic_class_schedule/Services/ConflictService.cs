using Automatic_class_schedule.Models;

namespace Automatic_class_schedule.Services;

public sealed class ConflictService
{
    public IReadOnlyList<ScheduleConflict> Analyze(ScheduleProblem problem, IEnumerable<ScheduleEntry> entries)
    {
        List<ScheduleConflict> conflicts = new();
        List<ScheduleEntry> entryList = entries.ToList();

        foreach (ScheduleEntry entry in entryList)
        {
            if (entry.DayIndex < 0 || entry.DayIndex >= problem.Settings.DaysPerWeek || entry.PeriodIndex < 1 || entry.PeriodIndex > problem.Settings.PeriodsPerDay)
            {
                conflicts.Add(new ScheduleConflict
                {
                    Severity = ScheduleConflictSeverity.Hard,
                    Type = ScheduleConflictType.UnscheduledLesson,
                    Message = $"{entry.Summary} 超出可排课时间范围",
                    Scope = entry.SlotLabel
                });
            }
        }

        foreach (IGrouping<(int Day, int Period), ScheduleEntry> group in entryList.GroupBy(x => (x.DayIndex, x.PeriodIndex)))
        {
            List<ScheduleEntry> sameSlot = group.ToList();
            if (sameSlot.Count <= 1)
            {
                continue;
            }

            foreach (IGrouping<Guid, ScheduleEntry> classGroup in sameSlot.GroupBy(x => x.ClassId))
            {
                if (classGroup.Count() > 1)
                {
                    conflicts.Add(CreateConflict(ScheduleConflictType.ClassConflict, ScheduleConflictSeverity.Hard, classGroup.First(), "同班同一时间存在多节课"));
                }
            }

            foreach (IGrouping<Guid, ScheduleEntry> teacherGroup in sameSlot.GroupBy(x => x.TeacherId))
            {
                // 跳过空TeacherId（固定课程等无教师条目）
                if (teacherGroup.Key == Guid.Empty) continue;
                if (teacherGroup.Count() > 1)
                {
                    conflicts.Add(CreateConflict(ScheduleConflictType.TeacherConflict, ScheduleConflictSeverity.Hard, teacherGroup.First(), "同教师同一时间存在多节课"));
                }
            }
        }

        foreach (ScheduleEntry entry in entryList)
        {
            // 固定课程自身不参与冲突检测
            if (entry.IsFixed) continue;
            FixedLesson? fixedLesson = problem.FixedLessons.FirstOrDefault(x => BlocksSlot(x, entry, entry.DayIndex, entry.PeriodIndex));
            if (fixedLesson is not null)
            {
                conflicts.Add(CreateConflict(ScheduleConflictType.FixedLessonConflict, ScheduleConflictSeverity.Hard, entry, $"{fixedLesson.Reason} 占用该时间"));
            }
        }

        return conflicts;
    }

    public IReadOnlyList<ScheduleConflict> ValidatePlacement(ScheduleProblem problem, IEnumerable<ScheduleEntry> existingEntries, ScheduleEntry candidate, int dayIndex, int periodIndex)
    {
        List<ScheduleConflict> conflicts = new();

        if (dayIndex < 0 || dayIndex >= problem.Settings.DaysPerWeek || periodIndex < 1 || periodIndex > problem.Settings.PeriodsPerDay)
        {
            conflicts.Add(new ScheduleConflict
            {
                Severity = ScheduleConflictSeverity.Hard,
                Type = ScheduleConflictType.UnscheduledLesson,
                Message = "目标时间超出排课范围",
                Scope = $"周{dayIndex + 1} 第{periodIndex}节"
            });
            return conflicts;
        }

        foreach (ScheduleEntry entry in existingEntries)
        {
            if (entry.DayIndex == dayIndex && entry.PeriodIndex == periodIndex)
            {
                if (entry.ClassId == candidate.ClassId)
                {
                    conflicts.Add(CreateConflict(ScheduleConflictType.ClassConflict, ScheduleConflictSeverity.Hard, candidate, "该班级在目标时间已有课程"));
                }

                // 跳过空TeacherId（固定课程等无教师条目）
                if (candidate.TeacherId != Guid.Empty && entry.TeacherId == candidate.TeacherId)
                {
                    conflicts.Add(CreateConflict(ScheduleConflictType.TeacherConflict, ScheduleConflictSeverity.Hard, candidate, "该教师在目标时间已有课程"));
                }
            }
        }

        FixedLesson? fixedLesson = problem.FixedLessons.FirstOrDefault(x => BlocksSlot(x, candidate, dayIndex, periodIndex));
        if (fixedLesson is not null)
        {
            conflicts.Add(CreateConflict(ScheduleConflictType.FixedLessonConflict, ScheduleConflictSeverity.Hard, candidate, fixedLesson.Reason));
        }

        return conflicts;
    }

    private static ScheduleConflict CreateConflict(ScheduleConflictType type, ScheduleConflictSeverity severity, ScheduleEntry entry, string message)
    {
        return new ScheduleConflict
        {
            Type = type,
            Severity = severity,
            Message = message,
            Scope = $"{entry.ClassName} / {entry.Subject} / {entry.SlotLabel}"
        };
    }

    private static bool BlocksSlot(FixedLesson fixedLesson, ScheduleEntry entry, int dayIndex, int periodIndex)
    {
        // FixedLesson.DayIndex 是1-based（UI输入），entry/solver 是0-based
        int fixedDay = Math.Max(0, fixedLesson.DayIndex - 1);
        if (fixedDay != dayIndex || fixedLesson.PeriodIndex != periodIndex)
        {
            return false;
        }

        // 基于 ScopeValue 字符串判断（UI 直接设置此字段）
        string scope = fixedLesson.ScopeValue?.Trim() ?? "全校";
        if (string.IsNullOrWhiteSpace(scope) || scope == "全校")
            return true;

        // 支持 "七+八年级" 格式
        string[] gradeParts = scope.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (string part in gradeParts)
        {
            string grade = part.EndsWith("年级") ? part : part + "年级";
            if (entry.ClassName.StartsWith(grade, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
