using EasiLoginer.Domain.Configuration;
using EasiLoginer.Domain.Geometry;
using EasiLoginer.Presentation.Configuration;
using FluentAssertions;

namespace EasiLoginer.UiTests;

/// <summary>
/// 元数据驱动设置卡片（文档 §10.4）：目录生成与读写往返。
/// </summary>
public sealed class ConfigCatalogTests
{
    /// <summary>目录必须覆盖全部可见分组，且不含已移除的段。</summary>
    [Fact]
    public void 覆盖可见分组()
    {
        var groups = ConfigCatalog.Build().Select(descriptor => descriptor.Group).Distinct().ToArray();

        groups.Should().Contain(["Login", "Warning", "Banner", "StatusOverlay", "App", "Experimental", "Profile", "Update"]);
        groups.Should().NotContain("ClassIsland");
        groups.Should().NotContain("Debug");
        groups.Should().NotContain("Internal");
        groups.Should().NotContain("Statistics");
    }

    /// <summary>警示横幅的文本可以在配置页直接改（斜纹上滚动显示的内容）。</summary>
    [Fact]
    public void 横幅文本可编辑()
    {
        var descriptor = ConfigCatalog.Build().Single(item => item.Path == "Banner.Style.Text");

        descriptor.Kind.Should().Be(SettingKind.Text);
        descriptor.Title.Should().Be("文本");
        descriptor.Group.Should().Be("Banner");
    }

    /// <summary>已移除的遥测开关不再出现在目录里。</summary>
    [Fact]
    public void 遥测开关已隐藏()
    {
        ConfigCatalog.Build().Select(item => item.Path).Should().NotContain("App.TelemetryEnabled");
    }

    /// <summary>隐藏项不出现在界面目录中。</summary>
    [Theory]
    [InlineData("Login.Position.BaseSize")]
    [InlineData("Login.Position.LoginWindowSize")]
    [InlineData("Update.LastVersion")]
    public void 隐藏项被排除(string path)
    {
        ConfigCatalog.Build().Select(descriptor => descriptor.Path).Should().NotContain(path);
    }

    /// <summary>枚举项必须带显示文本。</summary>
    [Fact]
    public void 枚举项带有选项文本()
    {
        var enums = ConfigCatalog.Build().Where(descriptor => descriptor.Kind == SettingKind.Enum).ToArray();

        enums.Should().NotBeEmpty();
        enums.Should().OnlyContain(descriptor => descriptor.Options != null && descriptor.Options.Count > 0);
    }

    /// <summary>数值项的上下限与上游 pydantic 约束一致。</summary>
    [Theory]
    [InlineData("Warning.Timeout", 5, 600)]
    [InlineData("Warning.MaxDelays", 0, 3)]
    [InlineData("Banner.Style.Fps", 1, 120)]
    [InlineData("App.MaxRetries", 0, 5)]
    [InlineData("Login.Timeout.LaunchPollingInterval", 0.1, 60)]
    public void 数值范围与上游一致(string path, double min, double max)
    {
        var descriptor = ConfigCatalog.Build().Single(item => item.Path == path);

        descriptor.Kind.Should().Be(SettingKind.Number);
        descriptor.Min.Should().Be(min);
        descriptor.Max.Should().Be(max);
    }

    /// <summary>布尔项写回配置。</summary>
    [Fact]
    public void 布尔项写回()
    {
        var config = new Config();
        var item = Create(config, "Login.SkipIfLoggedIn");

        item.BoolValue = false;
        item.Apply();

        config.Login.SkipIfLoggedIn.Should().BeFalse();
    }

    /// <summary>枚举项按下标写回。</summary>
    [Fact]
    public void 枚举项写回()
    {
        var config = new Config();
        var item = Create(config, "Login.Method");

        item.SelectedIndex = (int)LoginMethod.Token;
        item.Apply();

        config.Login.Method.Should().Be(LoginMethod.Token);
    }

    /// <summary>数值项写回，且越界被钳制到范围内。</summary>
    [Fact]
    public void 数值项越界钳制()
    {
        var config = new Config();
        var item = Create(config, "Warning.Timeout");

        item.TextValue = "9999";
        item.Apply();

        config.Warning.Timeout.Should().Be(600);
        item.TextValue.Should().Be("600");
    }

    /// <summary>坐标项按 <c>x, y</c> 解析。</summary>
    [Fact]
    public void 坐标项写回()
    {
        var config = new Config();
        var item = Create(config, "Login.Position.AccountLoginTab");

        item.TextValue = "100, 200";
        item.Apply();

        config.Login.Position.AccountLoginTab.Should().Be(new PixelPoint(100, 200));
    }

    /// <summary>括号写法也要能解析：界面里显示为 (x, y) 时，未修改直接保存不能失败。</summary>
    [Theory]
    [InlineData("(172, 1044)")]
    [InlineData("[172, 1044]")]
    [InlineData("172,1044")]
    [InlineData(" 172 ， 1044 ")]
    public void 坐标括号写法可解析(string text)
    {
        var config = new Config();
        var item = Create(config, "Login.Position.AccountInput");

        item.TextValue = text;
        item.Apply();

        config.Login.Position.AccountInput.Should().Be(new PixelPoint(172, 1044));
    }

    /// <summary>载入时坐标按 "x, y" 显示，保证显示值与解析器互逆。</summary>
    [Fact]
    public void 坐标显示与解析互逆()
    {
        var config = new Config();
        config.Login.Position.AccountInput = new PixelPoint(172, 1044);

        var item = Create(config, "Login.Position.AccountInput");

        item.TextValue.Should().Be("172, 1044");
        item.Apply();
        config.Login.Position.AccountInput.Should().Be(new PixelPoint(172, 1044));
    }

    /// <summary>坐标格式非法时必须明确报错，而不是静默写成 (0,0)。</summary>
    [Fact]
    public void 坐标格式非法抛异常()
    {
        var config = new Config();
        var item = Create(config, "Login.Position.AccountLoginTab");
        item.TextValue = "100";

        var action = () => item.Apply();

        action.Should().Throw<FormatException>();
    }

    /// <summary>颜色项以 <c>#AARRGGBB</c> 文本写回。</summary>
    [Fact]
    public void 颜色项写回()
    {
        var config = new Config();
        var item = Create(config, "Banner.Style.TextColor");

        item.TextValue = "#80112233";
        item.Apply();

        config.Banner.Style.TextColor.Should().Be(ArgbColor.Parse("#80112233"));
    }

    /// <summary>加载时数值按不变文化格式化，避免小数点随语言漂移。</summary>
    [Fact]
    public void 数值文本使用不变文化()
    {
        var config = new Config();
        var item = Create(config, "Login.Timeout.LaunchPollingInterval");

        item.TextValue.Should().Be("0.5");
    }

    private static SettingItemViewModel Create(Config config, string path)
    {
        var descriptor = ConfigCatalog.Build().Single(item => item.Path == path);
        return new SettingItemViewModel(config, descriptor);
    }
}
