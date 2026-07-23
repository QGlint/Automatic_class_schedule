namespace Automatic_class_schedule.Models;

public sealed class SchoolClass : Automatic_class_schedule.Infrastructure.ObservableObject
{
    private Guid _id = Guid.NewGuid();
    private string _gradeName = string.Empty;
    private int _classNumber;
    private string _name = string.Empty;

    public Guid Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public string GradeName
    {
        get => _gradeName;
        set
        {
            if (SetProperty(ref _gradeName, value))
            {
                OnPropertyChanged(nameof(DisplayName));
            }
        }
    }

    public int ClassNumber
    {
        get => _classNumber;
        set
        {
            if (SetProperty(ref _classNumber, value))
            {
                OnPropertyChanged(nameof(DisplayName));
            }
        }
    }

    public string Name
    {
        get => _name;
        set
        {
            if (SetProperty(ref _name, value))
            {
                OnPropertyChanged(nameof(DisplayName));
            }
        }
    }

    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? $"{GradeName}{ClassNumber}班" : Name;
}
