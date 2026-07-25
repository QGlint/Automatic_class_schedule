using System.Collections.Generic;

namespace Automatic_class_schedule.Infrastructure;

/// <summary>分布规则选项资源提供器（用于 DataGrid 模板内 ComboBox 绑定）</summary>
public sealed class DistributionRuleOptionsProvider
{
    public List<string> Options { get; } = new() { "均匀分布", "每日至少一次", "集中安排" };
}

/// <summary>课程类别选项资源提供器</summary>
public sealed class CategoryOptionsProvider
{
    public List<string> Options { get; } = new() { "主科", "理科", "文科", "副科", "自习", "自定义" };
}

/// <summary>固定课程范围选项资源提供器</summary>
public sealed class FixedLessonScopeProvider
{
    public List<string> Options { get; } = new() { "全校", "七年级", "八年级", "九年级", "七+八年级", "七+九年级", "八+九年级" };
}
