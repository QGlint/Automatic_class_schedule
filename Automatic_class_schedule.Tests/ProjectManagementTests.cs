using System.IO;
using System.Linq;
using Automatic_class_schedule.ViewModels;
using Automatic_class_schedule.Infrastructure;
using Automatic_class_schedule.Services;

namespace Automatic_class_schedule.Tests;

public sealed class ProjectManagementTests : IDisposable
{
    private readonly MainViewModel _vm;
    private readonly string _testDir;

    public ProjectManagementTests()
    {
        AppPaths.EnsureDirectories();
        _testDir = Path.Combine(Path.GetTempPath(), "ACSTest_" + Path.GetRandomFileName());
        Directory.CreateDirectory(_testDir);
        _vm = new MainViewModel();
    }

    public void Dispose()
    {
        try
        {
            var testFiles = new[] { "UT_Snap", "UT_Open", "UT_Mod", "UT_Close", "UT_SaveCheck",
                                    "测试", "已存在", "旧项目", "自定义路径测试", "打开测试", "打开测试2" };
            foreach (var name in testFiles)
            {
                var path = AppPaths.GetProjectFilePath(name);
                if (File.Exists(path)) File.Delete(path);
            }
            if (Directory.Exists(_testDir))
                Directory.Delete(_testDir, recursive: true);
        }
        catch { }
    }

    // ── Unsaved changes (HasUnsavedChanges) ──

    [Fact]
    public void HasUnsavedChanges_AfterCreateProject_IsFalse()
    {
        _vm.ProjectName = "UT_Snap";
        _vm.CreateProject();
        Assert.False(_vm.HasUnsavedChanges);
    }

    [Fact]
    public void HasUnsavedChanges_AfterModifyingData_IsTrue()
    {
        _vm.ProjectName = "UT_Mod";
        _vm.CreateProject();
        _vm.DaysPerWeek = _vm.DaysPerWeek + 1;
        Assert.True(_vm.HasUnsavedChanges);
    }

    [Fact]
    public void HasUnsavedChanges_AfterSave_IsFalse()
    {
        _vm.ProjectName = "UT_SaveCheck";
        _vm.CreateProject();
        _vm.DaysPerWeek = _vm.DaysPerWeek + 1;
        Assert.True(_vm.HasUnsavedChanges);
        _vm.SaveProject(_vm.ProjectFilePath);
        Assert.False(_vm.HasUnsavedChanges);
    }

    [Fact]
    public void HasUnsavedChanges_AfterOpenProject_IsFalse()
    {
        _vm.ProjectName = "UT_Open";
        _vm.CreateProject();
        var path = _vm.ProjectFilePath;
        Assert.False(_vm.HasUnsavedChanges);

        var vm2 = new MainViewModel();
        vm2.OpenProject(path);
        Assert.False(vm2.HasUnsavedChanges);
    }

    [Fact]
    public void HasUnsavedChanges_NoActiveProject_IsFalse()
    {
        Assert.False(_vm.HasActiveProject);
        Assert.False(_vm.HasUnsavedChanges);
    }

    // ── CloseProject ──

    [Fact]
    public void CloseProject_ResetsState()
    {
        _vm.ProjectName = "UT_Close";
        _vm.CreateProject();
        Assert.True(_vm.HasActiveProject);

        _vm.CloseProject();

        Assert.False(_vm.HasActiveProject);
        Assert.Equal("", _vm.ProjectFilePath);
        Assert.Equal("", _vm.ProjectName);
    }

    [Fact]
    public void CloseProject_NoActiveProject_NoOp()
    {
        Assert.False(_vm.HasActiveProject);
        _vm.CloseProject();
        Assert.False(_vm.HasActiveProject);
    }

    // ── Snapshot lifecycle ──

    [Fact]
    public void SaveProject_CapturesSnapshot()
    {
        _vm.ProjectName = "UT_Snap";
        _vm.CreateProject();
        _vm.DaysPerWeek = _vm.DaysPerWeek + 1;
        Assert.True(_vm.HasUnsavedChanges);

        _vm.SaveProject(_vm.ProjectFilePath);
        Assert.False(_vm.HasUnsavedChanges);
    }

    [Fact]
    public void ModifyAfterSave_ChangesUnsavedFlag()
    {
        _vm.ProjectName = "UT_Snap";
        _vm.CreateProject();

        _vm.DaysPerWeek = _vm.DaysPerWeek + 1;
        Assert.True(_vm.HasUnsavedChanges);

        _vm.SaveProject(_vm.ProjectFilePath);
        Assert.False(_vm.HasUnsavedChanges);

        _vm.DaysPerWeek = _vm.DaysPerWeek + 2;
        Assert.True(_vm.HasUnsavedChanges);
    }

    // ── HomePageProjects ──

    [Fact]
    public void HomePageProjects_ContainsCreatedProject()
    {
        _vm.ProjectName = "UT_Snap";
        _vm.CreateProject();
        var projects = _vm.HomePageProjects;
        Assert.Contains(projects, p => p.Name == "UT_Snap");
    }

