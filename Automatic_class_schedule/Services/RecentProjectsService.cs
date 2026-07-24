using System.Text.Json;
using Automatic_class_schedule.Infrastructure;

namespace Automatic_class_schedule.Services;

public class ProjectInfo
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public string LastOpen { get; set; } = "";
}

public class RecentProjectsService
{
    private const int MaxEntries = 10;
    private readonly string _filePath;
    private List<ProjectInfo> _projects;

    public RecentProjectsService()
    {
        _filePath = AppPaths.SettingsFile;
        _projects = Load();
    }

    public IReadOnlyList<ProjectInfo> Projects => _projects.AsReadOnly();

    public void AddOrUpdate(string name, string path)
    {
        _projects.RemoveAll(p => p.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
        _projects.Insert(0, new ProjectInfo
        {
            Name = name,
            Path = path,
            LastOpen = DateTime.Now.ToString("yyyy-MM-dd")
        });
        if (_projects.Count > MaxEntries)
            _projects = _projects.Take(MaxEntries).ToList();
        Save();
    }

    public void Remove(string path)
    {
        _projects.RemoveAll(p => p.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
        Save();
    }

    private List<ProjectInfo> Load()
    {
        try
        {
            if (System.IO.File.Exists(_filePath))
            {
                var json = System.IO.File.ReadAllText(_filePath);
                var list = JsonSerializer.Deserialize<List<ProjectInfo>>(json) ?? new();
                list.RemoveAll(p => string.IsNullOrEmpty(p.Path) || !System.IO.File.Exists(p.Path));
                return list;
            }
        }
        catch { }
        return new();
    }

    private void Save()
    {
        try
        {
            var dir = System.IO.Path.GetDirectoryName(_filePath)!;
            System.IO.Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(_projects, new JsonSerializerOptions { WriteIndented = true });
            System.IO.File.WriteAllText(_filePath, json);
        }
        catch { }
    }
}