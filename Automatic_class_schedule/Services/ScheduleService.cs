using System.Collections.ObjectModel;
using Automatic_class_schedule.Models;
using Automatic_class_schedule.Solver;

namespace Automatic_class_schedule.Services;

public sealed class ScheduleService
{
    private readonly IScheduleSolver _solver;
    private readonly ConflictService _conflictService;

    public ScheduleService(IScheduleSolver? solver = null, ConflictService? conflictService = null)
    {
        _solver = solver ?? new GreedyScheduleSolver();
        _conflictService = conflictService ?? new ConflictService();
    }

    public ScheduleResult Generate(SchoolData data)
    {
        ScheduleProblem problem = CreateProblem(data);
        return _solver.Solve(problem);
    }

    public ScheduleResult GenerateWithLocks(SchoolData data, List<LockedLesson> locks)
    {
        ScheduleProblem problem = CreateProblem(data);
        return _solver.SolveWithLocks(problem, locks);
    }

    public IReadOnlyList<ScheduleConflict> Validate(SchoolData data)
    {
        ScheduleProblem problem = CreateProblem(data);
        return _conflictService.Analyze(problem, data.ScheduleEntries);
    }

    public bool TryMoveEntry(SchoolData data, ScheduleEntry entry, int dayIndex, int periodIndex, out IReadOnlyList<ScheduleConflict> conflicts)
    {
        ScheduleProblem problem = CreateProblem(data);
        List<ScheduleEntry> others = data.ScheduleEntries.Where(x => x.Id != entry.Id).ToList();
        conflicts = _conflictService.ValidatePlacement(problem, others, entry, dayIndex, periodIndex);
        if (conflicts.Count > 0)
        {
            return false;
        }

        entry.DayIndex = dayIndex;
        entry.PeriodIndex = periodIndex;
        entry.Locked = true;
        entry.IsFixed = false;
        entry.Note = "手动调整";
        return true;
    }

    public bool TrySwapEntries(SchoolData data, ScheduleEntry first, ScheduleEntry second, out IReadOnlyList<ScheduleConflict> conflicts)
    {
        ScheduleProblem problem = CreateProblem(data);
        List<ScheduleEntry> others = data.ScheduleEntries.Where(x => x.Id != first.Id && x.Id != second.Id).ToList();

        List<ScheduleConflict> firstConflicts = _conflictService.ValidatePlacement(problem, others, first, second.DayIndex, second.PeriodIndex).ToList();
        List<ScheduleConflict> secondConflicts = _conflictService.ValidatePlacement(problem, others, second, first.DayIndex, first.PeriodIndex).ToList();
        conflicts = firstConflicts.Concat(secondConflicts).ToList();
        if (conflicts.Count > 0)
        {
            return false;
        }

        int firstDay = first.DayIndex;
        int firstPeriod = first.PeriodIndex;
        first.DayIndex = second.DayIndex;
        first.PeriodIndex = second.PeriodIndex;
        second.DayIndex = firstDay;
        second.PeriodIndex = firstPeriod;

        first.Locked = true;
        second.Locked = true;
        first.IsFixed = false;
        second.IsFixed = false;
        first.Note = "换课";
        second.Note = "换课";
        return true;
    }

    public List<SchoolClass> BuildClasses(IEnumerable<GradeInput> grades)
    {
        List<SchoolClass> classes = new();
        foreach (GradeInput grade in grades)
        {
            if (grade.ClassCount <= 0 || string.IsNullOrWhiteSpace(grade.GradeName))
            {
                continue;
            }

            for (int i = 1; i <= grade.ClassCount; i++)
            {
                classes.Add(new SchoolClass
                {
                    GradeName = grade.GradeName,
                    ClassNumber = i,
                    Name = $"{grade.GradeName}{i}班"
                });
            }
        }

        return classes;
    }

