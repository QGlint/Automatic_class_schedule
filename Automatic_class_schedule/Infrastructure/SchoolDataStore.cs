using System.IO;
using System.Text.Json;
using Automatic_class_schedule.Models;

namespace Automatic_class_schedule.Infrastructure;

public sealed class SchoolDataStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    public void Save(SchoolData data)
    {
        Directory.CreateDirectory(AppPaths.AppFolder);
        string json = JsonSerializer.Serialize(data, SerializerOptions);
        File.WriteAllText(AppPaths.DataFile, json);
    }

    public SchoolData Load()
    {
        if (!File.Exists(AppPaths.DataFile))
        {
            return new SchoolData();
        }

        string json = File.ReadAllText(AppPaths.DataFile);
        return JsonSerializer.Deserialize<SchoolData>(json, SerializerOptions) ?? new SchoolData();
    }

    public Task SaveAsync(SchoolData data, CancellationToken cancellationToken = default)
    {
        Save(data);
        return Task.CompletedTask;
    }

    public Task<SchoolData> LoadAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Load());
    }
}
