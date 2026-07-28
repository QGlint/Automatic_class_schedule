namespace Automatic_class_schedule.Models;

/// <summary>教师生成配置（可保存到项目/全局模板）</summary>
public sealed class TeacherGenConfig
{
    /// <summary>是否替换现有教师配置</summary>
    public bool ReplaceExisting { get; set; } = true;

    /// <summary>全局科目配置：科目名 → (Mode: 0=按班,1=按年级,2=全校, Value: 数量)</summary>
    public Dictionary<string, SubjectGenSetting> GlobalConfigs { get; set; } = new();

    /// <summary>年级自定义配置：年级名 → 该年级的科目配置</summary>
    public Dictionary<string, GradeGenOverride> GradeOverrides { get; set; } = new();

    /// <summary>生成内置默认配置</summary>
    public static TeacherGenConfig CreateDefault(IEnumerable<string> subjectNames)
    {
        var config = new TeacherGenConfig();
        foreach (var subj in subjectNames)
        {
            int mode = IsByGradeDefault(subj) ? 1 : 0;
            int value = GetDefaultValue(subj, mode == 0);
            config.GlobalConfigs[subj] = new SubjectGenSetting { Mode = mode, Value = value };
        }
        return config;
    }

    private static bool IsByGradeDefault(string subject)
        => subject is "物理" or "化学" or "地理" or "生物" or "历史" or "道德" or "音乐" or "美术" or "信息" or "劳动" or "体育";

    private static int GetDefaultValue(string subject, bool isClassesPerTeacherMode)
    {
        if (isClassesPerTeacherMode)
            return subject switch
            {
                "语文" or "数学" or "英语" => 2,
                "物理" or "化学" => 3,
                "地理" or "生物" or "历史" or "道德" => 4,
                "音乐" or "美术" or "信息" or "劳动" => 8,
                "体育" => 4,
                _ => 3
            };
        return subject switch
        {
            "语文" or "数学" or "英语" => 4,
            "物理" or "化学" => 3,
            "地理" or "生物" or "历史" or "道德" => 2,
            "音乐" or "美术" or "信息" or "劳动" => 1,
            "体育" => 2,
            _ => 2
        };
    }
}

/// <summary>单个科目的教师生成设置</summary>
public sealed class SubjectGenSetting
{
    /// <summary>模式：0=按班, 1=按年级, 2=全校</summary>
    public int Mode { get; set; }
    /// <summary>数量</summary>
    public int Value { get; set; }
}

/// <summary>年级级别的教师生成自定义配置</summary>
public sealed class GradeGenOverride
{
    /// <summary>是否启用自定义（false=使用全局配置）</summary>
    public bool UseCustom { get; set; }
    /// <summary>该年级的科目配置</summary>
    public Dictionary<string, SubjectGenSetting> Configs { get; set; } = new();
}
