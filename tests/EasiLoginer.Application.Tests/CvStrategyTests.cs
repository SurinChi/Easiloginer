using System.Drawing;
using EasiLoginer.Application.Automation;
using EasiLoginer.Automation.Cv;
using EasiLoginer.Automation.Strategies;
using EasiLoginer.Domain.Automation;
using EasiLoginer.Domain.Configuration;
using EasiLoginer.Domain.Geometry;
using FluentAssertions;

namespace EasiLoginer.Application.Tests;

/// <summary>图像识别策略：变体选择、模板匹配与流程顺序（对应上游 cv.py）。</summary>
public sealed class CvStrategyTests
{
    /// <summary>两套受支持的显示环境。</summary>
    [Theory]
    [InlineData(1920, 1080, 1.0, "")]
    [InlineData(3840, 2160, 2.0, "_4k")]
    public void 变体选择(int width, int height, double scale, string expected)
    {
        ImageTemplateVariant.Resolve(new PixelPoint(width, height), scale).Should().Be(expected);
    }

    /// <summary>其他分辨率/缩放不受支持。</summary>
    [Theory]
    [InlineData(2560, 1440, 1.0)]
    [InlineData(1920, 1080, 1.25)]
    [InlineData(1536, 960, 2.0)]
    public void 不支持的环境(int width, int height, double scale)
    {
        ImageTemplateVariant.Resolve(new PixelPoint(width, height), scale).Should().BeNull();
    }

    /// <summary>不支持的环境在准备阶段直接失败，且不可重试。</summary>
    [Fact]
    public async Task 不支持的环境启动前失败()
    {
        var strategy = CreateStrategy(
            new FakeLocator(),
            new RecordingInputSimulator(),
            new FakeSession(),
            new FixedDisplayMetrics(new ScreenMetrics(new PixelPoint(1536, 960), 2.0, 60)));

        var action = () => strategy.BeforePrepareAsync(TestContexts.Create(TestConfigs.Create()), CancellationToken.None);

        var exception = await action.Should().ThrowAsync<LoginException>();
        exception.Which.CanRetry.Should().BeFalse();
        exception.Which.Message.Should().Contain("不支持图像识别");
        exception.Which.Message.Should().Contain("1920x1080");
    }

    /// <summary>PC（非 IWB）：切账号页 → 账号 → 密码 → 协议 → 回车。</summary>
    [Fact]
    public async Task PC流程()
    {
        var locator = new FakeLocator
        {
            ["account_login_button_direct"] = new PixelPoint(100, 200),
            ["agreement_checkbox_direct"] = new PixelPoint(50, 60),
        };
        var input = new RecordingInputSimulator();
        var strategy = CreateStrategy(locator, input, new FakeSession { IsIwb = false });
        var context = TestContexts.Create(TestConfigs.Create());
        context.Config.Experimental.PrivacyMask.Enabled = false;

        await strategy.BeforePrepareAsync(context, CancellationToken.None);
        await strategy.LoginAsync(context, CancellationToken.None);

        input.Clicks.Should().ContainInOrder(
            (100, 200),
            (100, 270),
            (100, 334),
            (50, 60));
        input.Typed.Should().ContainInOrder(("15777420786", false), ("secret", true));
        input.EnterCalls.Should().Be(1);
    }

    /// <summary>IWB：先点进入登录界面，再找账号按钮。</summary>
    [Fact]
    public async Task IWB流程()
    {
        var locator = new FakeLocator
        {
            ["account_login_button"] = new PixelPoint(300, 400),
            ["agreement_checkbox"] = new PixelPoint(10, 20),
        };
        var input = new RecordingInputSimulator();
        var strategy = CreateStrategy(locator, input, new FakeSession { IsIwb = true });
        var context = TestContexts.Create(TestConfigs.Create());
        context.Config.Experimental.PrivacyMask.Enabled = false;

        await strategy.BeforePrepareAsync(context, CancellationToken.None);
        await strategy.LoginAsync(context, CancellationToken.None);

        input.Clicks[0].Should().Be((172, 1044));
        input.Clicks.Should().Contain((300, 400));
        input.EnterCalls.Should().Be(1);
    }

