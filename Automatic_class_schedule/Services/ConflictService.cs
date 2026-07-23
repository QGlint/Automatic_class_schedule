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
                if (teacherGroup.Count() > 1)
                {
                    conflicts.Add(CreateConflict(ScheduleConflictType.TeacherConflict, ScheduleConflictSeverity.Hard, teacherGroup.First(), "同教师同一时间存在多节课"));
                }
            }
        }

        foreach (ScheduleEntry entry in entryList)
        {
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

                if (entry.TeacherId == candidate.TeacherId)
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
        if (fixedLesson.DayIndex != dayIndex || fixedLesson.PeriodIndex != periodIndex)
        {
            return false;
        }

        return fixedLesson.Scope switch
        {
            FixedLessonScope.All => true,
            FixedLessonScope.Class => string.IsNullOrWhiteSpace(fixedLesson.ScopeValue) || string.Equals(fixedLesson.ScopeValue, entry.ClassName, StringComparison.OrdinalIgnoreCase),
            FixedLessonScope.Teacher => string.IsNullOrWhiteSpace(fixedLesson.ScopeValue) || string.Equals(fixedLesson.ScopeValue, entry.TeacherName, StringComparison.OrdinalIgnoreCase),
            FixedLessonScope.Grade => string.IsNullOrWhiteSpace(fixedLesson.ScopeValue) || entry.ClassName.StartsWith(fixedLesson.ScopeValue, StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }
}
