using System.IO;
using ClosedXML.Excel;
using Automatic_class_schedule.Models;

namespace Automatic_class_schedule.Services;

public sealed class ExcelScheduleService
{
    public SchoolData Import(string filePath)
    {
        using XLWorkbook workbook = new(filePath);
        SchoolData data = new();

        IXLWorksheet? gradeSheet = workbook.Worksheets.FirstOrDefault(x => x.Name == "班级配置");
        if (gradeSheet is not null)
        {
            foreach (IXLRow row in gradeSheet.RowsUsed().Skip(1))
            {
                string gradeName = row.Cell(1).GetString().Trim();
                int classCount = row.Cell(2).GetValue<int>();
                if (!string.IsNullOrWhiteSpace(gradeName) && classCount > 0)
                {
                    data.GradeInputs.Add(new GradeInput { GradeName = gradeName, ClassCount = classCount });
                }
            }
        }

        if (data.GradeInputs.Count > 0)
        {
            data.Classes.AddRange(new ScheduleService().BuildClasses(data.GradeInputs));
        }

        IXLWorksheet? teacherSheet = workbook.Worksheets.FirstOrDefault(x => x.Name == "教师信息");
        if (teacherSheet is not null)
        {
            foreach (IXLRow row in teacherSheet.RowsUsed().Skip(1))
            {
                string name = row.Cell(1).GetString().Trim();
                string subject = row.Cell(2).GetString().Trim();
                string classes = row.Cell(3).GetString().Trim();
                int weeklyCount = row.Cell(4).GetValue<int>();
                if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(subject))
                {
                    data.TeacherAssignments.Add(new TeacherAssignment
                    {
                        TeacherName = name,
                        Subject = subject,
                        ClassNames = string.IsNullOrWhiteSpace(classes) ? "全部" : classes,
                        WeeklyCount = weeklyCount
                    });
                }
            }
        }

        IXLWorksheet? requirementSheet = workbook.Worksheets.FirstOrDefault(x => x.Name == "授课安排");
        if (requirementSheet is not null)
        {
            foreach (IXLRow row in requirementSheet.RowsUsed().Skip(1))
            {
                string teacherName = row.Cell(1).GetString().Trim();
                string classRange = row.Cell(2).GetString().Trim();
                string subject = row.Cell(3).GetString().Trim();
                int weeklyCount = row.Cell(4).GetValue<int>();
                string distribution = row.Cell(5).GetString().Trim();
                if (string.IsNullOrWhiteSpace(teacherName) || string.IsNullOrWhiteSpace(subject))
                {
                    continue;
                }

                Teacher? teacher = data.Teachers.FirstOrDefault(x => string.Equals(x.Name, teacherName, StringComparison.OrdinalIgnoreCase)) ?? new Teacher { Name = teacherName, Subject = subject };
                if (!data.Teachers.Contains(teacher))
                {
                    data.Teachers.Add(teacher);
                }

                foreach (SchoolClass schoolClass in ResolveClasses(classRange, data.Classes))
                {
                    data.Requirements.Add(new LessonRequirement
                    {
                        ClassId = schoolClass.Id,
                        ClassName = schoolClass.Name,
                        TeacherId = teacher.Id,
                        TeacherName = teacher.Name,
                        Subject = subject,
                        WeeklyCount = weeklyCount > 0 ? weeklyCount : 2,
                        DistributionRule = string.IsNullOrWhiteSpace(distribution) ? "均衡分布" : distribution,
                        PreferMorning = subject is "数学" or "英语",
                        AvoidLastPeriod = subject is "体育" or "英语"
                    });
                }
            }
        }

        if (data.Requirements.Count == 0 && data.Classes.Count > 0 && data.TeacherAssignments.Count > 0)
        {
            data.Requirements.AddRange(new ScheduleService().BuildRequirementsFromAssignments(data.TeacherAssignments, data.Classes, data.Subjects));
        }

        IXLWorksheet? fixedSheet = workbook.Worksheets.FirstOrDefault(x => x.Name == "固定课程");
        if (fixedSheet is not null)
        {
            foreach (IXLRow row in fixedSheet.RowsUsed().Skip(1))
            {
                string scope = row.Cell(1).GetString().Trim();
                string day = row.Cell(2).GetString().Trim();
                string periodText = row.Cell(3).GetString().Trim();
                string subject = row.Cell(4).GetString().Trim();
                if (string.IsNullOrWhiteSpace(day) || string.IsNullOrWhiteSpace(periodText))
                {
                    continue;
                }

                data.FixedLessons.Add(new FixedLesson
                {
                    Scope = ParseScope(scope),
                    ScopeValue = scope,
                    DayIndex = ParseDay(day),
                    PeriodIndex = ParsePeriod(periodText),
                    Subject = subject,
                    Reason = string.IsNullOrWhiteSpace(subject) ? "固定课程" : subject
                });
            }
        }