    /// <summary>普通样式找不到时退回“已选中”样式（上游此处是无效重试，这里做了修正）。</summary>
    [Fact]
    public async Task 回退到已选中样式()
    {
        var locator = new FakeLocator
        {
            ["account_login_button_selected_direct"] = new PixelPoint(7, 8),
            ["agreement_checkbox_direct"] = new PixelPoint(1, 2),
        };
        var input = new RecordingInputSimulator();
        var strategy = CreateStrategy(locator, input, new FakeSession { IsIwb = false });
        var context = TestContexts.Create(TestConfigs.Create());
        context.Config.Experimental.PrivacyMask.Enabled = false;

        await strategy.BeforePrepareAsync(context, CancellationToken.None);
        await strategy.LoginAsync(context, CancellationToken.None);

        input.Clicks.Should().Contain((7, 8));
    }

    /// <summary>找不到账号按钮 → 可重试失败。</summary>
    [Fact]
    public async Task 找不到账号按钮()
    {
        var strategy = CreateStrategy(new FakeLocator(), new RecordingInputSimulator(), new FakeSession());
        var context = TestContexts.Create(TestConfigs.Create());
        context.Config.Experimental.PrivacyMask.Enabled = false;

        await strategy.BeforePrepareAsync(context, CancellationToken.None);

        var action = () => strategy.LoginAsync(context, CancellationToken.None);
        var exception = await action.Should().ThrowAsync<LoginException>();
        exception.Which.CanRetry.Should().BeTrue();
    }

    /// <summary>模板匹配：在大图里找到小图并返回中心点。</summary>
    [Fact]
    public void 模板匹配返回中心()
    {
        using var screen = new Bitmap(64, 48);
        using (var graphics = Graphics.FromImage(screen))
        {
            graphics.Clear(Color.Black);
            graphics.FillRectangle(Brushes.White, 20, 10, 8, 6);
        }

        using var template = new Bitmap(8, 6);
        using (var graphics = Graphics.FromImage(template))
        {
            graphics.Clear(Color.White);
        }

        var found = TemplateMatcher.Find(screen, template);

        found.Should().Be(new PixelPoint(24, 13));
    }

    /// <summary>找不到时返回 null，模板比屏幕大也返回 null。</summary>
    [Fact]
    public void 模板不存在返回空()
    {
        using var screen = new Bitmap(32, 32);
        using (var graphics = Graphics.FromImage(screen))
        {
            graphics.Clear(Color.Black);
        }

        using var template = new Bitmap(8, 8);
        using (var graphics = Graphics.FromImage(template))
        {
            graphics.Clear(Color.White);
        }

        TemplateMatcher.Find(screen, template).Should().BeNull();

        using var huge = new Bitmap(64, 64);
        TemplateMatcher.Find(screen, huge).Should().BeNull();
    }

    private static CvLoginStrategy CreateStrategy(
        ITemplateLocator locator,
        RecordingInputSimulator input,
        FakeSession session,
        IDisplayMetrics? display = null)
        => new(
            input,
            locator,
            session,
            display ?? new FixedDisplayMetrics(new ScreenMetrics(new PixelPoint(1920, 1080), 1.0, 60)),
            (_, _) => Task.CompletedTask);

    private sealed class FakeLocator : ITemplateLocator
    {
        private readonly Dictionary<string, PixelPoint> _points = new(StringComparer.Ordinal);

        public PixelPoint this[string name]
        {
            set => _points[name] = value;
        }

        public PixelPoint? Locate(string templateName)
            => _points.TryGetValue(templateName, out var point) ? point : null;
    }
}
