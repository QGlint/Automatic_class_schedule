namespace Automatic_class_schedule.Models;

public sealed class SubjectDefinition : Infrastructure.ObservableObject
{
    private Guid _id = Guid.NewGuid();
    private string _name = string.Empty;
    private string _category = string.Empty;

    public Guid Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string Category
    {
        get => _category;
        set => SetProperty(ref _category, value);
    }
}
