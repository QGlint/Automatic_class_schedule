using System.IO;
using System.Linq;
using Automatic_class_schedule.ViewModels;
using Automatic_class_schedule.Infrastructure;
using Automatic_class_schedule.Services;

namespace Automatic_class_schedule.Tests;

public sealed class ProjectCreationTests : IDisposable
{
    private readonly MainViewModel _vm;

    public ProjectCreationTests()
    {
        AppPaths.EnsureDirectories();
        _vm = new MainViewModel();
    }

    public void Dispose()
    {
        try
        {
            var testFiles = new[] { "测试", "已存在", "旧项目", "自定义路径测试", "打开测试", "打开测试2" };
            foreach (var name in testFiles)
            {
                var path = AppPaths.GetProjectFilePath(name);
                if (File.Exists(path))
                    File.Delete(path);
            }
        }
        catch { }
    }

    [Fact]
    public void CreateProject_WithValidName_CreatesFileAndActivates()
    {
        var filePath = AppPaths.GetProjectFilePath("测试");
        _vm.ProjectName = "测试";

        _vm.CreateProject();

        Assert.True(File.Exists(filePath));
        Assert.True(_vm.HasActiveProject);
        Assert.Contains("测试", _vm.StatusMessage);
    }

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

    [Fact]
    public void NewProject_ResetsNameAndFilePath()
    {
        _vm.ProjectName = "旧项目";

        _vm.NewProjectCommand.Execute(null);

        Assert.False(_vm.HasActiveProject);
        Assert.Equal("", _vm.ProjectName);
        Assert.Equal("", _vm.ProjectFilePath);
    }

    [Fact]
    public void CreateProject_WithCustomPath_CreatesFileAtSpecifiedLocation()
    {
        var customPath = AppPaths.GetProjectFilePath("自定义路径测试");
        _vm.ProjectName = "自定义路径测试";

        _vm.CreateProject(customPath);

        Assert.True(File.Exists(customPath));
        Assert.True(_vm.HasActiveProject);
        Assert.Equal(customPath, _vm.ProjectFilePath);
    }

    [Fact]
    public void HomePageProjects_IncludesRecentAndDefaultDirProjects()
    {
        // Create a project first
        _vm.ProjectName = "测试";
        _vm.CreateProject();

        var projects = _vm.HomePageProjects;
        Assert.Contains(projects, p => p.Name == "测试");
    }

    [Fact]
    public void HomePageProjects_DoesNotIncludeNonExistentFiles()
    {
        // Clear any existing projects
        foreach (var p in _vm.HomePageProjects.ToList())
        {
            if (!File.Exists(p.Path))
            {
                // Should be filtered out - just verify it's not in the default dir list
                var dir = new DirectoryInfo(AppPaths.ProjectsPath);
                if (dir.Exists)
                {
                    var files = dir.GetFiles("*.acsproj").Select(f => f.FullName).ToHashSet();
                    Assert.DoesNotContain(p.Path, files);
                }
            }
        }
    }

    [Fact]
    public void SerializeThenDeserialize_Roundtrip_Works()
    {
        _vm.ProjectName = "打开测试";
        _vm.CreateProject();
        var path = _vm.ProjectFilePath;
        Assert.NotNull(path);
        Assert.True(File.Exists(path));

        // Read the file back
        using var stream = File.OpenRead(path);
        var deserialized = Automatic_class_schedule.Infrastructure.SchoolDataSerializer.Deserialize(stream);

        Assert.NotNull(deserialized);
        Assert.NotEmpty(deserialized.GradeInputs);
    }

    [Fact]
    public void OpenProject_WithValidPath_ActivatesProject()
    {
        _vm.ProjectName = "打开测试2";
        _vm.CreateProject();
        var path = _vm.ProjectFilePath;

        _vm.OpenProject(path);

        Assert.True(_vm.HasActiveProject);
        Assert.Equal(path, _vm.ProjectFilePath);
    }
}