using CommunityToolkit.Mvvm.ComponentModel;
using EasiLoginer.Application.Abstractions;
using EasiLoginer.Domain.Configuration;

namespace EasiLoginer.Presentation.Onboarding;

/// <summary>
/// 首次向导的视图模型：步骤推进、跳过与完成落盘。
/// </summary>
/// <remarks>
/// 完成或跳过都会写 <c>Internal.IsOobeCompleted = true</c>（文档 §10.7）。
/// 不依赖 WPF，便于单测。
/// </remarks>
public sealed partial class WizardViewModel : ObservableObject
{
    private readonly IConfigService _config;

    [ObservableProperty]
    private int _currentIndex;

    [ObservableProperty]
    private int _themeIndex;

    [ObservableProperty]
    private int _methodIndex;

    [ObservableProperty]
    private int _finishPageIndex;

    /// <summary>创建视图模型。</summary>
    public WizardViewModel(IConfigService config, IReadOnlyList<WizardStep>? steps = null)
    {
        ArgumentNullException.ThrowIfNull(config);

        _config = config;
        Steps = steps ?? OobeSteps.Create();

        var current = config.Current;
        _themeIndex = current.App.Theme switch
        {
            ThemeOptions.Light => 1,
            ThemeOptions.Dark => 2,
            _ => 0,
        };
        _methodIndex = (int)current.Login.Method;
        // 0 = 配置，1 = 档案；档案页已是应用默认页，这里默认选它
        _finishPageIndex = 1;
    }

    /// <summary>全部步骤。</summary>
    public IReadOnlyList<WizardStep> Steps { get; }

    /// <summary>当前步骤。</summary>
    public WizardStep CurrentStep => Steps[Math.Clamp(CurrentIndex, 0, Steps.Count - 1)];

    /// <summary>是否第一步。</summary>
    public bool IsFirst => CurrentIndex <= 0;

    /// <summary>是否最后一步。</summary>
    public bool IsLast => CurrentIndex >= Steps.Count - 1;

    /// <summary>进度文案。</summary>
    public string ProgressText => $"第 {CurrentIndex + 1} / {Steps.Count} 步";

    /// <summary>是否已经完成（完成或跳过）。</summary>
    public bool Completed { get; private set; }

    /// <summary>下一步；在最后一步时完成向导。</summary>
    public void Next()
    {
        if (IsLast)
        {
            Complete();
            return;
        }

        CurrentIndex++;
    }

    /// <summary>上一步（不会越过第一步）。</summary>
    public void Previous()
    {
        if (CurrentIndex > 0)
        {
            CurrentIndex--;
        }
    }

    /// <summary>跳过整个向导：同样标记为已完成并保存当前选择。</summary>
    public void SkipWizard() => Complete();

    /// <summary>完成向导：写回配置并落盘。</summary>
    public void Complete()
    {
        if (Completed)
        {
            return;
        }

        var current = _config.Current;
        current.App.Theme = ThemeIndex switch
        {
            1 => ThemeOptions.Light,
            2 => ThemeOptions.Dark,
            _ => ThemeOptions.Auto,
        };
        current.Login.Method = MethodIndex switch
        {
            1 => LoginMethod.Cv,
            2 => LoginMethod.Uia,
            3 => LoginMethod.Token,
            _ => LoginMethod.Fixed,
        };
        current.Internal.IsOobeCompleted = true;

        _config.Save();
        Completed = true;
    }

    partial void OnCurrentIndexChanged(int value)
    {
        OnPropertyChanged(nameof(CurrentStep));
        OnPropertyChanged(nameof(IsFirst));
        OnPropertyChanged(nameof(IsLast));
        OnPropertyChanged(nameof(ProgressText));
    }
}
