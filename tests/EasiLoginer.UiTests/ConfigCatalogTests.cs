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

        groups.Should().Contain(["Login", "Warning", "Banner", "StatusOverlay", "App", "Experimental", "Update"]);
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

    /// <summary>括号里的解释变成二级说明，标题只保留必要的计数单位。</summary>
    [Fact]
    public void 括号提示改为二级文字()
    {
        var items = ConfigCatalog.Build().ToDictionary(item => item.Path);

        items["Login.SkipOnce"].Title.Should().Be("取消二次确认");
        items["Login.SkipOnce"].Hint.Should().Be("勾选后运行登录不再弹确认");

        items["Experimental.PrivacyMask.Enabled"].Title.Should().Be("启用隐私保护遮罩");
        items["Experimental.PrivacyMask.Enabled"].Hint.Should().Contain("智慧大屏");

        // 计数单位保留在标题里，仅把补充说明放进二级文字
        items["App.ResetDataAfterDays"].Title.Should().Be("未启动清理（天）");
        items["App.ResetDataAfterDays"].Hint.Should().Be("0 关闭，最短 7 天");
        items["Warning.Timeout"].Title.Should().Be("超时时长（秒）");
        items["Warning.Timeout"].Hint.Should().BeNullOrEmpty();
    }

    /// <summary>启动页面是可选的枚举项。</summary>
    [Fact]
    public void 启动页面可选()
    {
        var descriptor = ConfigCatalog.Build().Single(item => item.Path == "App.StartPage");

        descriptor.Kind.Should().Be(SettingKind.Enum);
        descriptor.Title.Should().Be("启动页面");
        descriptor.Options.Should().Contain(["自动（有档案进快捷登录）", "快捷登录", "档案", "设置"]);
    }

    /// <summary>已移除的遥测开关不再出现在目录里。</summary>
    [Fact]
    public void 遥测开关已隐藏()
    {
        ConfigCatalog.Build().Select(item => item.Path).Should().NotContain("App.TelemetryEnabled");
    }

    /// <summary>
    /// 还没有实现消费方的开关不能出现在界面上：用户改了却没有任何效果，
    /// 比看不到这个选项更糟（<c>Profile.SkipVerify</c> 目前就属于这一类）。
    /// </summary>
    [Fact]
    public void 无实现的开关不暴露()
    {
        ConfigCatalog.Build().Select(item => item.Path).Should().NotContain("Profile.SkipVerify");
    }

    /// <summary>隐藏项不出现在界面目录中。</summary>
    [Theory]
    [InlineData("Login.Position.BaseSize")]
    [InlineData("Login.Position.LoginWindowSize")]
    [InlineData("Update.LastVersion")]
    [InlineData("Profile.SkipVerify")]
    public void 隐藏项被排除(string path)
    {
        ConfigCatalog.Build().Select(descriptor => descriptor.Path).Should().NotContain(path);
    }

    /// <summary>字体项必须是下拉（Font 类型），且选项来自系统字体。</summary>
    [Fact]
    public void 字体项是下拉()
    {
        var config = new Config();
        var descriptor = ConfigCatalog.Build().Single(item => item.Path == "Banner.Style.TextFont");
        descriptor.Kind.Should().Be(SettingKind.Font);

        var item = new SettingItemViewModel(config, descriptor);
        item.IsFont.Should().BeTrue();
        item.IsTextual.Should().BeFalse();
        item.FontOptions.Should().NotBeEmpty();
    }

    /// <summary>未安装的字体也会出现在选项里，避免保存时被改掉。</summary>
    [Fact]
    public void 当前字体保留在选项中()
    {
        var config = new Config();
        config.Banner.Style.TextFont = "不存在的字体 XYZ";
        var descriptor = ConfigCatalog.Build().Single(item => item.Path == "Banner.Style.TextFont");

        var item = new SettingItemViewModel(config, descriptor);

        item.TextValue.Should().Be("不存在的字体 XYZ");
        item.FontOptions.Should().Contain("不存在的字体 XYZ");
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
