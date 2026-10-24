using EasiLoginer.Application.Automation;
using EasiLoginer.Automation.Strategies;
using EasiLoginer.Automation.Uia;
using EasiLoginer.Domain.Automation;
using EasiLoginer.Domain.Configuration;
using FluentAssertions;

namespace EasiLoginer.Application.Tests;

/// <summary>UIA 策略的流程与失败分类（文档 §9.2）。</summary>
public sealed class UiaLoginStrategyTests
{
    /// <summary>PC 界面：切账号页 → 账号 → 密码 → 协议 → 登录。</summary>
    [Fact]
    public async Task PC流程()
    {
        var ui = new FakeUi(EasiNoteUiIds.AccountLoginRadio, EasiNoteUiIds.AccountInput, EasiNoteUiIds.PasswordBox,
            EasiNoteUiIds.AgreementCheckBox, EasiNoteUiIds.LoginButton);
        ui.ToggleState = false;
        var presenter = new RecordingPresenter();
        var context = CreateContext(presenter);

        await new UiaLoginStrategy(ui, InstantDelay).LoginAsync(context, CancellationToken.None);

        ui.Operations.Should().ContainInOrder(
            $"click:{EasiNoteUiIds.AccountLoginRadio}",
            $"set:{EasiNoteUiIds.AccountInput}=alice",
            $"set:{EasiNoteUiIds.PasswordBox}=secret",
            $"click:{EasiNoteUiIds.AgreementCheckBox}",
            $"click:{EasiNoteUiIds.LoginButton}");
    }

    /// <summary>协议已勾选时不再点击。</summary>
    [Fact]
    public async Task 已勾选协议不重复点击()
    {
        var ui = new FakeUi(EasiNoteUiIds.AccountLoginRadio, EasiNoteUiIds.AccountInput, EasiNoteUiIds.PasswordBox,
            EasiNoteUiIds.AgreementCheckBox, EasiNoteUiIds.LoginButton);
        ui.ToggleState = true;
        var context = CreateContext(new RecordingPresenter());

        await new UiaLoginStrategy(ui, InstantDelay).LoginAsync(context, CancellationToken.None);

        ui.Operations.Should().NotContain($"click:{EasiNoteUiIds.AgreementCheckBox}");
    }

    /// <summary>IWB 界面：先点头像、等 IWBLogin，再用 AccountRadioButton。</summary>
    [Fact]
    public async Task IWB流程()
    {
        var ui = new FakeUi(EasiNoteUiIds.IwbProfileButton, EasiNoteUiIds.IwbLoginWindow, EasiNoteUiIds.IwbAccountRadio,
            EasiNoteUiIds.AccountInput, EasiNoteUiIds.PasswordBox, EasiNoteUiIds.AgreementCheckBox, EasiNoteUiIds.LoginButton);
        ui.ToggleState = true;
        var context = CreateContext(new RecordingPresenter());

        await new UiaLoginStrategy(ui, InstantDelay).LoginAsync(context, CancellationToken.None);

        ui.Operations.Should().ContainInOrder(
            $"click:{EasiNoteUiIds.IwbProfileButton}",
            $"click:{EasiNoteUiIds.IwbAccountRadio}",
            $"click:{EasiNoteUiIds.LoginButton}");
    }

    /// <summary>找不到登录按钮 → 可重试失败。</summary>
    [Fact]
    public async Task 找不到登录按钮可重试()
    {
        var ui = new FakeUi(EasiNoteUiIds.AccountLoginRadio, EasiNoteUiIds.AccountInput, EasiNoteUiIds.PasswordBox);
        var context = CreateContext(new RecordingPresenter());

        var action = () => new UiaLoginStrategy(ui, InstantDelay).LoginAsync(context, CancellationToken.None);

        var exception = await action.Should().ThrowAsync<LoginException>();
        exception.Which.CanRetry.Should().BeTrue();
        exception.Which.Message.Should().Contain("LoginButton");
    }

    /// <summary>完全没有登录界面 → 可重试失败。</summary>
    [Fact]
    public async Task 没有登录界面()
    {
        var ui = new FakeUi();
        var context = CreateContext(new RecordingPresenter());

        var action = () => new UiaLoginStrategy(ui, InstantDelay).LoginAsync(context, CancellationToken.None);

        var exception = await action.Should().ThrowAsync<LoginException>();
        exception.Which.CanRetry.Should().BeTrue();
    }

    /// <summary>开启隐私遮罩时取二维码矩形。</summary>
    [Fact]
    public async Task 遮罩使用二维码矩形()
    {
        var ui = new FakeUi(EasiNoteUiIds.AccountLoginRadio, EasiNoteUiIds.AccountInput, EasiNoteUiIds.PasswordBox,
            EasiNoteUiIds.AgreementCheckBox, EasiNoteUiIds.LoginButton, EasiNoteUiIds.QrCodeControl);
        ui.ToggleState = true;
        ui.Rects[EasiNoteUiIds.QrCodeControl] = new UiaRect(100, 200, 300, 400);
        var presenter = new RecordingPresenter();
        var config = new Config();
        config.Experimental.PrivacyMask.Enabled = true;
        var context = new LoginContext("alice", "secret", config, presenter);

        await new UiaLoginStrategy(ui, InstantDelay).LoginAsync(context, CancellationToken.None);

        presenter.Masks.Should().ContainSingle()
            .Which.Should().Be((100, 200, 300, 400));
    }

    private static Task InstantDelay(TimeSpan duration, CancellationToken cancellationToken) => Task.CompletedTask;

    private static LoginContext CreateContext(ILoginPresenter presenter)
    {
        var config = new Config();
        config.Experimental.PrivacyMask.Enabled = false;
        return new LoginContext("alice", "secret", config, presenter);
    }

    private sealed class FakeUi(params string[] present) : IEasiNoteUi
    {
        private readonly HashSet<string> _present = new(present, StringComparer.Ordinal);

        public List<string> Operations { get; } = [];

        public Dictionary<string, UiaRect> Rects { get; } = new(StringComparer.Ordinal);

        public bool? ToggleState { get; set; }

        public Task<bool> WaitForElementAsync(string automationId, TimeSpan timeout, CancellationToken cancellationToken)
            => Task.FromResult(_present.Contains(automationId));

        public Task<bool> ClickAsync(string automationId, CancellationToken cancellationToken)
        {
            if (!_present.Contains(automationId))
            {
                return Task.FromResult(false);
            }

            Operations.Add($"click:{automationId}");
            return Task.FromResult(true);
        }

        public Task<bool> SetTextAsync(string automationId, string text, CancellationToken cancellationToken)
        {
            if (!_present.Contains(automationId))
            {
                return Task.FromResult(false);
            }

            Operations.Add($"set:{automationId}={text}");
            return Task.FromResult(true);
        }

        public Task<bool?> IsToggledAsync(string automationId, CancellationToken cancellationToken)
            => Task.FromResult(_present.Contains(automationId) ? ToggleState : null);

        public Task<UiaRect?> GetRectAsync(string automationId, CancellationToken cancellationToken)
            => Task.FromResult(Rects.TryGetValue(automationId, out var rect) ? rect : (UiaRect?)null);
    }
}