        return data;
    }

    public void ExportAll(SchoolData data, string folder)
    {
        Directory.CreateDirectory(folder);
        ExportWorkbook(Path.Combine(folder, "总课表.xlsx"), "总课表", data.ScheduleEntries);
        ExportWorkbook(Path.Combine(folder, "班级课表.xlsx"), "班级课表", data.ScheduleEntries);
        ExportWorkbook(Path.Combine(folder, "教师课表.xlsx"), "教师课表", data.ScheduleEntries);
        ExportWorkbook(Path.Combine(folder, "年级课表.xlsx"), "年级课表", data.ScheduleEntries);
    }

    private static void ExportWorkbook(string filePath, string sheetName, IEnumerable<ScheduleEntry> entries)
    {
        using XLWorkbook workbook = new();
        IXLWorksheet sheet = workbook.AddWorksheet(sheetName);
        sheet.Cell(1, 1).Value = "班级";
        sheet.Cell(1, 2).Value = "科目";
        sheet.Cell(1, 3).Value = "教师";
        sheet.Cell(1, 4).Value = "星期";
        sheet.Cell(1, 5).Value = "节次";
        sheet.Cell(1, 6).Value = "备注";

        int row = 2;
        foreach (ScheduleEntry entry in entries.OrderBy(x => x.ClassName).ThenBy(x => x.DayIndex).ThenBy(x => x.PeriodIndex))
        {
            sheet.Cell(row, 1).Value = entry.ClassName;
            sheet.Cell(row, 2).Value = entry.Subject;
            sheet.Cell(row, 3).Value = entry.TeacherName;
            sheet.Cell(row, 4).Value = $"周{entry.DayIndex + 1}";
            sheet.Cell(row, 5).Value = entry.PeriodIndex;
            sheet.Cell(row, 6).Value = entry.Note;
            row++;
        }

        sheet.Columns().AdjustToContents();
        workbook.SaveAs(filePath);
    }

    private static IEnumerable<SchoolClass> ResolveClasses(string classRange, List<SchoolClass> classes)
    {
        if (string.IsNullOrWhiteSpace(classRange))
        {
            return classes;
        }

        if (classRange.Contains("全部", StringComparison.OrdinalIgnoreCase))
        {
            string grade = classRange.Replace("年级全部", string.Empty).Replace("全部", string.Empty).Trim();
            return classes.Where(x => x.Name.StartsWith(grade, StringComparison.OrdinalIgnoreCase));
        }

        string[] names = classRange.Split(new[] { ',', '，', ';', '；', '、', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        List<SchoolClass> result = classes.Where(x => names.Contains(x.Name, StringComparer.OrdinalIgnoreCase)).ToList();
        if (result.Count > 0)
        {
            return result;
        }

        foreach (string name in names)
        {
            if (!classes.Any(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                classes.Add(new SchoolClass
                {
                    Name = name,
                    GradeName = ExtractGradeName(name),
                    ClassNumber = ExtractClassNumber(name)
                });
            }
        }

        return classes.Where(x => names.Contains(x.Name, StringComparer.OrdinalIgnoreCase));
    }

    private static string ExtractGradeName(string className)
    {
        int index = className.IndexOf('班');
        return index > 0 ? className[..index] : className;
    }

    private static int ExtractClassNumber(string className)
    {
        string digits = new(className.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out int number) ? number : 1;
    }

    private static FixedLessonScope ParseScope(string scope)
    {
        return scope switch
        {
            "全校" => FixedLessonScope.All,
            string s when s.Contains("年级", StringComparison.OrdinalIgnoreCase) => FixedLessonScope.Grade,
            string s when s.Contains("老师", StringComparison.OrdinalIgnoreCase) => FixedLessonScope.Teacher,
            _ => FixedLessonScope.Class
        };
    }

    private static int ParseDay(string value)
    {
        return value switch
        {
            "周一" => 0,
            "周二" => 1,
            "周三" => 2,
            "周四" => 3,
            "周五" => 4,
            "周六" => 5,
            "周日" => 6,
            _ => 0
        };
    }

    private static int ParsePeriod(string value)
    {
        string digits = new(value.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out int result) && result > 0 ? result : 1;
    }
}
