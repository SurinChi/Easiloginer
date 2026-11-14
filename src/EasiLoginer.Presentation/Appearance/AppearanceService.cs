using System.Windows;
using System.Windows.Media;
using EasiLoginer.Domain.Configuration;
using Wpf.Ui.Appearance;

namespace EasiLoginer.Presentation.Appearance;

/// <summary>
/// 把外观配置解析成窗口背景层使用的资源：
/// 背景画刷、模糊半径、玻璃层颜色/不透明度。
/// </summary>
/// <remarks>
/// <para>
/// 采用应用内模糊，而不是系统 DWM 亚克力：系统亚克力的模糊半径固定，
/// 无法满足「模糊强度可调」；应用内实现还能在 Windows 10 / 11 上保持一致。
/// </para>
/// <para>
/// 资源键由主窗口、首次向导等窗口通过 <c>DynamicResource</c> 引用，
/// 保存设置后调用 <see cref="Apply"/> 即可实时刷新，不需要重建窗口。
/// </para>
/// </remarks>
public static class AppearanceService
{
    /// <summary>背景图片 / 壁纸画刷。</summary>
    public const string BackgroundBrushKey = "AcrylicBackgroundBrush";

    /// <summary>覆盖在背景上的玻璃层画刷。</summary>
    public const string TintBrushKey = "AcrylicTintBrush";

    /// <summary>背景层模糊半径。</summary>
    public const string BlurRadiusKey = "AcrylicBlurRadius";

    /// <summary>背景层的负外边距，用来遮住模糊产生的透明边缘。</summary>
    public const string BackgroundMarginKey = "AcrylicBackgroundMargin";

    private static AppearanceConfig? _appearance;

    /// <summary>按配置应用外观，并记住当前配置供主题切换时重算。</summary>
    public static void Apply(Config config)
    {
        ArgumentNullException.ThrowIfNull(config);

        _appearance = config.Appearance;
        Refresh();
    }

    /// <summary>主题变化后重算玻璃层颜色；尚未应用过外观时什么也不做。</summary>
    public static void OnThemeChanged()
    {
        if (_appearance is null)
        {
            return;
        }

        Refresh();
    }

    /// <summary>重算并写入应用资源。</summary>
    public static void Refresh()
    {
        var application = System.Windows.Application.Current;
        if (application is null)
        {
            return;
        }

        var appearance = _appearance ?? new AppearanceConfig();
        var resources = application.Resources;
        var themeBackground = resources["ApplicationBackgroundBrush"] as Brush ?? CreateDefaultThemeBrush();

        var (background, blurRadius, tint) = Resolve(appearance, themeBackground);

        resources[BackgroundBrushKey] = background;
        resources[TintBrushKey] = tint;
        resources[BlurRadiusKey] = blurRadius;

        // 模糊会把元素的边缘也一起模糊掉，负边距确保边缘落在窗口之外。
        resources[BackgroundMarginKey] = blurRadius > 0
            ? new Thickness(-(blurRadius + 24))
            : new Thickness(0);
    }

    private static (Brush Background, double BlurRadius, Brush Tint) Resolve(
        AppearanceConfig appearance,
        Brush themeBackground)
    {
        if (!appearance.AcrylicEnabled || appearance.BackgroundMode == BackgroundMode.Theme)
        {
            return (themeBackground, 0, Brushes.Transparent);
        }

        if (appearance.BackgroundMode == BackgroundMode.Solid)
        {
            return (Freeze(new SolidColorBrush(ToColor(appearance.BackgroundColor))), 0, Brushes.Transparent);
        }

        if (appearance.BackgroundMode == BackgroundMode.Gradient)
        {
            var gradient = new LinearGradientBrush(
                ToColor(appearance.BackgroundColor),
                ToColor(appearance.BackgroundColor2),
                new Point(0, 0),
                new Point(1, 1));
            return (Freeze(gradient), 0, Brushes.Transparent);
        }

        var image = appearance.BackgroundMode == BackgroundMode.Image
            ? WallpaperLocator.LoadImage(appearance.BackgroundImagePath) ?? WallpaperLocator.LoadDesktopWallpaper()
            : WallpaperLocator.LoadDesktopWallpaper();

        if (image is null)
        {
            // 壁纸不可用（虚拟机、幻灯片、路径失效）时不能让窗口变成透明或空白。
            return (themeBackground, 0, Brushes.Transparent);
        }

        var imageBrush = new ImageBrush(image)
        {
            Stretch = Stretch.UniformToFill,
            AlignmentX = AlignmentX.Center,
            AlignmentY = AlignmentY.Center,
        };

        return (Freeze(imageBrush), appearance.EffectiveBlurRadius, CreateGlassTint(appearance));
    }

    private static SolidColorBrush CreateGlassTint(AppearanceConfig appearance)
    {
        var light = ThemeService.CurrentTheme == ApplicationTheme.Light;
        var baseColor = light
            ? Color.FromRgb(0xF3, 0xF3, 0xF3)
            : Color.FromRgb(0x1C, 0x1C, 0x1C);

        var alpha = (byte)Math.Round(Math.Clamp(appearance.GlassOpacity, 0.2, 1.0) * 255);
        return Freeze(new SolidColorBrush(Color.FromArgb(alpha, baseColor.R, baseColor.G, baseColor.B)));
    }

    private static Color ToColor(ArgbColor color) => Color.FromArgb(color.A, color.R, color.G, color.B);

    private static SolidColorBrush CreateDefaultThemeBrush()
        => Freeze(new SolidColorBrush(ThemeService.CurrentTheme == ApplicationTheme.Dark
            ? Color.FromRgb(0x20, 0x20, 0x20)
            : Colors.White));

    private static T Freeze<T>(T freezable)
        where T : Freezable
    {
        if (freezable.CanFreeze)
        {
            freezable.Freeze();
        }

        return freezable;
    }
}
