using EasiLoginer.Application.Abstractions;
using EasiLoginer.Application.Automation;
using EasiLoginer.Application.Profiles;
using EasiLoginer.Domain.Automation;
using EasiLoginer.Domain.Configuration;
using EasiLoginer.Domain.Geometry;
using EasiLoginer.Domain.Profiles;
using EasiLoginer.Presentation.Pages;
using FluentAssertions;

namespace EasiLoginer.UiTests;

/// <summary>
/// 档案页视图模型的保存链路与主密码门禁。
/// </summary>
/// <remarks>
/// 两个曾经的真实问题：
/// <list type="bullet">
///   <item><c>Add()</c> 只往界面集合加行、没进仓储，保存后数据消失；</item>
///   <item>设过主密码后页面用 DPAPI 缓存自动解锁，敏感操作无需验证。</item>
/// </list>
/// </remarks>
public sealed class ProfilePageViewModelTests
{
    /// <summary>新增后必须立刻进入仓储，否则后续 Save 会丢掉它。</summary>
    [Fact]
    public void 新增即写入仓储()
    {
        var repository = new FakeProfileRepository();
        var viewModel = CreateViewModel(repository);

        viewModel.Add();

        repository.Current.Automations.Should().ContainSingle("新增行必须先进入 Current 才可能被保存");
        viewModel.Items.Should().ContainSingle();
        viewModel.Selected.Should().NotBeNull();
    }

    /// <summary>填写后保存：仓储落盘一次，数据可读回。</summary>
    [Fact]
    public void 填写后保存会落盘()
    {
        var repository = new FakeProfileRepository();
        var viewModel = CreateViewModel(repository);

        viewModel.Add();
        viewModel.Selected!.Name = "测试档案";
        viewModel.Selected.Account = "13800000000";
        viewModel.Selected.Password = "pw";
        viewModel.Save();

        repository.SaveCalls.Should().Be(1);
        repository.Current.Automations.Should().ContainSingle()
            .Which.Account.Should().Be("13800000000");
        viewModel.StatusText.Should().Contain("已保存");
    }

    /// <summary>删除后仓储同步减少并落盘。</summary>
    [Fact]
    public void 删除会同步仓储()
    {
        var repository = new FakeProfileRepository();
        var viewModel = CreateViewModel(repository);
        viewModel.Add();
        viewModel.Selected!.Account = "a";
        viewModel.Selected.Password = "b";
        viewModel.Save();

        viewModel.Delete();

        repository.Current.Automations.Should().BeEmpty();
        repository.SaveCalls.Should().Be(2);
    }

    /// <summary>查看档案不算敏感操作：启用主密码后打开页面会尝试本机缓存解锁。</summary>
    [Fact]
    public void 加密后打开页面尝试缓存解锁()
    {
        var repository = new FakeProfileRepository { EncryptionEnabled = true, Unlocked = false };
        repository.CachedUnlockSucceeds = true;

        var viewModel = CreateViewModel(repository);

        repository.CachedUnlockCalls.Should().Be(1);
        viewModel.RequiresMasterPassword.Should().BeTrue();
    }

    /// <summary>密码错误时不放行；正确后本次操作解锁。</summary>
    [Fact]
    public void 验证主密码()
    {
        var repository = new FakeProfileRepository { EncryptionEnabled = true, Unlocked = false };
        var viewModel = CreateViewModel(repository);

        viewModel.VerifyMasterPassword("wrong").Should().BeFalse();
        viewModel.VerifyMasterPassword("pw").Should().BeTrue();
        repository.IsUnlocked.Should().BeTrue();
    }

