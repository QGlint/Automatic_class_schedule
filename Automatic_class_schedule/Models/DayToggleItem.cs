namespace Automatic_class_schedule.Models;

/// <summary>晚自习天选择按钮项</summary>
public sealed class DayToggleItem : Infrastructure.ObservableObject
{
    private bool _isSelected;

    public string Label { get; set; } = "";
    public int Index { get; set; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
