using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using Automatic_class_schedule.Models;

namespace Automatic_class_schedule.Tests;

/// <summary>验证课程配置自定义状态触发逻辑</summary>
public sealed class CourseConfigCustomStateTests
{
    /// <summary>模拟 MainViewModel 中的自定义状态检测模式</summary>
    private sealed class CustomStateSimulator
    {
        private bool _suppress;
        public bool IsCustom { get; private set; }
        public string SelectedTemplate { get; private set; } = string.Empty;
        public ObservableCollection<SubjectDefinition> Subjects { get; } = new();

        public CustomStateSimulator()
        {
            Subjects.CollectionChanged += (_, e) =>
            {
                if (e.NewItems != null)
                    foreach (SubjectDefinition item in e.NewItems)
                        item.PropertyChanged += OnItemChanged;
                if (e.OldItems != null)
                    foreach (SubjectDefinition item in e.OldItems)
                        item.PropertyChanged -= OnItemChanged;
                if (!_suppress) MarkCustom();
            };
        }

        private void OnItemChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (!_suppress) MarkCustom();
        }

        private void MarkCustom()
        {
            IsCustom = true;
            // 新版逻辑：不清空 SelectedTemplate，保持 ComboBox 可交互
        }

        /// <summary>模拟下拉关闭时的重载逻辑</summary>
        public void DropDownClosed()
        {
            if (IsCustom && !string.IsNullOrEmpty(SelectedTemplate))
            {
                IsCustom = false;
                // 实际中会调用 LoadCourseTemplate
            }
        }

