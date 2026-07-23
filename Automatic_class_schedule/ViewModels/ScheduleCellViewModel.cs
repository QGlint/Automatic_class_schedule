using System.Collections.ObjectModel;
using Automatic_class_schedule.Models;

namespace Automatic_class_schedule.ViewModels;

public sealed class ScheduleCellViewModel
{
    public int DayIndex { get; init; }

    public int PeriodIndex { get; init; }

    public string PeriodLabel => $"第{PeriodIndex}节";

    public ObservableCollection<ScheduleEntry> Entries { get; } = new();
}
