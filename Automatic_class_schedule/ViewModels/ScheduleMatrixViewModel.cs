using System.Collections.ObjectModel;
using Automatic_class_schedule.Models;

namespace Automatic_class_schedule.ViewModels;

public sealed class PeriodGroup
{
    public string PeriodLabel { get; set; } = string.Empty;
    public int PeriodIndex { get; set; }
    public ObservableCollection<ScheduleGridRow> ClassRows { get; set; } = new();
}

public sealed class ScheduleGridRow
{
    public string PeriodLabel { get; set; } = string.Empty;
    public int PeriodIndex { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public Guid ClassId { get; set; }
    public ObservableCollection<ScheduleGridCell> Cells { get; set; } = new();
}

public sealed class ScheduleGridCell
{
    public int DayIndex { get; set; }
    public int PeriodIndex { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string TeacherName { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public Guid EntryId { get; set; }
    public ScheduleEntry? Entry { get; set; }
    public bool IsEmpty => string.IsNullOrWhiteSpace(Subject);
    /// <summary>该单元格是否存在冲突（标红）</summary>
    public bool HasConflict { get; set; }
    /// <summary>是否为手动拖拽过的课程（蓝色字体）</summary>
    public bool IsManuallyMoved { get; set; }
    /// <summary>单元格背景色（冲突时红色底）</summary>
    public Microsoft.UI.Xaml.Media.SolidColorBrush CellBackground =>
        HasConflict
            ? new Microsoft.UI.Xaml.Media.SolidColorBrush(new Windows.UI.Color { A = 40, R = 255, G = 0, B = 0 })
            : new Microsoft.UI.Xaml.Media.SolidColorBrush(new Windows.UI.Color { A = 0, R = 255, G = 255, B = 255 });
    /// <summary>科目字体颜色（手动拖拽过蓝色）</summary>
    public Microsoft.UI.Xaml.Media.SolidColorBrush SubjectForeground =>
        IsManuallyMoved
            ? new Microsoft.UI.Xaml.Media.SolidColorBrush(new Windows.UI.Color { A = 255, R = 0, G = 102, B = 204 })
            : new Microsoft.UI.Xaml.Media.SolidColorBrush(new Windows.UI.Color { A = 255, R = 0, G = 0, B = 0 });

    /// <summary>同时段所有条目（教师课表连班用）</summary>
    public List<ScheduleEntry> AllEntries { get; set; } = new();

    /// <summary>显示文本（连班时显示 "体育 七1+七2"）</summary>
    public string DisplayText
    {
        get
        {
            if (IsEmpty) return string.Empty;
            if (AllEntries.Count <= 1) return $"{Subject} {ClassName}";
            var classes = string.Join("+", AllEntries.Select(e => e.ClassName).Distinct().Take(2));
            return $"{Subject} {classes}";
        }
    }
}

public sealed class DayTabItem
{
    public int Index { get; set; }
    public string Name { get; set; } = string.Empty;
}

/// <summary>年级总表 - 双层表头的天列信息</summary>
public sealed class GradeDayHeader
{
    public string DayName { get; set; } = string.Empty;
    public int DayIndex { get; set; }
    /// <summary>该天下的节次编号列表 (1,2,3...)</summary>
    public ObservableCollection<int> PeriodNumbers { get; set; } = new();
}

/// <summary>年级总表 - 班级行（每行一个班级，Cells按 day×period 平铺）</summary>
public sealed class GradeClassRow
{
    public string ClassName { get; set; } = string.Empty;
    public Guid ClassId { get; set; }
    public ObservableCollection<ScheduleGridCell> Cells { get; set; } = new();
    /// <summary>该班级课程数量与配置不符（表头标红）</summary>
    public bool HasCountError { get; set; }
    /// <summary>表头背景色（数量不符时红色）</summary>
    public Microsoft.UI.Xaml.Media.SolidColorBrush HeaderBackground =>
        HasCountError
            ? new Microsoft.UI.Xaml.Media.SolidColorBrush(new Windows.UI.Color { A = 60, R = 255, G = 80, B = 80 })
            : new Microsoft.UI.Xaml.Media.SolidColorBrush(new Windows.UI.Color { A = 0, R = 255, G = 255, B = 255 });
}
