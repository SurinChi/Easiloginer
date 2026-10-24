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
/// 档案页视图模型的保存链路回归测试。
/// </summary>
/// <remarks>
/// 曾经的真实缺陷：<c>Add()</c> 只把新档案加进界面集合、没加进仓储的 <c>Current</c>，
/// 于是「保存」时序列化的是旧列表——界面上有、落盘后消失，表现为"保存失败"。
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

    private static ProfilePageViewModel CreateViewModel(FakeProfileRepository repository)
        => new(repository, new NoopLoginRunner(), new FakeConfigService(), new FakeDisplayMetrics());

    private sealed class FakeProfileRepository : IProfileRepository
    {
        public Profile Current { get; } = new();

        public bool IsUnlocked => true;

        public int SaveCalls { get; private set; }

        public event EventHandler<ProfileChangedEventArgs>? Changed;

        public Profile Load() => Current;

        public void Save(ProfileChangeReason reason = ProfileChangeReason.ProfileChanged) => SaveCalls++;

        public bool Unlock(string masterPassword) => true;

        public bool TryCachedUnlock() => true;

        public void EnableEncryption(string masterPassword)
        {
        }

        public bool DisableEncryption(string masterPassword) => true;

        public void Lock()
        {
        }

        public void Reload()
        {
        }

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
