namespace Automatic_class_schedule.Models;

public sealed class GradeInput : Infrastructure.ObservableObject
{
    private string _gradeName = string.Empty;
    private int _classCount;

    public string GradeName
    {
        get => _gradeName;
        set => SetProperty(ref _gradeName, value);
    }

    public int ClassCount
    {
        get => _classCount;
        set => SetProperty(ref _classCount, value);
    }
}