    [Fact]
    public void HomePageProjects_AfterCloseProject_StillContainsProject()
    {
        _vm.ProjectName = "UT_Close";
        _vm.CreateProject();
        _vm.CloseProject();
        var projects = _vm.HomePageProjects;
        Assert.Contains(projects, p => p.Name == "UT_Close");
    }

    // ── RecentProjectsService stale filtering ──

    [Fact]
    public void RecentProjectsService_FiltersStaleEntriesOnLoad()
    {
        var settingsDir = Path.GetDirectoryName(AppPaths.SettingsFile)!;
        Directory.CreateDirectory(settingsDir);

        var validPath = Path.Combine(_testDir, "valid.acsproj");
        File.WriteAllText(validPath, "dummy");

        var stale = new List<ProjectInfo>
        {
            new() { Name = "存活", Path = validPath, LastOpen = "2024-01-01" },
            new() { Name = "已删除", Path = Path.Combine(_testDir, "deleted.acsproj"), LastOpen = "2024-01-01" },
            new() { Name = "空路径", Path = "", LastOpen = "2024-01-01" }
        };
        var json = System.Text.Json.JsonSerializer.Serialize(stale);
        File.WriteAllText(AppPaths.SettingsFile, json);

        var service = new RecentProjectsService();
        Assert.Single(service.Projects);
        Assert.Equal("存活", service.Projects[0].Name);
    }

    // ── Serialization roundtrip ──

    [Fact]
    public void SerializeThenDeserialize_Roundtrip_PreservesSettings()
    {
        _vm.ProjectName = "UT_Snap";
        _vm.CreateProject();
        _vm.DaysPerWeek = 6;
        _vm.PeriodsPerDay = 8;
        _vm.SaveProject(_vm.ProjectFilePath);

        var vm2 = new MainViewModel();
        vm2.OpenProject(_vm.ProjectFilePath);

        Assert.Equal(6, vm2.DaysPerWeek);
        Assert.Equal(8, vm2.PeriodsPerDay);
    }

    [Fact]
    public void SerializeThenDeserialize_Roundtrip_PreservesData()
    {
        _vm.ProjectName = "UT_Snap";
        _vm.CreateProject();
        var path = _vm.ProjectFilePath;

        using var stream = File.OpenRead(path);
        var deserialized = SchoolDataSerializer.Deserialize(stream);

        Assert.NotNull(deserialized);
        Assert.NotEmpty(deserialized.GradeInputs);
    }

    // ── HasUnsavedChanges with collection modification ──

    [Fact]
    public void HasUnsavedChanges_AfterAddingGrade_IsTrue()
    {
        _vm.ProjectName = "UT_Snap";
        _vm.CreateProject();
        Assert.False(_vm.HasUnsavedChanges);

        _vm.GradeInputs.Add(new Models.GradeInput { GradeName = "新年级" });

        Assert.True(_vm.HasUnsavedChanges);
    }

    [Fact]
    public void HasUnsavedChanges_AfterSaveWithCollectionMod_IsFalse()
    {
        _vm.ProjectName = "UT_Snap";
        _vm.CreateProject();
        _vm.GradeInputs.Add(new Models.GradeInput { GradeName = "新年级" });
        Assert.True(_vm.HasUnsavedChanges);

        _vm.SaveProject(_vm.ProjectFilePath);
        Assert.False(_vm.HasUnsavedChanges);
    }

    // ── OpenProject with custom path ──

    [Fact]
    public void OpenProject_WithValidPath_ActivatesAndSetsFilePath()
    {
        _vm.ProjectName = "UT_Open";
        _vm.CreateProject();
        var path = _vm.ProjectFilePath;

        var vm2 = new MainViewModel();
        vm2.OpenProject(path);

        Assert.True(vm2.HasActiveProject);
        Assert.Equal(path, vm2.ProjectFilePath);
    }

    [Fact]
    public void OpenProject_NonExistentFile_ShowsError()
    {
        _vm.OpenProject(Path.Combine(_testDir, "nonexistent.acsproj"));
        Assert.False(_vm.HasActiveProject);
        Assert.Contains("不存在", _vm.StatusMessage);
    }

    // ── CreateProject error paths ──

    [Fact]
    public void CreateProject_EmptyName_ShowsError()
    {
        _vm.ProjectName = "";
        _vm.CreateProject();
        Assert.False(_vm.HasActiveProject);
        Assert.Equal("请输入项目名称", _vm.StatusMessage);
    }

    [Fact]
    public void CreateProject_FileAlreadyExists_ShowsError()
    {
        var filePath = AppPaths.GetProjectFilePath("已存在");
        File.WriteAllText(filePath, "");
        _vm.ProjectName = "已存在";
        _vm.CreateProject();
        Assert.False(_vm.HasActiveProject);
        Assert.Contains("已存在", _vm.StatusMessage);
    }
}
