namespace Automatic_class_schedule.Models;

public sealed class SubjectDefinition : Infrastructure.ObservableObject
{
    private Guid _id = Guid.NewGuid();
    private string _name = string.Empty;
    private string _category = string.Empty;
    private int _defaultWeeklyCount = 4;

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

    public int DefaultWeeklyCount
    {
        get => _defaultWeeklyCount;
        set => SetProperty(ref _defaultWeeklyCount, value);
    }

    private string _distributionRule = "均匀分布";
    private string _gradeName = string.Empty;

    public string DistributionRule
    {
        get => _distributionRule;
        set => SetProperty(ref _distributionRule, value);
    }

    public string GradeName
    {
        get => _gradeName;
        set => SetProperty(ref _gradeName, value);
    }
}
