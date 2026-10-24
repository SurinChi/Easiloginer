using Microsoft.Extensions.Logging.Abstractions;
using EasiLoginer.Infrastructure.Windows;
using FluentAssertions;

namespace EasiLoginer.Infrastructure.Tests;

/// <summary>
/// Win32 进程/窗口原语的真机冒烟。
/// </summary>
/// <remarks>
/// 这里只做"不崩溃、结果形态正确"的断言，刻意不启动或关闭任何真实应用程序，
/// 以免干扰用户桌面。真实的前台化行为需人工验证。
/// </remarks>
public sealed class WindowsPrimitivesTests
{
    /// <summary>终止一个不存在的进程不应抛异常。</summary>
    [Fact]
    public void 终止不存在的进程不抛异常()
    {
        var manager = new WindowsProcessManager(NullLogger<WindowsProcessManager>.Instance);

        var action = () => manager.Kill("EasiLoginerNoSuchProcess_" + Guid.NewGuid().ToString("N"), force: true, wait: false);

        action.Should().NotThrow();
    }

    /// <summary>枚举顶层窗口应返回结果，且元素字段可用。</summary>
    [Fact]
    public void 枚举窗口可以运行()
    {
        var service = new WindowsWindowService(NullLogger<WindowsWindowService>.Instance);

        var windows = service.EnumerateWindows();

        windows.Should().NotBeEmpty("交互式桌面上至少应有若干可见窗口");
        windows.Should().OnlyContain(window => window.Handle != 0);
        windows.Should().Contain(window => window.Title.Length > 0);
    }

    /// <summary>查找不存在的标题返回 0（包括包含匹配）。</summary>
    [Fact]
    public void 查找不存在的窗口返回零()
    {
        var service = new WindowsWindowService(NullLogger<WindowsWindowService>.Instance);
        var title = "EasiLoginer-不存在的窗口-" + Guid.NewGuid().ToString("N");

        service.FindWindow(title, substring: false).Should().Be(0);
        service.FindWindow(title, substring: true).Should().Be(0);
    }

    /// <summary>无效句柄的前台化必须安全失败。</summary>
    [Fact]
    public void 无效句柄前台化失败()
    {
        var service = new WindowsWindowService(NullLogger<WindowsWindowService>.Instance);

        service.SwitchToWindow(0).Should().BeFalse();
        service.SwitchToWindow(0, pressKey: true).Should().BeFalse();
    }

    /// <summary>读取前台窗口句柄不抛异常（交互式会话下通常非 0）。</summary>
    [Fact]
    public void 读取前台窗口可以运行()
    {
        var service = new WindowsWindowService(NullLogger<WindowsWindowService>.Instance);

        _ = service.GetForegroundWindow();
    }
}
