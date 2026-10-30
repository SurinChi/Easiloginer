using System.Windows;
using System.Windows.Controls;

namespace EasiLoginer.Presentation.Pages;

/// <summary>配置页（M5 首版为手写绑定；元数据驱动卡片在后续迭代补齐）。</summary>
public partial class ConfigPage : Page
{
    private readonly ConfigPageViewModel _viewModel;

    /// <summary>创建配置页。</summary>
    public ConfigPage(AppServices services)
    {
        ArgumentNullException.ThrowIfNull(services);

        InitializeComponent();
        _viewModel = new ConfigPageViewModel(services.Config);
        DataContext = _viewModel;
        PageLayout.ConstrainToNavigationHost(this);
    }

    private void OnSaveClicked(object sender, RoutedEventArgs e) => _viewModel.Save();
}
