namespace Automatic_class_schedule.Models;

/// <summary>年级个性化排课配置。默认继承全局设置，可选单独配置。</summary>
public sealed class GradeScheduleConfig : Infrastructure.ObservableObject
{
    private string _gradeName = string.Empty;
    private bool _useCustomSettings;
    private int _daysPerWeek = 5;
    private int _morningPeriods = 4;
    private int _afternoonPeriods = 3;
    private bool _includeEveningSelfStudy;
    private int _eveningPeriods = 2;
    private bool[] _eveningStudyDays = { true, true, true, true, true, false, false };

    public string GradeName
    {
        get => _gradeName;
        set => SetProperty(ref _gradeName, value);
    }

    /// <summary>是否使用个性化配置（false = 继承全局）</summary>
    public bool UseCustomSettings
    {
        get => _useCustomSettings;
        set => SetProperty(ref _useCustomSettings, value);
    }

    public int DaysPerWeek
    {
        get => _daysPerWeek;
        set => SetProperty(ref _daysPerWeek, value);
    }

    public int MorningPeriods
    {
        get => _morningPeriods;
        set => SetProperty(ref _morningPeriods, value);
    }

    public int AfternoonPeriods
    {
        get => _afternoonPeriods;
        set => SetProperty(ref _afternoonPeriods, value);
    }

    public bool IncludeEveningSelfStudy
    {
        get => _includeEveningSelfStudy;
        set => SetProperty(ref _includeEveningSelfStudy, value);
    }

    public int EveningPeriods
    {
        get => _eveningPeriods;
        set => SetProperty(ref _eveningPeriods, value);
    }

    /// <summary>晚自习天配置（周一到周日，7个bool）</summary>
    public bool[] EveningStudyDays
    {
        get => _eveningStudyDays;
        set => SetProperty(ref _eveningStudyDays, value);
    }

    /// <summary>从全局设置复制值到本年级配置</summary>
    public void CopyFromGlobal(ScheduleSettings global)
    {
        DaysPerWeek = global.DaysPerWeek;
        MorningPeriods = global.MorningPeriods;
        AfternoonPeriods = global.AfternoonPeriods;
        IncludeEveningSelfStudy = global.IncludeEveningSelfStudy;
        EveningPeriods = global.EveningPeriods;
        EveningStudyDays = (bool[])global.EveningStudyDays.Clone();
    }
}
