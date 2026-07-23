using System.Collections.ObjectModel;

namespace Automatic_class_schedule.ViewModels;

public sealed class ScheduleRowViewModel
{
    public int DayIndex { get; init; }

    public string DayName { get; init; } = string.Empty;

    public ObservableCollection<ScheduleCellViewModel> Cells { get; } = new();
}
