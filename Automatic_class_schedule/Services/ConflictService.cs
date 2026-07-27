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
                    var first = classGroup.First();
                    string subjects = string.Join("、", classGroup.Select(e => e.Subject).Distinct());
                    conflicts.Add(new ScheduleConflict
                    {
                        Type = ScheduleConflictType.ClassConflict,
                        Severity = ScheduleConflictSeverity.Hard,
                        Message = $"{first.ClassName} 在{first.SlotLabel}存在多节课（{subjects}）",
                        Scope = $"{first.ClassName} / {first.SlotLabel}",
                        Target = first.ClassName
                    });
                }
            }

            foreach (IGrouping<Guid, ScheduleEntry> teacherGroup in sameSlot.GroupBy(x => x.TeacherId))
            {
                // 跳过空TeacherId（固定课程等无教师条目）
                if (teacherGroup.Key == Guid.Empty) continue;
                if (teacherGroup.Count() > 1)
                {
                    // 体育教师允许同年级相邻班号连班（最多2班）
                    if (teacherGroup.All(e => e.Subject == "体育") && teacherGroup.Count() == 2)
                    {
                        var peEntries = teacherGroup.ToList();
                        if (AreConsecutiveClasses(peEntries[0].ClassName, peEntries[1].ClassName))
                            continue; // 合法连班，不报告冲突
                    }

                    var first = teacherGroup.First();
                    string classes = string.Join("、", teacherGroup.Select(e => e.ClassName).Distinct());
                    conflicts.Add(new ScheduleConflict
                    {
                        Type = ScheduleConflictType.TeacherConflict,
                        Severity = ScheduleConflictSeverity.Hard,
                        Message = $"{first.TeacherName} 在{first.SlotLabel}被分配到多个班级（{classes}）",
                        Scope = $"{first.TeacherName} / {first.SlotLabel}",
                        Target = first.TeacherName
                    });
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
                conflicts.Add(new ScheduleConflict
                {
                    Type = ScheduleConflictType.FixedLessonConflict,
                    Severity = ScheduleConflictSeverity.Hard,
                    Message = $"{entry.ClassName} 的{entry.Subject}与固定课「{fixedLesson.Reason}」在{entry.SlotLabel}冲突",
                    Scope = $"{entry.ClassName} / {entry.SlotLabel}",
                    Target = entry.ClassName
                });
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
                Message = $"目标时间超出排课范围（周{dayIndex + 1} 第{periodIndex}节）",
                Scope = $"周{dayIndex + 1} 第{periodIndex}节",
                Target = candidate.ClassName
            });
            return conflicts;
        }

        foreach (ScheduleEntry entry in existingEntries)
        {
            if (entry.DayIndex == dayIndex && entry.PeriodIndex == periodIndex)
            {
                if (entry.ClassId == candidate.ClassId)
                {
                    conflicts.Add(new ScheduleConflict
                    {
                        Type = ScheduleConflictType.ClassConflict,
                        Severity = ScheduleConflictSeverity.Hard,
                        Message = $"{candidate.ClassName} 在目标时间已有{entry.Subject}",
                        Scope = $"{candidate.ClassName} / 周{dayIndex + 1} 第{periodIndex}节",
                        Target = candidate.ClassName
                    });
                }

                // 跳过空TeacherId（固定课程等无教师条目）
                if (candidate.TeacherId != Guid.Empty && entry.TeacherId == candidate.TeacherId)
                {
                    conflicts.Add(new ScheduleConflict
                    {
                        Type = ScheduleConflictType.TeacherConflict,
                        Severity = ScheduleConflictSeverity.Hard,
                        Message = $"{candidate.TeacherName} 在目标时间已有{entry.ClassName}的{entry.Subject}",
                        Scope = $"{candidate.TeacherName} / 周{dayIndex + 1} 第{periodIndex}节",
                        Target = candidate.TeacherName
                    });
                }
            }
        }

        FixedLesson? fixedLesson = problem.FixedLessons.FirstOrDefault(x => BlocksSlot(x, candidate, dayIndex, periodIndex));
        if (fixedLesson is not null)
        {
            conflicts.Add(new ScheduleConflict
            {
                Type = ScheduleConflictType.FixedLessonConflict,
                Severity = ScheduleConflictSeverity.Hard,
                Message = $"目标时间与固定课「{fixedLesson.Reason}」冲突",
                Scope = $"{candidate.ClassName} / 周{dayIndex + 1} 第{periodIndex}节",
                Target = candidate.ClassName
            });
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
            Scope = $"{entry.ClassName} / {entry.Subject} / {entry.SlotLabel}",
            Target = type == ScheduleConflictType.TeacherConflict ? entry.TeacherName : entry.ClassName
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

    private static bool AreConsecutiveClasses(string classA, string classB)
    {
        var (gradeA, numA) = ParseClassName(classA);
        var (gradeB, numB) = ParseClassName(classB);
        if (gradeA != gradeB || numA < 0 || numB < 0) return false;
        return Math.Abs(numA - numB) == 1;
    }

    private static (string Grade, int Number) ParseClassName(string className)
    {
        if (string.IsNullOrEmpty(className)) return ("", -1);
        string trimmed = className.Replace("班", "");
        int i = 0;
        while (i < trimmed.Length && !char.IsDigit(trimmed[i])) i++;
        if (i == 0 || i >= trimmed.Length) return ("", -1);
        string grade = trimmed[..i];
        return int.TryParse(trimmed[i..], out int num) ? (grade, num) : (grade, -1);
    }
}
