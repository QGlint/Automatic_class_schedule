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

    public List<LessonRequirement> BuildRequirements(IEnumerable<SchoolClass> classes, IEnumerable<Teacher> teachers)
    {
        List<LessonRequirement> requirements = new();
        Dictionary<string, Teacher> teacherMap = teachers.Where(x => !string.IsNullOrWhiteSpace(x.Name)).GroupBy(x => x.Name).ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

        foreach (SchoolClass schoolClass in classes)
        {
            foreach (Teacher teacher in teachers)
            {
                if (string.IsNullOrWhiteSpace(teacher.Subject))
                {
                    continue;
                }

                requirements.Add(new LessonRequirement
                {
                    ClassId = schoolClass.Id,
                    ClassName = schoolClass.Name,
                    TeacherId = teacher.Id,
                    TeacherName = teacher.Name,
                    Subject = teacher.Subject,
                    WeeklyCount = GetDefaultWeeklyCount(teacher.Subject),
                    DistributionRule = teacher.Subject is "数学" or "英语" ? "每天一次" : "均衡分布",
                    PreferMorning = teacher.Subject is "数学" or "英语",
                    AvoidLastPeriod = teacher.Subject is "体育" or "英语"
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

    private static int GetDefaultWeeklyCount(string subject)
    {
        return subject switch
        {
            "语文" => 5,
            "数学" => 5,
            "英语" => 4,
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
