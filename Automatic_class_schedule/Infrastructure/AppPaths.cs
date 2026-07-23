using System.IO;

namespace Automatic_class_schedule.Infrastructure;

public static class AppPaths
{
    public static string AppFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Automatic_class_schedule");
    public static string DataFile => Path.Combine(AppFolder, "school-data.json");
    public static string ExportFolder => Path.Combine(AppFolder, "Export");
}
