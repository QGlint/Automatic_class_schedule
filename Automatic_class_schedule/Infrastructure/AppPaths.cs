using System.IO;

namespace Automatic_class_schedule.Infrastructure;

public static class AppPaths
{
    public static string AppFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Automatic_class_schedule");
    public static string DataFile => Path.Combine(AppFolder, "school-data.json");
    public static string ExportFolder => Path.Combine(AppFolder, "Export");
    public static string TemplatesFile => Path.Combine(AppFolder, "templates.json");

    /// <summary>用户文档下的项目默认存储目录</summary>
    public static string DefaultProjectDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "acs");
}
