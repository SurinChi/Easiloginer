using System.Reflection;
using System.Windows.Controls;

namespace EasiLoginer.Presentation.Pages;

/// <summary>关于页：版本、上游归属与许可信息（文档 §10.2）。</summary>
public partial class AboutPage : Page
{
    /// <summary>创建关于页。</summary>
    public AboutPage()
    {
        InitializeComponent();

        // 页面高度必须钉在导航容器内，否则内部 ScrollViewer 会被撑成内容高度，
        // 滚轮事件落不到它身上（关于页正文较长，需要能滚动）。
        PageLayout.ConstrainToNavigationHost(this);

        var assembly = typeof(AboutPage).Assembly;
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        // 兜底：历史构建可能带 "+<commit>"，只取版本本身
        var version = informational?.Split('+')[0] ?? assembly.GetName().Version?.ToString() ?? "未知";
        VersionText.Text = $"版本 {version} · GPLv3";
    }
}
