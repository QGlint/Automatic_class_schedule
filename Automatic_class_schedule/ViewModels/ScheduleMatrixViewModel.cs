using System.Collections.ObjectModel;

namespace Automatic_class_schedule.ViewModels;

public sealed class ScheduleMatrixRow
{
    public string PeriodLabel { get; set; } = string.Empty;
    public int PeriodIndex { get; set; }
    public bool IsOdd { get; set; }
    public ObservableCollection<ScheduleMatrixCell> Cells { get; set; } = new();
}

public sealed class ScheduleMatrixCell
{
    public string Subject { get; set; } = string.Empty;
    public string TeacherName { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public bool IsEmpty => string.IsNullOrWhiteSpace(Subject);
}

public sealed class DayTabItem
{
    public int Index { get; set; }
    public string Name { get; set; } = string.Empty;
}
