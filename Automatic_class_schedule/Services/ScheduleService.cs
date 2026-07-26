using Automatic_class_schedule.Models;
using Automatic_class_schedule.Solver;

namespace Automatic_class_schedule.Services;

public sealed class ScheduleService
{
    private readonly IScheduleSolver _solver;
    private readonly ConflictService _conflictService;

    public ScheduleService(IScheduleSolver? solver = null, ConflictService? conflictService = null)
    {
        _solver = solver ?? new CpSatScheduleSolver();
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

        string[] mainSubjects = { "语文", "数学", "英语" };
        string[] scienceSubjects = { "物理", "化学" };
        string[] minorSubjects = { "地理", "生物", "历史", "道德" };
        string[] artSubjects = { "音乐", "美术", "信息", "劳动" };

        // 按年级分配非体育科目
        foreach (IGrouping<string, SchoolClass> gradeGroup in classList.GroupBy(c => c.GradeName))
        {
            string gradeName = gradeGroup.Key;
            string shortGrade = gradeName.Replace("年级", "");
            List<SchoolClass> gradeClasses = gradeGroup.ToList();
            int classCount = gradeClasses.Count;

            foreach (SubjectDefinition subDef in subjects.Where(s => string.IsNullOrEmpty(s.GradeName) || s.GradeName == gradeName))
            {
                // 体育单独处理（全校分配）
                if (subDef.Name == "体育") continue;

                bool preferMorning = mainSubjects.Contains(subDef.Name) || scienceSubjects.Contains(subDef.Name);
                string distributionRule = subDef.DistributionRule;

                // 根据科目类型确定教师数量
                int numTeachers;
                if (mainSubjects.Contains(subDef.Name))
                    numTeachers = (int)Math.Ceiling((double)classCount / 2); // 每人带2个班
                else if (scienceSubjects.Contains(subDef.Name))
                    numTeachers = 3; // 每年级3位
                else if (minorSubjects.Contains(subDef.Name))
                    numTeachers = 2; // 每年级2位
                else if (artSubjects.Contains(subDef.Name))
                    numTeachers = 1; // 每年级1位
                else
                    numTeachers = (int)Math.Ceiling((double)classCount / 3); // 默认每人3个班

                int perTeacher = (int)Math.Ceiling((double)classCount / numTeachers);
                int currentOffset = 0;

                for (int ti = 0; ti < numTeachers; ti++)
                {
                    int remaining = classCount - currentOffset;
                    int take = Math.Min(perTeacher, remaining);
                    if (take <= 0) break;

                    List<SchoolClass> teacherClasses = gradeClasses.Skip(currentOffset).Take(take).ToList();
                    currentOffset += take;

                    string teacherName = $"{shortGrade}{subDef.Name[..1]}{ToChineseNumeral(ti + 1)}";
                    var numbers = teacherClasses.Select(c => c.ClassNumber.ToString()).ToList();

                    assignments.Add(new TeacherAssignment
                    {
                        TeacherName = teacherName,
                        Subject = subDef.Name,
                        WeeklyCount = 0,
                        GradeName = gradeName,
                        ClassNumbers = string.Join(",", numbers),
                        DistributionRule = distributionRule,
                        PreferMorning = preferMorning,
                        AvoidLastPeriod = false
                    });
                }
            }
        }

        // 体育：全校6位老师，平均分配所有班级
        SubjectDefinition? peDef = subjects.FirstOrDefault(s => s.Name == "体育");
        if (peDef is not null)
        {
            int peTeacherCount = 6;
            int totalClasses = classList.Count;
            // 均匀分配：基础数 + 余数分配给前几位
            int baseCount = totalClasses / peTeacherCount;
            int remainder = totalClasses % peTeacherCount;
            int offset = 0;

            for (int ti = 0; ti < peTeacherCount; ti++)
            {
                int take = baseCount + (ti < remainder ? 1 : 0);
                if (take <= 0) break;

                List<SchoolClass> teacherClasses = classList.Skip(offset).Take(take).ToList();
                offset += take;

                string teacherName = $"体育{ToChineseNumeral(ti + 1)}";
                // 体育跨年级，用班级全名区分
                var fullNames = teacherClasses.Select(c => c.Name).ToList();

                var peAssignment = new TeacherAssignment
                {
                    TeacherName = teacherName,
                    Subject = "体育",
                    WeeklyCount = 0,
                    GradeName = "全校",
                    DistributionRule = peDef.DistributionRule,
                    PreferMorning = false,
                    AvoidLastPeriod = true
                };
                // 直接设置ClassNames避免UpdateClassNames拼接错误
                peAssignment.ClassNames = string.Join("、", fullNames);
                assignments.Add(peAssignment);
            }
        }
    }

    public List<LessonRequirement> BuildRequirementsFromAssignments(IEnumerable<TeacherAssignment> assignments, IEnumerable<SchoolClass> allClasses, IEnumerable<SubjectDefinition> subjects)
    {
        List<LessonRequirement> requirements = new();
        var subjectList = subjects.Where(x => !string.IsNullOrWhiteSpace(x.Name)).ToList();

        foreach (TeacherAssignment assignment in assignments)
        {
            if (string.IsNullOrWhiteSpace(assignment.TeacherName) || string.IsNullOrWhiteSpace(assignment.Subject))
            {
                continue;
            }

            // 年级感知的周课时解析：优先匹配同年级科目配置，其次无年级配置，最后默认值
            int weeklyCount = assignment.WeeklyCount > 0
                ? assignment.WeeklyCount
                : ResolveWeeklyCount(subjectList, assignment.Subject, assignment.GradeName);
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

    /// <summary>年级感知的周课时解析：优先同年级科目配置 → 无年级配置 → 默认值</summary>
    private static int ResolveWeeklyCount(List<SubjectDefinition> subjects, string subjectName, string gradeName)
    {
        // 1. 精确匹配：同科目+同年级
        if (!string.IsNullOrWhiteSpace(gradeName))
        {
            var gradeMatch = subjects.FirstOrDefault(s =>
                string.Equals(s.Name, subjectName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(s.GradeName, gradeName, StringComparison.OrdinalIgnoreCase));
            if (gradeMatch is not null) return gradeMatch.DefaultWeeklyCount;
        }

        // 2. 回退：同科目+无年级指定
        var genericMatch = subjects.FirstOrDefault(s =>
            string.Equals(s.Name, subjectName, StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrEmpty(s.GradeName));
        if (genericMatch is not null) return genericMatch.DefaultWeeklyCount;

        // 3. 同科目任意一个（兼容旧数据）
        var anyMatch = subjects.FirstOrDefault(s =>
            string.Equals(s.Name, subjectName, StringComparison.OrdinalIgnoreCase));
        if (anyMatch is not null) return anyMatch.DefaultWeeklyCount;

        // 4. 硬编码默认值
        return GetDefaultWeeklyCount(subjectName);
    }

    private static string ToChineseNumeral(int n)
    {
        string[] digits = { "零", "一", "二", "三", "四", "五", "六", "七", "八", "九", "十" };
        if (n <= 10) return digits[n];
        if (n < 20) return "十" + digits[n - 10];
        return n.ToString();
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
            "道德" => 2,
            "体育" => 3,
            "音乐" => 1,
            "美术" => 1,
            "信息" => 1,
            "劳动" => 1,
            _ => 2
        };
    }
}