    public void GenerateAssignments(ICollection<TeacherAssignment> assignments, IEnumerable<SubjectDefinition> subjects, IEnumerable<SchoolClass> classes)
    {
        assignments.Clear();
        List<string> classNames = classes.Select(c => c.Name).ToList();
        if (classNames.Count == 0) return;

        string[] allSubjects = { "语文", "数学", "英语", "体育", "政治", "历史", "地理", "物理", "化学", "生物" };
        int[] cpTeacher = { 2, 2, 2, 6, 3, 3, 3, 3, 3, 3 };

        int classCount = classNames.Count;

        for (int si = 0; si < allSubjects.Length; si++)
        {
            string subject = allSubjects[si];
            int weeklyCount = subjects.FirstOrDefault(s => s.Name == subject)?.DefaultWeeklyCount ?? GetDefaultWeeklyCount(subject);
            int perTeacher = cpTeacher[si];
            int numTeachers = (int)Math.Ceiling((double)classCount / perTeacher);

            bool preferMorning = subject is "数学" or "英语" or "语文" or "物理" or "化学";
            bool avoidLast = subject is "体育";

            int currentOffset = 0;
            for (int ti = 0; ti < numTeachers; ti++)
            {
                int remaining = classCount - currentOffset;
                int take = Math.Min(perTeacher, remaining);
                if (take <= 0) break;

                List<string> teacherClasses = classNames.Skip(currentOffset).Take(take).ToList();
                currentOffset += take;

                string teacherName = $"{subject.Substring(0, 1)}老师{ti + 1}";

                TeacherAssignment ta = new()
                {
                    TeacherName = teacherName,
                    Subject = subject,
                    WeeklyCount = weeklyCount,
                    ClassNames = string.Join("、", teacherClasses),
                    DistributionRule = preferMorning ? "每天一次" : "均衡分布",
                    PreferMorning = preferMorning,
                    AvoidLastPeriod = avoidLast
                };
                assignments.Add(ta);
            }

        }
    }

    public List<LessonRequirement> BuildRequirementsFromAssignments(IEnumerable<TeacherAssignment> assignments, IEnumerable<SchoolClass> allClasses, IEnumerable<SubjectDefinition> subjects)
    {
        List<LessonRequirement> requirements = new();
        Dictionary<string, SubjectDefinition> subjectMap = subjects.Where(x => !string.IsNullOrWhiteSpace(x.Name)).GroupBy(x => x.Name).ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

        foreach (TeacherAssignment assignment in assignments)
        {
            if (string.IsNullOrWhiteSpace(assignment.TeacherName) || string.IsNullOrWhiteSpace(assignment.Subject))
            {
                continue;
            }

            int weeklyCount = assignment.WeeklyCount > 0 ? assignment.WeeklyCount : (subjectMap.TryGetValue(assignment.Subject, out SubjectDefinition? sub) ? sub.DefaultWeeklyCount : GetDefaultWeeklyCount(assignment.Subject));

            string[] classNames = assignment.ClassNames.Split(new[] { '、', ',', '，', ';', '；', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (string className in classNames)
            {
                SchoolClass? schoolClass = allClasses.FirstOrDefault(c => string.Equals(c.Name, className, StringComparison.OrdinalIgnoreCase));
                if (schoolClass is null)
                {
                    schoolClass = new SchoolClass { Name = className, GradeName = className, ClassNumber = 1 };
                }

                Teacher teacher = new()
                {
                    Name = assignment.TeacherName,
                    Subject = assignment.Subject
                };

                requirements.Add(new LessonRequirement
                {
                    ClassId = schoolClass.Id,
                    ClassName = schoolClass.Name,
                    TeacherId = teacher.Id,
                    TeacherName = assignment.TeacherName,
                    Subject = assignment.Subject,
                    WeeklyCount = weeklyCount,
                    DistributionRule = assignment.DistributionRule,
                    PreferMorning = assignment.PreferMorning,
                    AvoidLastPeriod = assignment.AvoidLastPeriod
                });
            }
        }

        return requirements;
    }

    private static ScheduleProblem CreateProblem(SchoolData data)
    {
        return new ScheduleProblem
        {
            Settings = data.Settings,
            Classes = data.Classes,
            Teachers = data.Teachers,
            Requirements = data.Requirements,
            FixedLessons = data.FixedLessons
        };
    }

    public static int GetDefaultWeeklyCount(string subject)
    {
        return subject switch
        {
            "语文" => 6,
            "数学" => 6,
            "英语" => 5,
            "物理" => 4,
            "化学" => 3,
            "生物" => 3,
            "历史" => 3,
            "地理" => 3,
            "政治" => 3,
            "体育" => 3,
            "音乐" => 1,
            "美术" => 1,
            _ => 2
        };
    }
}
