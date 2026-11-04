using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using EasiLoginer.Application.Abstractions;
using EasiLoginer.Application.Automation;
using EasiLoginer.Application.Profiles;
using EasiLoginer.Domain.Automation;
using EasiLoginer.Domain.Profiles;
using EasiLoginer.Presentation.Login;

namespace EasiLoginer.Presentation.Pages;

/// <summary>快捷登录页：为每个档案生成一个按钮，点击即运行该档案的登录流程。</summary>
public sealed partial class QuickLoginPageViewModel : ObservableObject
{
    private readonly IProfileRepository _profiles;
    private readonly ILoginRunner _login;
    private readonly IConfigService _config;
    private readonly IDisplayMetrics _display;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    /// <summary>创建视图模型并载入档案。</summary>
    public QuickLoginPageViewModel(
        IProfileRepository profiles,
        ILoginRunner login,
        IConfigService config,
        IDisplayMetrics display)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        ArgumentNullException.ThrowIfNull(login);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(display);

        _profiles = profiles;
        _login = login;
        _config = config;
        _display = display;

        Refresh();
    }

    /// <summary>按钮对应的档案列表。</summary>
    public ObservableCollection<AutomationProfile> Items { get; } = [];

    /// <summary>没有档案时显示引导文案。</summary>
    public bool IsEmpty => Items.Count == 0;

    /// <summary>从仓储重新读取档案（切回本页时调用，保证与档案页一致）。</summary>
    public void Refresh()
    {
        Items.Clear();
        foreach (var automation in _profiles.Current.Automations)
        {
            Items.Add(automation);
        }

        OnPropertyChanged(nameof(IsEmpty));
    }

    /// <summary>运行指定档案的登录流程。</summary>
    public async Task RunAsync(AutomationProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (IsBusy)
        {
            return;
        }

        if (_profiles.Current.EncryptionEnabled && !_profiles.IsUnlocked)
        {
            StatusText = "档案已加密且尚未解锁，请先到「档案」页解锁。";
            return;
        }

        if (string.IsNullOrWhiteSpace(profile.Account))
        {
            StatusText = $"「{profile.Name}」没有填写账号，无法登录。";
            return;
        }

        if (string.IsNullOrWhiteSpace(profile.Password))
        {
            StatusText = $"「{profile.Name}」没有填写密码，无法登录。请到「档案」页补全。";
            return;
        }

        IsBusy = true;
        StatusText = $"正在为「{profile.Name}」运行登录……";

        try
        {
            var outcome = await ProfileLoginRunner
                .RunAsync(profile, _login, _config, _display, CancellationToken.None)
                .ConfigureAwait(true);

            StatusText = outcome switch
            {
                LoginOutcome.Success => $"「{profile.Name}」登录成功。",
                LoginOutcome.Cancelled => $"「{profile.Name}」的登录被取消或已跳过。",
                _ => $"「{profile.Name}」登录失败，详见日志。",
            };
        }
        catch (Exception exception) when (exception is InvalidOperationException or ProfileLoadException)
        {
            StatusText = $"登录失败：{exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
