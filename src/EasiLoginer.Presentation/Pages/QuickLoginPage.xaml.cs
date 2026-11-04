using System.Windows;
using System.Windows.Controls;
using EasiLoginer.Domain.Profiles;

namespace EasiLoginer.Presentation.Pages;

/// <summary>快捷登录页：每个档案一个按钮，点击即执行该档案的登录。</summary>
public partial class QuickLoginPage : Page
{
    private readonly QuickLoginPageViewModel _viewModel;

    /// <summary>创建快捷登录页。</summary>
    public QuickLoginPage(AppServices services)
    {
        ArgumentNullException.ThrowIfNull(services);

        InitializeComponent();
        _viewModel = new QuickLoginPageViewModel(services.Profiles, services.Login, services.Config, services.Display);
        DataContext = _viewModel;
        PageLayout.ConstrainToNavigationHost(this);

        Loaded += (_, _) => _viewModel.Refresh();
    }

    private async void OnProfileClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: AutomationProfile profile })
        {
            await _viewModel.RunAsync(profile);
        }
    }
}