    /// <summary>只有改动账号或密码才需要验证；改其它字段不算敏感变更。</summary>
    [Fact]
    public void 敏感变更判断()
    {
        var repository = new FakeProfileRepository { EncryptionEnabled = true, Unlocked = true };
        var viewModel = CreateViewModel(repository);
        viewModel.Add();
        viewModel.Selected!.Account = "13800000000";
        viewModel.Selected.Password = "pw";
        viewModel.CaptureSensitiveSnapshot();

        viewModel.Selected.Name = "改名不算敏感";
        viewModel.HasSensitiveChanges().Should().BeFalse();

        viewModel.Selected.Password = "pw2";
        viewModel.HasSensitiveChanges().Should().BeTrue();

        viewModel.CaptureSensitiveSnapshot();
        viewModel.HasSensitiveChanges().Should().BeFalse();
    }

    /// <summary>新增档案填了凭据也算敏感变更，需要验证。</summary>
    [Fact]
    public void 新增凭据算敏感变更()
    {
        var repository = new FakeProfileRepository { EncryptionEnabled = true, Unlocked = true };
        var viewModel = CreateViewModel(repository);
        viewModel.Add();
        viewModel.Selected!.Account = "13800000000";

        viewModel.HasSensitiveChanges().Should().BeTrue();
    }

    private static ProfilePageViewModel CreateViewModel(FakeProfileRepository repository)
        => new(repository, new NoopLoginRunner(), new FakeConfigService(), new FakeDisplayMetrics());

    private sealed class FakeProfileRepository : IProfileRepository
    {
        public bool EncryptionEnabled
        {
            get => Current.EncryptionEnabled;
            set => Current.EncryptionEnabled = value;
        }

        public bool Unlocked { get; set; }

        public Profile Current { get; } = new();

        public bool IsUnlocked => Unlocked;

        public int SaveCalls { get; private set; }

        public int LockCalls { get; private set; }

        public int ReloadCalls { get; private set; }

        public event EventHandler<ProfileChangedEventArgs>? Changed;

        public Profile Load() => Current;

        public void Save(ProfileChangeReason reason = ProfileChangeReason.ProfileChanged) => SaveCalls++;

        public bool Unlock(string masterPassword)
        {
            Unlocked = string.Equals(masterPassword, "pw", StringComparison.Ordinal);
            return Unlocked;
        }

        public bool CachedUnlockSucceeds { get; set; }

        public int CachedUnlockCalls { get; private set; }

        public bool TryCachedUnlock()
        {
            CachedUnlockCalls++;
            if (CachedUnlockSucceeds)
            {
                Unlocked = true;
            }

            return Unlocked;
        }

        public void EnableEncryption(string masterPassword)
        {
            Current.EncryptionEnabled = true;
            Unlocked = true;
        }

        public bool DisableEncryption(string masterPassword) => true;

        public void Lock()
        {
            Unlocked = false;
            LockCalls++;
        }

        public void Reload() => ReloadCalls++;

        public AutomationProfile? FindAutomation(string id)
            => Current.Automations.FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.Ordinal));

        public void UpsertAutomation(AutomationProfile automation)
        {
            var index = Current.Automations.FindIndex(item => string.Equals(item.Id, automation.Id, StringComparison.Ordinal));
            if (index >= 0)
            {
                Current.Automations[index] = automation;
            }
            else
            {
                Current.Automations.Add(automation);
            }
        }

        public bool DeleteAutomation(string id)
        {
            var index = Current.Automations.FindIndex(item => string.Equals(item.Id, id, StringComparison.Ordinal));
            if (index < 0)
            {
                return false;
            }

            Current.Automations.RemoveAt(index);
            return true;
        }

        public void RaiseChanged(ProfileChangeReason reason) => Changed?.Invoke(this, new ProfileChangedEventArgs(reason));
    }

    private sealed class NoopLoginRunner : ILoginRunner
    {
        public Task<LoginOutcome> RunForProfileAsync(string profileId, LoginRunOptions options, CancellationToken cancellationToken)
            => Task.FromResult(LoginOutcome.Cancelled);
    }

    private sealed class FakeConfigService : IConfigService
    {
        public Config Current { get; } = new();

        public void Save()
        {
        }
    }

    private sealed class FakeDisplayMetrics : IDisplayMetrics
    {
        public ScreenMetrics Capture() => new(new PixelPoint(1920, 1080), 1.0, 60);
    }
}
