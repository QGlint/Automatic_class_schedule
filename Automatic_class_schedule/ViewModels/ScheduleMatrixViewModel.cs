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
