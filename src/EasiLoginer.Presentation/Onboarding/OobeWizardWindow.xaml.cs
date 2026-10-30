using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Controls;
using EasiLoginer.Application.Abstractions;
using EasiLoginer.Domain.Configuration;

namespace EasiLoginer.Presentation.Onboarding;

/// <summary>
/// 首次设置向导窗口：7 步、可跳过、完成后回到主界面（文档 §10.7）。
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
    }

    /// <summary>完成后是否应打开档案页（否则打开配置页）。</summary>
    public bool FinishToProfile { get; private set; }

    /// <inheritdoc />
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // 直接关窗也视为"跳过"：写 IsOobeCompleted=true，避免每次启动都弹向导
        if (!_viewModel.Completed)
        {
            _viewModel.SkipWizard();
        }

        base.OnClosing(e);
    }

    private void Render()
    {
        var step = _viewModel.CurrentStep;

        StepTitle.Text = step.Title;
        StepProgress.Text = _viewModel.ProgressText;
        StepContent.Children.Clear();

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

        PreviousButton.IsEnabled = !_viewModel.IsFirst;
        SkipButton.IsEnabled = !_viewModel.IsLast;
        NextButton.Content = _viewModel.IsLast ? "完成" : "下一步";
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
            }
        };
        return combo;
    }

    private ComboBox BuildMethodSelector()
    {
        var combo = new ComboBox { Width = 260, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 16, 0, 0) };
        combo.Items.Add("固定位置");
        combo.Items.Add("图像识别（尚未实现）");
        combo.Items.Add("自动定位（尚未实现）");
        combo.Items.Add("令牌投递");
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

        var configOption = new RadioButton { Content = "完成后查看配置", IsChecked = _viewModel.FinishPageIndex == 0, Margin = new Thickness(0, 0, 0, 6) };
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
        _viewModel.SkipWizard();
        FinishToProfile = false;
        DialogResult = true;
    }
}