        public void LoadTemplate(string name, IEnumerable<SubjectDefinition> items)
        {
            _suppress = true;
            SelectedTemplate = name;
            Subjects.Clear();
            foreach (var item in items)
                Subjects.Add(item);
            _suppress = false;
            IsCustom = false;
        }
    }

    [Fact]
    public void AddSubject_TriggersCustom()
    {
        var sim = new CustomStateSimulator();
        sim.LoadTemplate("初中标准", new[] { new SubjectDefinition { Name = "语文", DefaultWeeklyCount = 7 } });
        Assert.False(sim.IsCustom);
        Assert.Equal("初中标准", sim.SelectedTemplate);

        // 添加科目 → 触发自定义
        sim.Subjects.Add(new SubjectDefinition { Name = "物理", DefaultWeeklyCount = 3 });
        Assert.True(sim.IsCustom);
        Assert.Equal("初中标准", sim.SelectedTemplate); // 选中保持不变，ComboBox可交互
    }

    [Fact]
    public void ModifySubjectName_TriggersCustom()
    {
        var sim = new CustomStateSimulator();
        sim.LoadTemplate("初中标准", new[] { new SubjectDefinition { Name = "语文", DefaultWeeklyCount = 7 } });
        Assert.False(sim.IsCustom);

        // 修改课程名称 → 触发自定义
        sim.Subjects[0].Name = "数学";
        Assert.True(sim.IsCustom);
        Assert.Equal("初中标准", sim.SelectedTemplate); // 保持不变
    }

    [Fact]
    public void ModifyWeeklyCount_TriggersCustom()
    {
        var sim = new CustomStateSimulator();
        sim.LoadTemplate("初中标准", new[] { new SubjectDefinition { Name = "语文", DefaultWeeklyCount = 7 } });

        sim.Subjects[0].DefaultWeeklyCount = 5;
        Assert.True(sim.IsCustom);
    }

    [Fact]
    public void ModifyCategory_TriggersCustom()
    {
        var sim = new CustomStateSimulator();
        sim.LoadTemplate("初中标准", new[] { new SubjectDefinition { Name = "语文", Category = "主课" } });

        sim.Subjects[0].Category = "副科";
        Assert.True(sim.IsCustom);
    }

    [Fact]
    public void ModifyDistributionRule_TriggersCustom()
    {
        var sim = new CustomStateSimulator();
        sim.LoadTemplate("初中标准", new[] { new SubjectDefinition { Name = "语文", DistributionRule = "均匀分布" } });

        sim.Subjects[0].DistributionRule = "隔天分布";
        Assert.True(sim.IsCustom);
    }

    [Fact]
    public void RemoveSubject_TriggersCustom()
    {
        var sim = new CustomStateSimulator();
        sim.LoadTemplate("初中标准", new[]
        {
            new SubjectDefinition { Name = "语文" },
            new SubjectDefinition { Name = "数学" }
        });

        sim.Subjects.RemoveAt(1);
        Assert.True(sim.IsCustom);
    }

    [Fact]
    public void LoadTemplate_ResetsCustom_AllowsReselection()
    {
        var sim = new CustomStateSimulator();
        sim.LoadTemplate("初中标准", new[] { new SubjectDefinition { Name = "语文", DefaultWeeklyCount = 7 } });

        // 修改 → 自定义
        sim.Subjects[0].DefaultWeeklyCount = 3;
        Assert.True(sim.IsCustom);
        Assert.Equal("初中标准", sim.SelectedTemplate); // 保持不变

        // 重新加载模板 → 恢复
        sim.LoadTemplate("初中标准", new[] { new SubjectDefinition { Name = "语文", DefaultWeeklyCount = 7 } });
        Assert.False(sim.IsCustom);
        Assert.Equal("初中标准", sim.SelectedTemplate);
    }

    [Fact]
    public void DeleteAddedContent_StillCustom_CanReselectTemplate()
    {
        var sim = new CustomStateSimulator();
        sim.LoadTemplate("初中标准", new[] { new SubjectDefinition { Name = "语文" } });

        // 添加后删除 → 依然是自定义（因为CollectionChanged触发了）
        var added = new SubjectDefinition { Name = "物理" };
        sim.Subjects.Add(added);
        Assert.True(sim.IsCustom);

        sim.Subjects.Remove(added);
        Assert.True(sim.IsCustom); // 仍为自定义

        // 关键：SelectedTemplate 保持为"初中标准"，下拉关闭时可触发重载
        Assert.Equal("初中标准", sim.SelectedTemplate);

        // 模拟下拉关闭 → 自动重载模板
        sim.DropDownClosed();
        Assert.False(sim.IsCustom);

        // 重新选择模板 → 恢复正常
        sim.LoadTemplate("初中标准", new[] { new SubjectDefinition { Name = "语文" } });
        Assert.False(sim.IsCustom);
        Assert.Equal("初中标准", sim.SelectedTemplate);
    }

    [Fact]
    public void RemovedItem_PropertyChange_DoesNotTrigger()
    {
        var sim = new CustomStateSimulator();
        var item = new SubjectDefinition { Name = "语文" };
        sim.LoadTemplate("初中标准", new[] { item });

        // 重置状态
        Assert.False(sim.IsCustom);

        // 移除后修改不应触发
        sim.Subjects.Remove(item);
        // 此时已经因为Remove触发了custom
        Assert.True(sim.IsCustom);

        // 重新加载
        sim.LoadTemplate("初中标准", new[] { new SubjectDefinition { Name = "数学" } });
        Assert.False(sim.IsCustom);

        // 修改已移除的item不应触发
        item.Name = "英语";
        Assert.False(sim.IsCustom); // 已取消订阅
    }

    [Fact]
    public void ModifyConfig_ThenReselectTemplate_ContentRestored()
    {
        // 模拟完整流程：加载初中标准 → 修改配置 → 重新点击初中标准 → 内容恢复
        var sim = new CustomStateSimulator();

        // 1. 加载"初中标准"模板
        sim.LoadTemplate("初中标准", new[]
        {
            new SubjectDefinition { Name = "语文", DefaultWeeklyCount = 7, Category = "主课" },
            new SubjectDefinition { Name = "数学", DefaultWeeklyCount = 6, Category = "主课" },
            new SubjectDefinition { Name = "英语", DefaultWeeklyCount = 5, Category = "主课" }
        });
        Assert.False(sim.IsCustom);
        Assert.Equal(3, sim.Subjects.Count);

        // 2. 用户修改配置（改名称、改课时、添加科目）
        sim.Subjects[0].Name = "语文改";
        sim.Subjects[1].DefaultWeeklyCount = 99;
        sim.Subjects.Add(new SubjectDefinition { Name = "物理", DefaultWeeklyCount = 3 });
        Assert.True(sim.IsCustom);
        Assert.Equal("初中标准", sim.SelectedTemplate); // 保持不变
        Assert.Equal(4, sim.Subjects.Count);

        // 3. 用户重新点击"初中标准" → 内容应完全恢复为模板原始值
        sim.LoadTemplate("初中标准", new[]
        {
            new SubjectDefinition { Name = "语文", DefaultWeeklyCount = 7, Category = "主课" },
            new SubjectDefinition { Name = "数学", DefaultWeeklyCount = 6, Category = "主课" },
            new SubjectDefinition { Name = "英语", DefaultWeeklyCount = 5, Category = "主课" }
        });

        // 4. 验证状态恢复
        Assert.False(sim.IsCustom);
        Assert.Equal("初中标准", sim.SelectedTemplate);

        // 5. 验证内容完全恢复（不是修改后的值）
        Assert.Equal(3, sim.Subjects.Count);
        Assert.Equal("语文", sim.Subjects[0].Name);       // 不是"语文改"
        Assert.Equal(7, sim.Subjects[0].DefaultWeeklyCount);
        Assert.Equal("数学", sim.Subjects[1].Name);
        Assert.Equal(6, sim.Subjects[1].DefaultWeeklyCount); // 不是99
        Assert.Equal("英语", sim.Subjects[2].Name);
        // 物理已被清除
        Assert.DoesNotContain(sim.Subjects, s => s.Name == "物理");
    }

    [Fact]
    public void ModifyConfig_ReselectTemplate_ThenModifyAgain_TriggersCustom()
    {
        // 改回模板后再次修改，应再次触发自定义
        var sim = new CustomStateSimulator();

        sim.LoadTemplate("初中标准", new[]
        {
            new SubjectDefinition { Name = "语文", DefaultWeeklyCount = 7 }
        });

        // 修改 → 自定义
        sim.Subjects[0].DefaultWeeklyCount = 3;
        Assert.True(sim.IsCustom);

        // 重新选择模板 → 恢复
        sim.LoadTemplate("初中标准", new[]
        {
            new SubjectDefinition { Name = "语文", DefaultWeeklyCount = 7 }
        });
        Assert.False(sim.IsCustom);

        // 再次修改 → 应再次触发自定义
        sim.Subjects[0].Name = "数学";
        Assert.True(sim.IsCustom);
        Assert.Equal("初中标准", sim.SelectedTemplate); // 保持不变
    }

    [Fact]
    public void DropDownClosed_WhenCustom_ReloadsTemplate()
    {
        // 模拟用户修改后点击下拉再关闭（重选同一模板）
        var sim = new CustomStateSimulator();
        sim.LoadTemplate("初中标准", new[] { new SubjectDefinition { Name = "语文", DefaultWeeklyCount = 7 } });

        // 修改 → 自定义
        sim.Subjects[0].DefaultWeeklyCount = 3;
        Assert.True(sim.IsCustom);

        // 模拟下拉关闭（用户点击了初中标准，但值相同不触发SelectionChanged）
        sim.DropDownClosed();
        Assert.False(sim.IsCustom); // 已恢复
        Assert.Equal("初中标准", sim.SelectedTemplate);
    }

    [Fact]
    public void DropDownClosed_WhenNotCustom_DoesNothing()
    {
        var sim = new CustomStateSimulator();
        sim.LoadTemplate("初中标准", new[] { new SubjectDefinition { Name = "语文" } });
        Assert.False(sim.IsCustom);

        // 非自定义状态下拉关闭 → 无变化
        sim.DropDownClosed();
        Assert.False(sim.IsCustom);
        Assert.Equal("初中标准", sim.SelectedTemplate);
    }
}
