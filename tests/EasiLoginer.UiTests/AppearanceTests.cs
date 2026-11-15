using System.IO;
using EasiLoginer.Domain.Configuration;
using EasiLoginer.Presentation.Appearance;
using EasiLoginer.Presentation.Configuration;
using FluentAssertions;

namespace EasiLoginer.UiTests;

/// <summary>
/// 外观与背景：配置项目录、滑杆 / 文件选择编辑器、亚克力参数换算与图片加载。
/// </summary>
public sealed class AppearanceTests
{
    /// <summary>外观分组出现在「应用与外观」分类中，且包含全部七项。</summary>
    [Fact]
    public void 外观分组包含全部设置()
    {
        var descriptors = ConfigCatalog.Build()
            .Where(descriptor => descriptor.Group == "Appearance")
            .ToDictionary(descriptor => descriptor.Path);

        descriptors.Keys.Should().BeEquivalentTo(
        [
            "Appearance.AcrylicEnabled",
            "Appearance.BlurStrength",
            "Appearance.AcrylicOpacity",
            "Appearance.BackgroundMode",
            "Appearance.BackgroundColor",
            "Appearance.BackgroundColor2",
            "Appearance.BackgroundImagePath",
        ]);

        ConfigCatalog.GetGroupTitle("Appearance").Should().Be("外观与背景");
        ConfigCatalog.BuildCategories().Select(category => category.Title)
            .Should().Contain("应用与外观");
    }

    /// <summary>模糊强度和不透明度用滑杆，范围分别是 0–60 与 20–100。</summary>
    [Fact]
    public void 亚克力参数使用滑杆()
    {
        var items = ConfigCatalog.Build().ToDictionary(descriptor => descriptor.Path);

        items["Appearance.BlurStrength"].Kind.Should().Be(SettingKind.Slider);
        items["Appearance.BlurStrength"].Min.Should().Be(0);
        items["Appearance.BlurStrength"].Max.Should().Be(60);

        items["Appearance.AcrylicOpacity"].Kind.Should().Be(SettingKind.Slider);
        items["Appearance.AcrylicOpacity"].Min.Should().Be(20);
        items["Appearance.AcrylicOpacity"].Max.Should().Be(100);
    }

    /// <summary>背景来源是枚举，选项顺序与枚举一致；图片路径带浏览按钮。</summary>
    [Fact]
    public void 背景来源与图片路径()
    {
        var items = ConfigCatalog.Build().ToDictionary(descriptor => descriptor.Path);

        items["Appearance.BackgroundMode"].Kind.Should().Be(SettingKind.Enum);
        items["Appearance.BackgroundMode"].Options.Should().Equal(
            "跟随主题", "纯色", "渐变", "桌面壁纸", "本地图片");

        items["Appearance.BackgroundImagePath"].Kind.Should().Be(SettingKind.FilePath);
    }

    /// <summary>滑杆写回配置并钳制越界值。</summary>
    [Fact]
    public void 滑杆写回并钳制()
    {
        var config = new Config();
        var item = Create(config, "Appearance.BlurStrength");

        item.IsSlider.Should().BeTrue();
        item.Minimum.Should().Be(0);
        item.Maximum.Should().Be(60);

        item.NumberValue = 500;
        item.Apply();

        config.Appearance.BlurStrength.Should().Be(60);
        item.NumberValue.Should().Be(60);
        item.NumberText.Should().Be("60");
    }

    /// <summary>图片路径可以写入配置，且不会同时显示普通文本框。</summary>
    [Fact]
    public void 图片路径写回()
    {
        var config = new Config();
        var item = Create(config, "Appearance.BackgroundImagePath");

        item.IsFilePath.Should().BeTrue();
        item.IsTextual.Should().BeFalse();

        item.TextValue = @"C:\Pictures\wallpaper.png";
        item.Apply();

        config.Appearance.BackgroundImagePath.Should().Be(@"C:\Pictures\wallpaper.png");
    }

    /// <summary>背景来源按下标写回枚举。</summary>
    [Fact]
    public void 背景来源写回()
    {
        var config = new Config();
        var item = Create(config, "Appearance.BackgroundMode");

        item.SelectedIndex = (int)BackgroundMode.Gradient;
        item.Apply();

        config.Appearance.BackgroundMode.Should().Be(BackgroundMode.Gradient);
    }

    /// <summary>玻璃不透明度按百分比换算，并钳制到可读下限。</summary>
    [Theory]
    [InlineData(20, 0.2)]
    [InlineData(78, 0.78)]
    [InlineData(100, 1.0)]
    [InlineData(0, 0.2)]
    [InlineData(150, 1.0)]
    public void 玻璃不透明度换算(int percent, double expected)
    {
        var appearance = new AppearanceConfig { AcrylicOpacity = percent };

        appearance.GlassOpacity.Should().BeApproximately(expected, 0.0001);
    }

    /// <summary>只有壁纸 / 图片背景需要模糊，其他模式返回 0。</summary>
    [Theory]
    [InlineData(BackgroundMode.Theme, 0)]
    [InlineData(BackgroundMode.Solid, 0)]
    [InlineData(BackgroundMode.Gradient, 0)]
    [InlineData(BackgroundMode.Wallpaper, 32)]
    [InlineData(BackgroundMode.Image, 32)]
    public void 模糊半径只对图片背景生效(BackgroundMode mode, double expected)
    {
        var appearance = new AppearanceConfig
        {
            BackgroundMode = mode,
            BlurStrength = 32,
        };

        appearance.EffectiveBlurRadius.Should().Be(expected);
    }

    /// <summary>模糊强度上限为 60，防止过大的半径拖慢渲染。</summary>
    [Fact]
    public void 模糊强度上限()
    {
        var appearance = new AppearanceConfig
        {
            BackgroundMode = BackgroundMode.Wallpaper,
            BlurStrength = 999,
        };

        appearance.EffectiveBlurRadius.Should().Be(60);
    }

    /// <summary>图片不存在或格式非法时返回 null，由调用方回退主题背景。</summary>
    [Fact]
    public void 无效图片返回空()
    {
        WallpaperLocator.LoadImage(null).Should().BeNull();
        WallpaperLocator.LoadImage(string.Empty).Should().BeNull();
        WallpaperLocator.LoadImage(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".png"))
            .Should().BeNull();

        var bad = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".png");
        try
        {
            File.WriteAllText(bad, "not an image");
            WallpaperLocator.LoadImage(bad).Should().BeNull();
        }
        finally
        {
            File.Delete(bad);
        }
    }

    /// <summary>有效图片会被解码，且返回已冻结、可跨窗口使用的位图。</summary>
    [Fact]
    public void 有效图片可加载()
    {
        // 1×1 透明 PNG
        const string onePixelPng =
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=";

        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".png");
        try
        {
            File.WriteAllBytes(path, Convert.FromBase64String(onePixelPng));

            var image = WallpaperLocator.LoadImage(path);

            image.Should().NotBeNull();
            image!.IsFrozen.Should().BeTrue("背景位图会被多个窗口共享");
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static SettingItemViewModel Create(Config config, string path)
    {
        var descriptor = ConfigCatalog.Build().Single(item => item.Path == path);
        return new SettingItemViewModel(config, descriptor);
    }
}
