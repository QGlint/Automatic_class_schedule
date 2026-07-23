namespace Automatic_class_schedule.Models;

public sealed class Teacher : Infrastructure.ObservableObject
{
    private Guid _id = Guid.NewGuid();
    private string _name = string.Empty;
    private string _subject = string.Empty;
    private string _role = "教师";

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

    public string Subject
    {
        get => _subject;
        set => SetProperty(ref _subject, value);
    }

    public string Role
    {
        get => _role;
        set => SetProperty(ref _role, value);
    }
}
