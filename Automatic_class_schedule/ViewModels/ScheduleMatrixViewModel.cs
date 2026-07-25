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
}
