using System.IO;
using Automatic_class_schedule.ViewModels;

namespace Automatic_class_schedule.Tests;

public sealed class ProjectCreationTests : IDisposable
{
    private readonly string _tempDir;
    private readonly MainViewModel _vm;

    public ProjectCreationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "asc_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _vm = new MainViewModel();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void CreateProject_WithValidNameAndPath_CreatesFileAndActivates()
    {
        var filePath = Path.Combine(_tempDir, "测试.acsproj");
        _vm.ProjectName = "测试";
        _vm.ProjectFilePath = filePath;

        _vm.CreateProject();

        Assert.True(File.Exists(filePath));
        Assert.True(_vm.HasActiveProject);
        Assert.Contains("测试", _vm.StatusMessage);
    }

    [Fact]
    public void CreateProject_EmptyName_ShowsError()
    {
        _vm.ProjectName = "";
        _vm.ProjectFilePath = Path.Combine(_tempDir, "test.acsproj");

        _vm.CreateProject();

        Assert.False(_vm.HasActiveProject);
        Assert.Equal("请输入项目名称", _vm.StatusMessage);
    }

    [Fact]
    public void CreateProject_FileAlreadyExists_ShowsError()
    {
        var filePath = Path.Combine(_tempDir, "已存在.acsproj");
        File.WriteAllText(filePath, "");
        _vm.ProjectName = "已存在";
        _vm.ProjectFilePath = filePath;

        _vm.CreateProject();

        Assert.False(_vm.HasActiveProject);
        Assert.Contains("已存在", _vm.StatusMessage);
    }

    [Fact]
    public void NewProject_ResetsNameAndFilePath()
    {
        _vm.ProjectName = "旧项目";
        _vm.ProjectFilePath = Path.Combine(_tempDir, "旧项目.acsproj");

        _vm.NewProjectCommand.Execute(null);

        Assert.False(_vm.HasActiveProject);
        Assert.Equal("", _vm.ProjectName);
        Assert.Equal("", _vm.ProjectFilePath);
    }
}