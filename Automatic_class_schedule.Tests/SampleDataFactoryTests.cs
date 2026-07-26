using Automatic_class_schedule.Services;

namespace Automatic_class_schedule.Tests;

public sealed class SampleDataFactoryTests
{
    [Fact]
    public void Create_ReturnsValidSchoolData()
    {
        var data = SampleDataFactory.Create();
        Assert.NotNull(data);
        Assert.Equal("", data.Settings.SchoolName);
        Assert.Equal(5, data.Settings.DaysPerWeek);
        Assert.Equal(8, data.Settings.PeriodsPerDay);
        Assert.Equal(4, data.Settings.MorningPeriods);
        Assert.Equal(4, data.Settings.AfternoonPeriods);
    }

    [Fact]
    public void Create_HasCorrectGradeCounts()
    {
        var data = SampleDataFactory.Create();
        Assert.Equal(3, data.GradeInputs.Count);
        Assert.Contains(data.GradeInputs, g => g.GradeName == "七年级" && g.ClassCount == 8);
        Assert.Contains(data.GradeInputs, g => g.GradeName == "八年级" && g.ClassCount == 8);
        Assert.Contains(data.GradeInputs, g => g.GradeName == "九年级" && g.ClassCount == 6);
    }

    [Fact]
    public void Create_HasAllSubjects()
    {
        var data = SampleDataFactory.Create();
        string[] expected = { "语文", "数学", "英语", "物理", "化学", "生物", "历史", "地理", "道德", "体育", "音乐", "美术", "信息", "劳动" };
        Assert.Equal(expected.Length, data.Subjects.Count);
        foreach (var name in expected)
            Assert.Contains(data.Subjects, s => s.Name == name);
    }

    [Fact]
    public void Create_HasClasses()
    {
        var data = SampleDataFactory.Create();
        int expected = 8 + 8 + 6;
        Assert.Equal(expected, data.Classes.Count);
    }

    [Fact]
    public void Create_HasTeacherAssignments()
    {
        var data = SampleDataFactory.Create();
        Assert.NotEmpty(data.TeacherAssignments);
    }

    [Fact]
    public void Create_HasRequirements()
    {
        var data = SampleDataFactory.Create();
        Assert.NotEmpty(data.Requirements);
    }

    [Fact]
    public void Create_HasScheduleEntries()
    {
        var data = SampleDataFactory.Create();
        Assert.NotEmpty(data.ScheduleEntries);
    }
}