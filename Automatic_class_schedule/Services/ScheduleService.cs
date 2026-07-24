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

    public ScheduleResult Generate(SchoolData data, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        ScheduleProblem problem = CreateProblem(data);
        return _solver.Solve(problem, progress, ct);
    }

    public ScheduleResult GenerateWithLocks(SchoolData data, List<LockedLesson> locks, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        ScheduleProblem problem = CreateProblem(data);
        return _solver.SolveWithLocks(problem, locks, progress, ct);
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

            string shortGrade = grade.GradeName.Replace("年级", "");

            for (int i = 1; i <= grade.ClassCount; i++)
            {
                classes.Add(new SchoolClass
                {
                    GradeName = grade.GradeName,
                    ClassNumber = i,
                    Name = $"{shortGrade}{i}班"
                });
            }
        }

        return classes;
    }

    public void GenerateAssignments(ICollection<TeacherAssignment> assignments, IEnumerable<SubjectDefinition> subjects, IEnumerable<SchoolClass> classes)
    {
        assignments.Clear();
        var classList = classes.ToList();
        if (classList.Count == 0) return;

        foreach (IGrouping<string, SchoolClass> gradeGroup in classList.GroupBy(c => c.GradeName))
        {
            string gradeName = gradeGroup.Key;
            List<SchoolClass> gradeClasses = gradeGroup.ToList();

            foreach (SubjectDefinition subDef in subjects.Where(s => string.IsNullOrEmpty(s.GradeName) || s.GradeName == gradeName))
            {
                int weeklyCount = subDef.DefaultWeeklyCount;
                int classCount = gradeClasses.Count;
                int perTeacher = Math.Max(1, (int)Math.Ceiling(classCount / 3.0));

                bool preferMorning = subDef.Name is "数学" or "英语" or "语文" or "物理" or "化学";
                bool avoidLast = subDef.Name is "体育";
                string distributionRule = subDef.DistributionRule;

                int numTeachers = (int)Math.Ceiling((double)classCount / perTeacher);
                int currentOffset = 0;

                for (int ti = 0; ti < numTeachers; ti++)
                {
                    int remaining = classCount - currentOffset;
                    int take = Math.Min(perTeacher, remaining);
                    if (take <= 0) break;

                    List<SchoolClass> teacherClasses = gradeClasses.Skip(currentOffset).Take(take).ToList();
                    currentOffset += take;

                    string teacherName = $"{subDef.Name[..1]}老师{ti + 1}";
                    var numbers = teacherClasses.Select(c => c.ClassNumber.ToString()).ToList();

                    assignments.Add(new TeacherAssignment
                    {
                        TeacherName = teacherName,
                        Subject = subDef.Name,
                        WeeklyCount = weeklyCount,
                        GradeName = gradeName,
                        ClassNumbers = string.Join(",", numbers),
                        DistributionRule = distributionRule,
                        PreferMorning = preferMorning,
                        AvoidLastPeriod = avoidLast
                    });
                }
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
            Guid teacherId = DeterministicGuid(assignment.TeacherName);

            string[] classNames = assignment.ClassNames.Split(new[] { '、', ',', '，', ';', '；', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (string className in classNames)
            {
                SchoolClass? schoolClass = allClasses.FirstOrDefault(c => string.Equals(c.Name, className, StringComparison.OrdinalIgnoreCase));
                if (schoolClass is null)
                {
                    schoolClass = new SchoolClass { Name = className, GradeName = className, ClassNumber = 1 };
                }

                requirements.Add(new LessonRequirement
                {
                    ClassId = schoolClass.Id,
                    ClassName = schoolClass.Name,
                    TeacherId = teacherId,
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

    private static Guid DeterministicGuid(string input)
    {
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(input);
        return new Guid(bytes.Concat(new byte[16 - bytes.Length]).Take(16).ToArray());
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
            "物理" => 3,
            "化学" => 3,
            "生物" => 2,
            "历史" => 2,
            "地理" => 2,
            "政治" => 2,
            "体育" => 2,
            "音乐" => 1,
            "美术" => 1,
            _ => 2
        };
    }
}
