using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Controls;
using EasiLoginer.Application.Abstractions;
using EasiLoginer.Domain.Configuration;
using EasiLoginer.Presentation.Configuration;

namespace EasiLoginer.Presentation.Onboarding;

/// <summary>
/// 首次设置向导窗口：6 步、可跳过、完成后回到主界面（文档 §10.7）。
/// </summary>
public partial class OobeWizardWindow : FluentWindow
{
    private readonly WizardViewModel _viewModel;

    /// <summary>创建向导。</summary>
    public OobeWizardWindow(IConfigService config)
    {
        ArgumentNullException.ThrowIfNull(config);

        InitializeComponent();
        _viewModel = new WizardViewModel(config);
        Render();
        WizardTheme.Apply(this, Root);
    }

    /// <summary>完成后是否应打开档案页（否则打开配置页）。</summary>
    public bool FinishToProfile { get; private set; }

    /// <summary>是否已经完成或跳过向导（关闭窗口时也会置位）。</summary>
    /// <remarks>
    /// <see cref="Window.ShowDialog"/> 在用户直接关窗时返回 <see langword="null"/>，
    /// 但向导可能在关闭前已按“已同意协议”完成收尾；首启逻辑需要这个标记区分
    /// “同意后关窗”和“未同意就关窗”。
    /// </remarks>
    public bool IsCompleted => _viewModel.Completed;

    /// <inheritdoc />
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // 直接关窗也视为"跳过"：写 IsOobeCompleted=true，避免每次启动都弹向导。
        // 但用户协议尚未同意时不能这样收尾——否则「必须同意才能继续」形同虚设；
        // 此时只是本次不设置，下次启动会再次弹出向导。
        if (!_viewModel.Completed && CanSkipWizard)
        {
            _viewModel.SkipWizard();
        }

