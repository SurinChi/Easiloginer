using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using EasiLoginer.Application.Abstractions;
using EasiLoginer.Application.Automation;
using EasiLoginer.Application.Profiles;
using EasiLoginer.Domain.Automation;
using EasiLoginer.Domain.Profiles;
using EasiLoginer.Presentation.Login;
using EasiLoginer.Presentation.Profiles;

namespace EasiLoginer.Presentation.Pages;

/// <summary>档案页视图模型。</summary>
public sealed partial class ProfilePageViewModel : ObservableObject
{
    private readonly IProfileRepository _profiles;
    private readonly ILoginRunner _login;
    private readonly IConfigService _config;
    private readonly IDisplayMetrics _display;

    /// <summary>账号/密码基线，用于判断保存时是否需要验证主密码。</summary>
    private List<SensitiveSnapshot> _sensitiveSnapshot = [];

    [ObservableProperty]
    private AutomationProfile? _selected;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private string _encryptionStatus = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    /// <summary>创建视图模型并载入档案列表。</summary>
    public ProfilePageViewModel(
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

        // 设过主密码时优先用本机缓存静默解锁：查看不算敏感操作，
        // 只有"修改/删除账号及其密码"才要求重新验证。
        if (profiles.Current.EncryptionEnabled && !profiles.IsUnlocked)
        {
            _ = profiles.TryCachedUnlock();
        }

        RefreshEncryptionState();
        CaptureSensitiveSnapshot();
    }

    /// <summary>档案集合。</summary>
    public ObservableCollection<AutomationProfile> Items { get; } = [];

    /// <summary>是否有选中项（控制编辑器可用性）。</summary>
    public bool HasSelection => Selected is not null;

    /// <summary>是否已启用主密码（只有改账号/密码与删除才需要验证）。</summary>
    public bool RequiresMasterPassword => _profiles.Current.EncryptionEnabled;

    /// <summary>校验主密码；成功则解锁。</summary>
    /// <remarks>
    /// 校验可能触发仓储从磁盘重建档案列表（锁定 → 解锁）。重建会换掉编辑器当前绑定的对象，
    /// 所以已经解锁时编辑中的实例要在校验后放回仓储，否则刚输入的改动会在点「保存」时消失。
    /// </remarks>
    public bool VerifyMasterPassword(string password)
    {
        var edited = _profiles.IsUnlocked ? Items.ToList() : null;

        if (!_profiles.Unlock(password))
        {
            return false;
        }

        if (edited is not null)
        {
            foreach (var profile in edited)
            {
                _profiles.UpsertAutomation(profile);
            }
        }

        RefreshEncryptionState();
        return true;
    }

