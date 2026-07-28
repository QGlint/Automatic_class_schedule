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
    /// <summary>教师生成配置全局默认文件</summary>
    public static string TeacherGenDefaultFile => Path.Combine(AppFolder, "teacher-gen-default.json");

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
        return Path.Combine(ProjectsPath, projectName, projectName + ".acsproj");
    }

    // ========== 项目目录格式（v3：文件夹 + 内部 .acsproj 文件） ==========

    /// <summary>根据 .acsproj 文件路径获取项目主文件路径（即自身）</summary>
    public static string GetProjectMainFile(string acsprojFilePath)
        => acsprojFilePath;

    /// <summary>获取项目目录内的缓存子目录路径（与 .acsproj 文件同级）</summary>
    public static string GetProjectCacheDir(string acsprojFilePath)
        => Path.Combine(Path.GetDirectoryName(acsprojFilePath)!, "cache");

    /// <summary>获取指定缓存子文件的完整路径</summary>
    public static string GetCacheFilePath(string acsprojFilePath, string cacheFileName)
        => Path.Combine(GetProjectCacheDir(acsprojFilePath), cacheFileName);

    /// <summary>判断路径是否为 .acsproj 项目文件</summary>
    public static bool IsProjectFile(string path)
        => !string.IsNullOrEmpty(path) && File.Exists(path) && path.EndsWith(".acsproj", StringComparison.OrdinalIgnoreCase);

    /// <summary>判断路径是否为旧版目录格式项目（v2：.acsproj 为目录名）</summary>
    public static bool IsLegacyProjectDirectory(string path)
        => !string.IsNullOrEmpty(path) && Directory.Exists(path)
           && File.Exists(Path.Combine(path, "project.acs"));
}