        base.OnClosing(e);
    }

    /// <summary>协议页未同意时，既不能跳过向导，也不能通过关闭窗口绕过。</summary>
    private bool CanSkipWizard
        => _viewModel.AgreementAccepted ||
           _viewModel.Steps.All(step => step.Kind != WizardStepKind.Agreement);

    private void Render()
    {
        var step = _viewModel.CurrentStep;

        StepTitle.Text = step.Title;
        StepProgress.Text = _viewModel.ProgressText;
        StepContent.Children.Clear();

        if (step.Kind == WizardStepKind.Agreement)
        {
            // 协议页自己管理「说明 + 可伸缩正文 + 勾选」的布局，
            // 避免正文固定高度把勾选框挤出可视区域。
            StepContent.Children.Add(BuildAgreement(step.Description));
        }
        else
        {
            StepContent.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = step.Description,
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.88,
                FontSize = 14,
            });

            switch (step.Kind)
            {
                case WizardStepKind.Theme:
                    StepContent.Children.Add(BuildThemeSelector());
                    break;
                case WizardStepKind.LoginMethod:
                    StepContent.Children.Add(BuildMethodSelector());
                    break;
                case WizardStepKind.Finish:
                    StepContent.Children.Add(BuildFinishSelector());
                    break;
            }
        }

        PreviousButton.IsEnabled = !_viewModel.IsFirst;
        // 协议是使用本软件的前提：未勾选“我同意”前，任何一步都不能跳过向导。
        SkipButton.IsEnabled = !_viewModel.IsLast && step.Skippable && _viewModel.AgreementAccepted;
        NextButton.Content = _viewModel.IsLast ? "完成" : "下一步";
        UpdateNextAvailability();
    }

    /// <summary>
    /// 用户协议页：正文必须滚动到底部才允许勾选「我同意」，
    /// 勾选后才允许「下一步」。
    /// </summary>
    private Grid BuildAgreement(string description)
    {
        // 用 Grid 的 * 行让正文吃掉可用高度：正文、提示和勾选框始终同屏，
        // 用户只需滚动协议正文本身，不会因为外层滚动而看不到「我同意」。
        var panel = new Grid { Margin = new Thickness(0, 0, 0, 0) };
        panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // 高度跟随外层滚动视口，保证协议页不产生第二层页面滚动。
        panel.SetBinding(
            FrameworkElement.HeightProperty,
            new System.Windows.Data.Binding(nameof(ScrollViewer.ViewportHeight))
            {
                Source = StepScroller,
            });

        var intro = new System.Windows.Controls.TextBlock
        {
            Text = description,
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.88,
            FontSize = 14,
        };
        Grid.SetRow(intro, 0);
        panel.Children.Add(intro);

        var body = new System.Windows.Controls.TextBlock
        {
            Text = OobeSteps.AgreementText,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 22,
            Padding = new Thickness(12, 10, 12, 10),
        };

        var scroller = new ScrollViewer
        {
            Content = body,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Padding = new Thickness(0),
            Focusable = true,
        };

        var frame = new Border
        {
            Child = scroller,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Margin = new Thickness(0, 8, 0, 0),
            Background = (System.Windows.Media.Brush)FindResource("ControlFillColorDefaultBrush"),
            BorderBrush = (System.Windows.Media.Brush)FindResource("ControlStrokeColorDefaultBrush"),
        };
        Grid.SetRow(frame, 1);
        panel.Children.Add(frame);

        var hint = new System.Windows.Controls.TextBlock
        {
            Text = "请滚动到协议末尾，以启用下方的勾选。",
            Opacity = 0.7,
            FontSize = 12,
            Margin = new Thickness(0, 8, 0, 0),
            TextWrapping = TextWrapping.Wrap,
        };
        Grid.SetRow(hint, 2);
        panel.Children.Add(hint);

        var accept = new CheckBox
        {
            Content = "我同意",
            IsChecked = _viewModel.AgreementAccepted,
            IsEnabled = _viewModel.AgreementAccepted,
            Margin = new Thickness(0, 8, 0, 0),
        };
        Grid.SetRow(accept, 3);
        panel.Children.Add(accept);

        void AllowAccept()
        {
            if (accept.IsEnabled)
            {
                return;
            }

            accept.IsEnabled = true;
            hint.Text = "已到达协议末尾，现在可以勾选“我同意”。";
        }

        // 正文尚未测量时 ActualHeight 为 0，此时不能误判为“已经到底”。
        // 内容短到无需滚动时 ScrollChanged 不一定触发，因此 Loaded 后再补判一次。
        void CheckScrolledToEnd()
        {
            if (body.ActualHeight <= 0)
            {
                return;
            }

            if (scroller.ScrollableHeight <= 0 ||
                scroller.VerticalOffset >= scroller.ScrollableHeight - 1)
            {
                AllowAccept();
            }
        }

        scroller.Loaded += (_, _) =>
            Dispatcher.BeginInvoke(new Action(CheckScrolledToEnd), System.Windows.Threading.DispatcherPriority.Loaded);

        scroller.ScrollChanged += (_, _) => CheckScrolledToEnd();

        accept.Checked += (_, _) =>
        {
            _viewModel.AgreementAccepted = true;
            UpdateNextAvailability();
        };

        accept.Unchecked += (_, _) =>
        {
            _viewModel.AgreementAccepted = false;
            UpdateNextAvailability();
        };

        // 从后面的步骤退回来时，之前已经同意过就不再要求重新滚到底部
        if (_viewModel.AgreementAccepted)
        {
            hint.Text = "已同意本协议。";
        }

        return panel;
    }

    /// <summary>协议页未勾选时禁用「下一步」；「跳过向导」同样受同意状态约束。</summary>
    private void UpdateNextAvailability()
    {
        var needsAgreement = _viewModel.CurrentStep.Kind == WizardStepKind.Agreement;
        NextButton.IsEnabled = !needsAgreement || _viewModel.AgreementAccepted;

        SkipButton.IsEnabled = !_viewModel.IsLast
                               && _viewModel.CurrentStep.Skippable
                               && _viewModel.AgreementAccepted;
    }

    private ComboBox BuildThemeSelector()
    {
        var combo = new ComboBox { Width = 220, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 16, 0, 0) };
        combo.Items.Add("跟随系统");
        combo.Items.Add("浅色");
        combo.Items.Add("深色");
        combo.SelectedIndex = _viewModel.ThemeIndex;
        combo.SelectionChanged += (_, _) =>
        {
            if (combo.SelectedIndex >= 0)
            {
                _viewModel.ThemeIndex = combo.SelectedIndex;
                ThemeService.Apply(_viewModel.ThemeIndex switch
                {
                    1 => ThemeOptions.Light,
                    2 => ThemeOptions.Dark,
                    _ => ThemeOptions.Auto,
                });

                // 全局主题换掉后，向导自身也要重新套用，否则窗口还是旧主题
                Wpf.Ui.Appearance.ApplicationThemeManager.Apply(this);
                WizardTheme.Apply(this, Root);
            }
        };
        return combo;
    }

    private ComboBox BuildMethodSelector()
    {
        var combo = new ComboBox { Width = 260, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 16, 0, 0) };
        // 顺序与 LoginMethod 枚举一致（Fixed=0, Cv=1, Uia=2, Token=3），
        // 名称统一从设置页使用的同一份标签表读取。
        foreach (var label in LoginMethodLabels.All)
        {
            combo.Items.Add(label);
        }
        combo.SelectedIndex = _viewModel.MethodIndex;
        combo.SelectionChanged += (_, _) =>
        {
            if (combo.SelectedIndex >= 0)
            {
                _viewModel.MethodIndex = combo.SelectedIndex;
            }
        };
        return combo;
    }

    private StackPanel BuildFinishSelector()
    {
        var panel = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(0, 16, 0, 0) };

        var configOption = new RadioButton { Content = "完成后查看设置", IsChecked = _viewModel.FinishPageIndex == 0, Margin = new Thickness(0, 0, 0, 6) };
        var profileOption = new RadioButton { Content = "完成后管理档案", IsChecked = _viewModel.FinishPageIndex == 1 };

        configOption.Checked += (_, _) => _viewModel.FinishPageIndex = 0;
        profileOption.Checked += (_, _) => _viewModel.FinishPageIndex = 1;

        panel.Children.Add(configOption);
        panel.Children.Add(profileOption);
        return panel;
    }

    private void OnNextClicked(object sender, RoutedEventArgs e)
    {
        _viewModel.Next();

        if (_viewModel.Completed)
        {
            FinishToProfile = _viewModel.FinishPageIndex == 1;
            DialogResult = true;
            return;
        }

        Render();
    }

    private void OnPreviousClicked(object sender, RoutedEventArgs e)
    {
        _viewModel.Previous();
        Render();
    }

    private void OnSkipClicked(object sender, RoutedEventArgs e)
    {
        if (!CanSkipWizard)
        {
            return;
        }

        _viewModel.SkipWizard();
        FinishToProfile = false;
        DialogResult = true;
    }
}