    /// <summary>
    /// 当前编辑是否改动了账号或密码（新增档案带凭据也算）。
    /// </summary>
    public bool HasSensitiveChanges()
    {
        foreach (var profile in Items)
        {
            var before = _sensitiveSnapshot.FirstOrDefault(
                item => string.Equals(item.Id, profile.Id, StringComparison.Ordinal));

            if (before is null)
            {
                if (!string.IsNullOrEmpty(profile.Account) || !string.IsNullOrEmpty(profile.Password))
                {
                    return true;
                }

                continue;
            }

            if (!string.Equals(before.Account, profile.Account, StringComparison.Ordinal) ||
                !string.Equals(before.Password, profile.Password, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>记录当前的账号/密码快照，作为后续变更判断的基线。</summary>
    public void CaptureSensitiveSnapshot()
        => _sensitiveSnapshot = Items
            .Select(profile => new SensitiveSnapshot(profile.Id, profile.Account, profile.Password))
            .ToList();

    /// <summary>是否可以启用加密。</summary>
    public bool CanEnableEncryption => ProfileEncryptionState.CanEnable(_profiles.Current);

    /// <summary>是否可以解锁。</summary>
    public bool CanUnlock => ProfileEncryptionState.CanUnlock(_profiles.Current, _profiles.IsUnlocked);

    /// <summary>是否可以关闭加密。</summary>
    public bool CanDisableEncryption => ProfileEncryptionState.CanDisable(_profiles.Current, _profiles.IsUnlocked);

    partial void OnSelectedChanged(AutomationProfile? value) => OnPropertyChanged(nameof(HasSelection));

    /// <summary>一条档案的敏感字段快照。</summary>
    private sealed record SensitiveSnapshot(string Id, string Account, string Password);

    /// <summary>新增一条空白档案。</summary>
    public void Add()
    {
        var automation = new AutomationProfile { Name = "新档案" };

        // 关键：必须同时写进仓储的 Current，否则 Save() 序列化的是旧列表，
        // 新增的档案会在界面上存在、落盘时消失（等于"保存失败"）。
        _profiles.UpsertAutomation(automation);
        Items.Add(automation);
        Selected = automation;
        StatusText = "已添加，填写账号密码后保存。";
    }

    /// <summary>删除选中档案。</summary>
    public void Delete()
    {
        if (Selected is null)
        {
            return;
        }

        var target = Selected;
        Items.Remove(target);
        _profiles.DeleteAutomation(target.Id);

        try
        {
            _profiles.Save(ProfileChangeReason.AutomationDeleted);
            StatusText = "已删除。";
        }
        catch (ProfileLockedException)
        {
            StatusText = "档案已加密且未解锁，无法保存删除结果。";
        }
        catch (ProfileValidationException exception)
        {
            StatusText = exception.Message;
        }

        Selected = Items.FirstOrDefault();
    }

    /// <summary>保存档案，返回是否真的写盘成功。</summary>
    /// <remarks>
    /// 调用方必须检查返回值：保存失败时继续登录只会用磁盘上的旧数据，
    /// 用户却以为改动已经生效（旧实现正是这样误报「已保存」的）。
    /// </remarks>
    public bool Save()
    {
        // 编辑器绑定的是 Items 里的实例；新行可能还没进 Current，这里再兜一次。
        if (Selected is not null)
        {
            _profiles.UpsertAutomation(Selected);
        }

        try
        {
            _profiles.Save(ProfileChangeReason.AutomationSaved);
            StatusText = $"已保存（共 {_profiles.Current.Automations.Count} 条档案）。";
            return true;
        }
        catch (ProfileLockedException)
        {
            StatusText = "档案已加密且主密码未解锁，已拒绝保存以避免数据丢失。";
        }
        catch (ProfileValidationException exception)
        {
            StatusText = $"保存失败：{exception.Message}（账号必填，密码可以留空）";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            StatusText = $"保存失败：无法写入文件（{exception.Message}）";
        }

        return false;
    }

    /// <summary>启用主密码加密。</summary>
    public void EnableEncryption(string masterPassword)
    {
        try
        {
            _profiles.EnableEncryption(masterPassword);
            StatusText = "已启用主密码加密。";
        }
        catch (ProfileValidationException exception)
        {
            StatusText = $"启用失败：{exception.Message}";
        }
        catch (ProfileLockedException)
        {
            StatusText = "启用失败：档案处于锁定状态。";
        }

        RefreshEncryptionState();
        CaptureSensitiveSnapshot();
    }

    /// <summary>解锁当前会话。</summary>
    public void Unlock(string masterPassword)
    {
        StatusText = _profiles.Unlock(masterPassword) ? "已解锁。" : "主密码错误。";
        RefreshEncryptionState();

        // 解锁后密码从空串变成真实值；不重建基线会让「只改了名称」也被判定为敏感改动
        CaptureSensitiveSnapshot();
    }

    /// <summary>关闭加密（恢复明文保存）。</summary>
    public void DisableEncryption(string masterPassword)
    {
        StatusText = _profiles.DisableEncryption(masterPassword) ? "已关闭加密。" : "主密码错误。";
        RefreshEncryptionState();
        CaptureSensitiveSnapshot();
    }

    /// <summary>运行选中档案的登录（手动触发）。</summary>
    public async Task RunAsync()
    {
        if (Selected is null || IsBusy)
        {
            return;
        }

        // 密码允许留空保存，但登录前必须填写
        if (string.IsNullOrWhiteSpace(Selected.Password))
        {
            StatusText = "该档案未填写密码，无法执行登录。请补全密码后再运行。";
            return;
        }

        // 保存失败（例如另一条档案账号为空导致整份校验不通过）时不能继续：
        // 登录路径会重新从磁盘读取，用旧凭据登录，用户却以为是刚改的密码。
        if (!Save())
        {
            return;
        }

        IsBusy = true;
        StatusText = "正在登录……";

        try
        {
            var outcome = await ProfileLoginRunner
                .RunAsync(Selected, _login, _config, _display, CancellationToken.None)
                .ConfigureAwait(true);

            StatusText = outcome switch
            {
                LoginOutcome.Success => "登录成功。",
                LoginOutcome.Cancelled => "登录被取消或已跳过。",
                _ => "登录失败，详见日志。",
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

    private void RefreshEncryptionState()
    {
        EncryptionStatus = ProfileEncryptionState.Describe(_profiles.Current, _profiles.IsUnlocked);

        var selectedId = Selected?.Id;
        Items.Clear();

        foreach (var automation in _profiles.Current.Automations)
        {
            Items.Add(automation);
        }

        Selected = Items.FirstOrDefault(item => string.Equals(item.Id, selectedId, StringComparison.Ordinal))
                   ?? Items.FirstOrDefault();

        OnPropertyChanged(nameof(CanEnableEncryption));
        OnPropertyChanged(nameof(CanUnlock));
        OnPropertyChanged(nameof(CanDisableEncryption));
        OnPropertyChanged(nameof(HasSelection));
    }
}
