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

        string prefix = string.IsNullOrWhiteSpace(data.ProjectName) ? "" : data.ProjectName + "_";
        int days = data.Settings.DaysPerWeek;
        int periods = data.Settings.PeriodsPerDay;
        var entries = data.ScheduleEntries;
        var grades = data.Classes.GroupBy(c => c.GradeName).OrderBy(g => g.Key).ToList();

        // ===== 文件1: 年级课表.xlsx =====
        using (XLWorkbook wb = new())
        {
            WriteGradeOverviewSheet(wb, "总表(简)", grades, entries, days, periods, simplified: true);
            WriteGradeOverviewSheet(wb, "总表", grades, entries, days, periods, simplified: false);
            foreach (var gradeGroup in grades)
            {
                string shortName = gradeGroup.Key.Replace("年级", "");
                WriteGradeOverviewSheet(wb, $"{shortName}年级", new[] { gradeGroup }, entries, days, periods, simplified: false);
            }
            wb.SaveAs(Path.Combine(folder, $"{prefix}年级课表.xlsx"));
        }

        // ===== 文件2: 班级课表.xlsx =====
        using (XLWorkbook wb = new())
        {
            foreach (var cls in data.Classes.OrderBy(c => c.GradeName).ThenBy(c => c.ClassNumber))
            {
                string sheetName = SanitizeSheetName(cls.Name);
                WriteClassSheet(wb, sheetName, cls.Name, entries, days, periods);
            }
            wb.SaveAs(Path.Combine(folder, $"{prefix}班级课表.xlsx"));
        }

        // ===== 文件3: 教师课表.xlsx =====
        using (XLWorkbook wb = new())
        {
            var teachers = entries.Where(e => !string.IsNullOrEmpty(e.TeacherName))
                .GroupBy(e => e.TeacherName).OrderBy(g => g.Key).ToList();
            foreach (var teacherGroup in teachers)
            {
                string sheetName = SanitizeSheetName(teacherGroup.Key);
                WriteTeacherSheet(wb, sheetName, teacherGroup.Key, teacherGroup.ToList(), days, periods);
            }
            wb.SaveAs(Path.Combine(folder, $"{prefix}教师课表.xlsx"));
        }
    }

    /// <summary>年级总表格式：班级为行，天×节次为列</summary>
    private static void WriteGradeOverviewSheet(XLWorkbook workbook, string sheetName,
        IEnumerable<IGrouping<string, SchoolClass>> gradeGroups, IEnumerable<ScheduleEntry> allEntries,
        int days, int periods, bool simplified)
    {
        IXLWorksheet sheet = workbook.AddWorksheet(SanitizeSheetName(sheetName));
        var entryList = allEntries.ToList();
        string[] dayNames = { "周一", "周二", "周三", "周四", "周五", "周六", "周日" };

        // 双层表头
        // Row 1: 空 | 天名称（合并每天的所有节次列）
        // Row 2: 班级 | 第1节 | 第2节 | ... 重复每天
        sheet.Cell(1, 1).Value = "班级";
        sheet.Range(1, 1, 2, 1).Merge();

        int col = 2;
        for (int d = 0; d < days; d++)
        {
            sheet.Cell(1, col).Value = dayNames[d];
            sheet.Range(1, col, 1, col + periods - 1).Merge();
            for (int p = 1; p <= periods; p++)
            {
                sheet.Cell(2, col).Value = simplified ? p.ToString() : $"第{p}节";
                col++;
            }
        }

        // 数据行
        int row = 3;
        foreach (var gradeGroup in gradeGroups)
        {
            foreach (var cls in gradeGroup.OrderBy(c => c.ClassNumber))
            {
                sheet.Cell(row, 1).Value = cls.Name;
                col = 2;
                for (int d = 0; d < days; d++)
                {
                    for (int p = 1; p <= periods; p++)
                    {
                        var entry = entryList.FirstOrDefault(e =>
                            e.ClassName == cls.Name && e.DayIndex == d && e.PeriodIndex == p);
                        if (entry != null)
                        {
                            string text = simplified
                                ? entry.Subject[..1]  // 只显示第一个字
                                : $"{entry.Subject}\n{entry.TeacherName}";
                            sheet.Cell(row, col).Value = text;
                        }
                        col++;
                    }
                }
                row++;
            }
        }

        // 样式
        StyleHeaderRow(sheet, 1, 1, 1, col - 1);
        StyleHeaderRow(sheet, 2, 1, 2, col - 1);
        sheet.Column(1).Width = 10;
        for (int c = 2; c < col; c++)
            sheet.Column(c).Width = simplified ? 5 : 10;
        sheet.Rows().Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        sheet.Rows().Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        sheet.Rows().Style.Alignment.WrapText = true;  // 换行显示科目+教师
        sheet.Rows().Style.Font.FontSize = 10;
        sheet.Row(1).Style.Font.Bold = true;
        sheet.Row(2).Style.Font.Bold = true;
    }

    /// <summary>班级课表格式：节次为行，天为列，固定课纵向合并</summary>
    private static void WriteClassSheet(XLWorkbook workbook, string sheetName, string className,
        IEnumerable<ScheduleEntry> allEntries, int days, int periods)
    {
        IXLWorksheet sheet = workbook.AddWorksheet(sheetName);
        var entries = allEntries.Where(e => e.ClassName == className).ToList();
        string[] dayNames = { "周一", "周二", "周三", "周四", "周五", "周六", "周日" };

        // 标题行
        sheet.Cell(1, 1).Value = $"{className} 课程表";
        sheet.Range(1, 1, 1, days + 1).Merge();
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(1, 1).Style.Font.FontSize = 14;
        sheet.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        // 表头: 节次 | 周一 | 周二 | ...
        sheet.Cell(2, 1).Value = "节次";
        for (int d = 0; d < days; d++)
            sheet.Cell(2, d + 2).Value = dayNames[d];
        StyleHeaderRow(sheet, 2, 1, 2, days + 1);

        // 数据行
        for (int p = 1; p <= periods; p++)
        {
            int row = p + 2;
            sheet.Cell(row, 1).Value = $"第{p}节";
            sheet.Cell(row, 1).Style.Font.Bold = true;

            for (int d = 0; d < days; d++)
            {
                var entry = entries.FirstOrDefault(e => e.DayIndex == d && e.PeriodIndex == p);
                if (entry != null)
                    sheet.Cell(row, d + 2).Value = $"{entry.Subject}\n{entry.TeacherName}";
            }
        }

        // 固定课纵向合并
        MergeFixedLessons(sheet, entries, days, periods);

        // 列宽自适应
        sheet.Column(1).Width = 8;
        for (int d = 0; d < days; d++)
            sheet.Column(d + 2).Width = 12;
        sheet.Rows().Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        sheet.Rows().Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        sheet.Rows().Style.Font.FontSize = 11;
        sheet.Rows().Style.Alignment.WrapText = true;
    }

    /// <summary>教师课表格式：节次为行，天为列，显示班级名</summary>
    private static void WriteTeacherSheet(XLWorkbook workbook, string sheetName, string teacherName,
        List<ScheduleEntry> entries, int days, int periods)
    {
        IXLWorksheet sheet = workbook.AddWorksheet(sheetName);
        string[] dayNames = { "周一", "周二", "周三", "周四", "周五", "周六", "周日" };

        // 标题行
        sheet.Cell(1, 1).Value = $"{teacherName} 课程表";
        sheet.Range(1, 1, 1, days + 1).Merge();
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(1, 1).Style.Font.FontSize = 14;
        sheet.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        // 表头
        sheet.Cell(2, 1).Value = "节次";
        for (int d = 0; d < days; d++)
            sheet.Cell(2, d + 2).Value = dayNames[d];
        StyleHeaderRow(sheet, 2, 1, 2, days + 1);

        // 数据行
        for (int p = 1; p <= periods; p++)
        {
            int row = p + 2;
            sheet.Cell(row, 1).Value = $"第{p}节";
            sheet.Cell(row, 1).Style.Font.Bold = true;

            for (int d = 0; d < days; d++)
            {
                // 教师同一时段可能有多个班（体育连班）
                var slotEntries = entries.Where(e => e.DayIndex == d && e.PeriodIndex == p).ToList();
                if (slotEntries.Count > 0)
                {
                    string text = string.Join("\n", slotEntries.Select(e => $"{e.Subject} {e.ClassName}"));
                    sheet.Cell(row, d + 2).Value = text;
                }
            }
        }

        // 固定课纵向合并
        MergeFixedLessons(sheet, entries, days, periods);

        // 列宽
        sheet.Column(1).Width = 8;
        for (int d = 0; d < days; d++)
            sheet.Column(d + 2).Width = 14;
        sheet.Rows().Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        sheet.Rows().Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        sheet.Rows().Style.Font.FontSize = 11;
        sheet.Rows().Style.Alignment.WrapText = true;
    }

    /// <summary>固定课程纵向合并单元格</summary>
    private static void MergeFixedLessons(IXLWorksheet sheet, List<ScheduleEntry> entries, int days, int periods)
    {
        // 找出固定课（IsFixed=true）的连续同科目段，纵向合并
        for (int d = 0; d < days; d++)
        {
            int p = 1;
            while (p <= periods)
            {
                var entry = entries.FirstOrDefault(e => e.DayIndex == d && e.PeriodIndex == p && e.IsFixed);
                if (entry != null)
                {
                    // 找连续同科目固定课
                    int startP = p;
                    while (p + 1 <= periods &&
                           entries.Any(e => e.DayIndex == d && e.PeriodIndex == p + 1 && e.IsFixed && e.Subject == entry.Subject))
                    {
                        p++;
                    }

                    if (p > startP)
                    {
                        // 合并单元格 (startP+2 到 p+2 行, d+2 列)
                        int startRow = startP + 2;
                        int endRow = p + 2;
                        int colIdx = d + 2;
                        sheet.Range(startRow, colIdx, endRow, colIdx).Merge();
                        sheet.Cell(startRow, colIdx).Value = $"{entry.Subject}\n{entry.TeacherName}";
                        sheet.Cell(startRow, colIdx).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                    }
                }
                p++;
            }
        }
    }

    private static void StyleHeaderRow(IXLWorksheet sheet, int row, int colStart, int rowEnd, int colEnd)
    {
        var range = sheet.Range(row, colStart, rowEnd, colEnd);
        range.Style.Font.Bold = true;
        range.Style.Fill.BackgroundColor = XLColor.LightSteelBlue;
        range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
    }

    /// <summary>Excel sheet名最长31字符，去除非法字符</summary>
    private static string SanitizeSheetName(string name)
    {
        char[] invalid = { '\\', '/', '?', '*', '[', ']', ':' };
        foreach (char c in invalid)
            name = name.Replace(c, '_');
        return name.Length > 31 ? name[..31] : name;
    }

    /// <summary>从 xlsx 导入教师配置（读取"教师信息" sheet）</summary>
    public List<TeacherAssignment> ImportTeacherAssignments(string filePath)
    {
        List<TeacherAssignment> assignments = new();
        using XLWorkbook workbook = new(filePath);

        IXLWorksheet? sheet = workbook.Worksheets.FirstOrDefault(x => x.Name == "教师信息")
            ?? workbook.Worksheets.FirstOrDefault();
        if (sheet is null) return assignments;

        foreach (IXLRow row in sheet.RowsUsed().Skip(1))
        {
            string teacherName = row.Cell(1).GetString().Trim();
            string subject = row.Cell(2).GetString().Trim();
            string classNames = row.Cell(3).GetString().Trim();
            int weeklyCount = row.Cell(4).GetValue<int>();

            if (string.IsNullOrWhiteSpace(teacherName) || string.IsNullOrWhiteSpace(subject))
                continue;

            // 推断年级：从班级名称中提取（如"七1班"→"七年级"）
            string gradeName = "";
            var firstClass = classNames.Split(new[] { '、', ',', '，' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim();
            if (!string.IsNullOrEmpty(firstClass))
            {
                string gradeChar = firstClass[..1];
                gradeName = $"{gradeChar}年级";
            }

            var assignment = new TeacherAssignment
            {
                TeacherName = teacherName,
                Subject = subject,
                WeeklyCount = weeklyCount,
                GradeName = gradeName
            };
            assignment.ClassNames = classNames;
            assignments.Add(assignment);
        }

        return assignments;
    }

    /// <summary>生成教师导入模板 Excel</summary>
    public void GenerateImportTemplate(string filePath, List<GradeInput> grades)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        using XLWorkbook workbook = new();

        // 教师信息 sheet
        IXLWorksheet teacherSheet = workbook.AddWorksheet("教师信息");
        teacherSheet.Cell(1, 1).Value = "教师姓名";
        teacherSheet.Cell(1, 2).Value = "科目";
        teacherSheet.Cell(1, 3).Value = "班级（如：七1班、七2班，多个用顿号分隔）";
        teacherSheet.Cell(1, 4).Value = "周课时";
        // 示例行
        teacherSheet.Cell(2, 1).Value = "张三";
        teacherSheet.Cell(2, 2).Value = "语文";
        teacherSheet.Cell(2, 3).Value = "七1班、七2班";
        teacherSheet.Cell(2, 4).Value = 6;
        teacherSheet.Cell(3, 1).Value = "李四";
        teacherSheet.Cell(3, 2).Value = "数学";
        teacherSheet.Cell(3, 3).Value = "八1班、八2班、八3班";
        teacherSheet.Cell(3, 4).Value = 6;
        // 调整列宽
        teacherSheet.Column(1).Width = 14;
        teacherSheet.Column(2).Width = 10;
        teacherSheet.Column(3).Width = 36;
        teacherSheet.Column(4).Width = 10;

        // 班级配置 sheet
        IXLWorksheet gradeSheet = workbook.AddWorksheet("班级配置");
        gradeSheet.Cell(1, 1).Value = "年级";
        gradeSheet.Cell(1, 2).Value = "班级数";
        int row = 2;
        foreach (var g in grades)
        {
            gradeSheet.Cell(row, 1).Value = g.GradeName;
            gradeSheet.Cell(row, 2).Value = g.ClassCount;
            row++;
        }
        gradeSheet.Column(1).Width = 12;
        gradeSheet.Column(2).Width = 10;

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
