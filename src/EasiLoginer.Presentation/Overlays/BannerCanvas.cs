using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace EasiLoginer.Presentation.Overlays;

/// <summary>
/// 横幅绘制画布：斜纹背景 + 循环滚动文字。
/// </summary>
/// <remarks>
/// 用 <see cref="System.Windows.Threading.DispatcherTimer"/> 按配置 FPS 节流，
/// 每帧推进文字与斜纹偏移后重绘（文档 §10.5）。
/// </remarks>
public sealed class BannerCanvas : FrameworkElement
{
    private const double StripeSpacing = 40;
    private const double StripeThickness = 12;
    private const double FontSize_ = 34;

    private readonly BannerOptions _options;
    private readonly BannerScroll _scroll = new();
    private readonly FormattedText _formattedText;
    private System.Windows.Threading.DispatcherTimer? _timer;
    private double _stripeOffset;

    /// <summary>创建画布。</summary>
    public BannerCanvas(BannerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;

        var typeface = new Typeface(
            new FontFamily(string.IsNullOrWhiteSpace(options.FontFamily) ? "Microsoft YaHei UI" : options.FontFamily),
            FontStyles.Normal,
            FontWeights.SemiBold,
            FontStretches.Normal);

        _formattedText = new FormattedText(
            options.Text,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            typeface,
            FontSize_,
            new SolidColorBrush(options.TextColor),
            1.0);
    }

    /// <summary>开始动画。</summary>
    public void Start()
    {
        _scroll.Reset(0);
        _timer ??= new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(_options.FrameIntervalMilliseconds),
        };

        _timer.Tick -= OnTick;
        _timer.Tick += OnTick;
        _timer.Start();
    }

    /// <summary>停止动画。</summary>
    public void Stop()
    {
        if (_timer is null)
        {
            return;
        }

        _timer.Stop();
        _timer.Tick -= OnTick;
    }

    /// <inheritdoc />
    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        var width = ActualWidth;
        var height = ActualHeight;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        drawingContext.PushClip(new RectangleGeometry(new Rect(0, 0, width, height)));

        var background = new SolidColorBrush(_options.BackgroundColor);
        drawingContext.DrawRectangle(background, null, new Rect(0, 0, width, height));

        // 斜纹：40px 间隔、12px 粗，随帧平移
        var stripePen = new Pen(new SolidColorBrush(_options.ForegroundColor), StripeThickness);
        for (var x = -height + _stripeOffset; x < width; x += StripeSpacing)
        {
            drawingContext.DrawLine(stripePen, new Point(x, height), new Point(x + height, 0));
        }

        // 文字：滚动到尾部后紧接着再画一份，形成无缝循环
        var textWidth = _formattedText.WidthIncludingTrailingWhitespace;
        var textY = (height - _formattedText.Height) / 2;

        drawingContext.DrawText(_formattedText, new Point(_scroll.Offset, textY));
        drawingContext.DrawText(_formattedText, new Point(_scroll.Offset + textWidth + StripeSpacing, textY));

        drawingContext.Pop();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        _scroll.Advance(_options.Speed, _formattedText.WidthIncludingTrailingWhitespace, ActualWidth);
        _stripeOffset += _options.Speed;
        if (_stripeOffset > StripeSpacing)
        {
            _stripeOffset -= StripeSpacing;
        }
        else if (_stripeOffset < 0)
        {
            _stripeOffset += StripeSpacing;
        }

        InvalidateVisual();
    }
}
