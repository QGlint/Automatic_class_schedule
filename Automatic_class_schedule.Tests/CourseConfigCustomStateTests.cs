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
            SelectedTemplate = string.Empty; // 取消选中
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
        Assert.Equal(string.Empty, sim.SelectedTemplate); // 选中已清除
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
        Assert.Equal(string.Empty, sim.SelectedTemplate);
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
        Assert.Equal(string.Empty, sim.SelectedTemplate);

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

        // 关键：SelectedTemplate 已清空，可以重新选择
        Assert.Equal(string.Empty, sim.SelectedTemplate);

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
}
