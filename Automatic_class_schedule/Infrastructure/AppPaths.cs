using System.IO;

namespace Automatic_class_schedule.Infrastructure;

public static class AppPaths
{
    private static readonly string MyDocs =
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

    // ========== ACS 工作空间 ==========

    /// <summary>ACS 工作空间根目录</summary>
    public static string RootPath => Path.Combine(MyDocs, "ACS");

    /// <summary>工程存放目录</summary>
    public static string ProjectsPath => Path.Combine(RootPath, "Projects");

    /// <summary>导出默认目录</summary>
    public static string OutputPath => Path.Combine(RootPath, "Output");

    /// <summary>默认项目目录（兼容旧引用）</summary>
    public static string DefaultProjectDirectory => ProjectsPath;

    /// <summary>应用本地数据目录（旧版兼容）</summary>
    public static string AppFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Automatic_class_schedule");

    /// <summary>本地设置文件（最近工程等）</summary>
    public static string SettingsFile => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ACS", "settings.json");

    // ========== 旧版兼容（仍使用 AppFolder） ==========

    public static string DataFile => Path.Combine(AppFolder, "school-data.json");
    public static string ExportFolder => Path.Combine(AppFolder, "Export");
    public static string TemplatesFile => Path.Combine(AppFolder, "templates.json");

    // ========== 辅助方法 ==========

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(RootPath);
        Directory.CreateDirectory(ProjectsPath);
        Directory.CreateDirectory(OutputPath);
    }

    public static string GetProjectOutputDir(string projectName)
    {
        return Path.Combine(OutputPath, projectName);
    }

    public static string GetProjectFilePath(string projectName)
    {
        return Path.Combine(ProjectsPath, projectName + ".acsproj");
    }
}