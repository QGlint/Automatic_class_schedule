using System.Collections.ObjectModel;
using Automatic_class_schedule.Models;

namespace Automatic_class_schedule.ViewModels;

public sealed class SchedulePeriodRowViewModel
{
    public int PeriodIndex { get; set; }
    public string PeriodLabel { get; set; } = string.Empty;
    public string PeriodType { get; set; } = string.Empty;
    public ObservableCollection<SchedulePeriodDayColumn> DayColumns { get; } = new();
}

public sealed class SchedulePeriodDayColumn
{
    public int DayIndex { get; set; }
    public string DayName { get; set; } = string.Empty;
    public ObservableCollection<ScheduleEntry> Entries { get; } = new();
}
