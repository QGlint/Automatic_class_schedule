using System.IO;
using Automatic_class_schedule.ViewModels;
using Automatic_class_schedule.Infrastructure;

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
        // 清理测试产生的工程文件
        try
        {
            var testFiles = new[] { "测试", "已存在", "旧项目" };
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
}