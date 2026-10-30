using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Wpf.Ui.Controls;

namespace EasiLoginer.Presentation.Pages;

/// <summary>
/// 页面尺寸约束：把页面高度限制在导航容器的可视高度内。
/// </summary>
/// <remarks>
/// 背景：<see cref="NavigationView"/> 的内容宿主会给页面无限高度，页面里的
/// <c>ScrollViewer</c> 因此把整页撑到内容高度。结果是焦点落到下方控件时，
/// 外层容器会连页头（标题、保存按钮）一起滚走——表现为"点一下空白，上方的栏消失了"。
/// 把页面高度钉在宿主高度上之后，滚动只发生在页面内部的滚动区，页头保持不动。
/// </remarks>
internal static class PageLayout
{
    /// <summary>让页面高度跟随导航容器，并在尺寸变化时保持同步。</summary>
    public static void ConstrainToNavigationHost(Page page)
    {
        ArgumentNullException.ThrowIfNull(page);

        void Apply()
        {
            if (!page.IsLoaded)
            {
                return;
            }

            var host = FindAncestor<NavigationView>(page);
            if (host is not null && host.ActualHeight > 0)
            {
                page.MaxHeight = host.ActualHeight;
            }
        }

        page.Loaded += (_, _) => Apply();
        page.SizeChanged += (_, _) => Apply();
    }

    private static T? FindAncestor<T>(DependencyObject node)
        where T : DependencyObject
    {
        var current = VisualTreeHelper.GetParent(node);
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }
}
