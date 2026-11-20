using EasiLoginer.Application.Abstractions;
using EasiLoginer.Domain.Configuration;
using EasiLoginer.Presentation.Configuration;
using EasiLoginer.Presentation.Legal;
using EasiLoginer.Presentation.Onboarding;
using FluentAssertions;

namespace EasiLoginer.UiTests;

/// <summary>首次向导的状态机（文档 §10.7；6 步，首步为用户协议）。</summary>
public sealed class WizardTests
{
    /// <summary>六个步骤、顺序与首尾约束。</summary>
    [Fact]
    public void 步骤定义()
    {
        var steps = OobeSteps.Create();

        steps.Should().HaveCount(6);
        steps[0].Kind.Should().Be(WizardStepKind.Agreement, "第一页是用户协议");
        steps[0].Skippable.Should().BeFalse("协议页不允许单步跳过");
        steps[^1].Kind.Should().Be(WizardStepKind.Finish);
        steps.Select(step => step.Kind).Should().Contain([
            WizardStepKind.Agreement,
            WizardStepKind.Theme,
            WizardStepKind.LoginMethod,
            WizardStepKind.EasiNote,
            WizardStepKind.Shortcuts,
        ]);
    }

    /// <summary>
    /// 设置页、首次向导等入口必须使用同一组登录方式名称；
    /// 自动定位不能再写成 UI Automation，否则用户会对不上号。
    /// </summary>
    [Fact]
    public void 四种登录方式名称统一()
    {
        LoginMethodLabels.All.Should().Equal("固定位置", "图像识别", "自动定位", "令牌投递");
        LoginMethodLabels.All[(int)LoginMethod.Uia].Should().Be("自动定位");

        var loginStep = OobeSteps.Create().Single(step => step.Kind == WizardStepKind.LoginMethod);
        loginStep.Description.Should().Contain("自动定位");
        loginStep.Description.Should().NotContain("UI Automation");
    }

    /// <summary>协议页必须有完整正文，且默认未勾选。</summary>
    [Fact]
    public void 协议默认未同意()
    {
        OobeSteps.AgreementText.Should().Contain("GPLv3");
        var viewModel = CreateViewModel(out _, out _);
        viewModel.AgreementAccepted.Should().BeFalse();
    }

    /// <summary>
    /// 首启向导展示的是完整协议，而不是摘要：正文必须包含数据、免责、
    /// 修补风险、协议变更等核心章节，且长度足以要求用户滚动阅读。
    /// </summary>
    [Fact]
    public void 协议是完整版()
    {
        var text = OobeSteps.AgreementText;

        text.Should().StartWith("EasiLoginer 用户协议（完整版）");
        text.Should().Contain("数据与隐私");
        text.Should().Contain("风险与免责声明");
        text.Should().Contain("责任限制");
        text.Should().Contain("协议变更");
        text.Should().Contain("知识产权");
        text.Should().Contain("适用法律");
        text.Should().Contain("Argon2id", "协议需要说明主密码的加密方式");
        text.Should().Contain("希沃白板的正常使用", "修补风险必须明确告知");
        text.Length.Should().BeGreaterThan(4000, "完整协议应远长于摘要");
    }

    /// <summary>协议版本与生效日期集中在一处维护。</summary>
    [Fact]
    public void 协议版本集中维护()
    {
        UserAgreement.Version.Should().Be("1.1");
        UserAgreement.EffectiveDate.Should().Be("2026-11-19");
        OobeSteps.AgreementText.Should().Be(UserAgreement.Text);
    }

    /// <summary>进度文案与首尾标记随索引变化。</summary>
    [Fact]
    public void 进度与边界()
    {
        var viewModel = CreateViewModel(out _, out _);

        viewModel.ProgressText.Should().Be("第 1 / 6 步");
        viewModel.IsFirst.Should().BeTrue();
        viewModel.IsLast.Should().BeFalse();

        viewModel.Previous();
        viewModel.CurrentIndex.Should().Be(0, "第一步不能再往前");

        for (var index = 0; index < 5; index++)
        {
            viewModel.Next();
        }

        viewModel.CurrentIndex.Should().Be(5);
        viewModel.IsLast.Should().BeTrue();
        viewModel.ProgressText.Should().Be("第 6 / 6 步");
    }

    /// <summary>完成时把选择写回配置并落盘一次。</summary>
    [Fact]
    public void 完成写回配置()
    {
        var viewModel = CreateViewModel(out var config, out var saves);
        viewModel.ThemeIndex = 2;
        viewModel.MethodIndex = 3;

        viewModel.Complete();

        config.App.Theme.Should().Be(ThemeOptions.Dark);
        config.Login.Method.Should().Be(LoginMethod.Token);
        config.Internal.IsOobeCompleted.Should().BeTrue();
        saves.Count.Should().Be(1);
        viewModel.Completed.Should().BeTrue();
    }

    /// <summary>跳过向导同样标记完成并保存。</summary>
    [Fact]
    public void 跳过也标记完成()
    {
        var viewModel = CreateViewModel(out var config, out var saves);

        viewModel.SkipWizard();

        config.Internal.IsOobeCompleted.Should().BeTrue();
        saves.Count.Should().Be(1);
        viewModel.Completed.Should().BeTrue();
    }

    /// <summary>重复完成不会重复保存。</summary>
    [Fact]
    public void 完成幂等()
    {
        var viewModel = CreateViewModel(out _, out var saves);

        viewModel.Complete();
        viewModel.Complete();

        saves.Count.Should().Be(1);
    }

    /// <summary>最后一步点「下一步」等价于完成。</summary>
    [Fact]
    public void 最后一步下一步即完成()
    {
        var viewModel = CreateViewModel(out var config, out _);

        for (var index = 0; index < 6; index++)
        {
            viewModel.Next();
        }

        viewModel.Completed.Should().BeTrue();
        config.Internal.IsOobeCompleted.Should().BeTrue();
    }

    private static WizardViewModel CreateViewModel(out Config config, out SaveCounter saveCounter)
    {
        config = new Config();
        var service = new FakeConfigService(config);
        saveCounter = service.Counter;
        return new WizardViewModel(service);
    }

    private sealed class SaveCounter
    {
        public int Count { get; private set; }

        public void Increment() => Count++;
    }

    private sealed class FakeConfigService(Config config) : IConfigService
    {
        public Config Current => config;

        public SaveCounter Counter { get; } = new();

        public void Save() => Counter.Increment();
    }
}
