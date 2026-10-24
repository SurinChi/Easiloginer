using FluentAssertions;

namespace EasiLoginer.Architecture.Tests;

/// <summary>
/// 分层依赖规则（文档 §4.3）。这些测试是合并门禁的一部分：
/// 任何跨越允许方向的引用都会让构建失败，而不是靠代码评审发现。
/// </summary>
public sealed class LayeringTests
{
    private static readonly string[] PlatformAndUi =
    [
        "System.Windows",
        "Microsoft.Win32",
        "System.Net.Http",
    ];

    /// <summary>
    /// 仅 Domain 禁止依赖 <c>Microsoft.Extensions.*</c>（文档 §4.3 规则 1）；
    /// Application 允许使用日志抽象（§3.1 技术选型表）。
    /// </summary>
    private static readonly string[] Extensions =
    [
        "Microsoft.Extensions",
    ];

    private static readonly string[] ImplementationLayers =
    [
        "EasiLoginer.Infrastructure",
        "EasiLoginer.Integrations",
        "EasiLoginer.AutomationKit",
    ];

    /// <summary>Domain 必须是不依赖任何东西的纯 BCL 层。</summary>
    [Fact]
    public void Domain不依赖任何外层与平台库()
    {
        var facts = Read("EasiLoginer.Domain");

        AssertNoDependency(
            facts,
            "EasiLoginer.Domain",
            [.. PlatformAndUi, .. Extensions, .. ImplementationLayers, "EasiLoginer.Application", "EasiLoginer.Presentation", "EasiLoginer.Host"]);
    }

    /// <summary>Domain 只允许引用 BCL，任何第三方包都应先去 Application 层。</summary>
    [Fact]
    public void Domain只引用BCL()
    {
        var facts = Read("EasiLoginer.Domain");

        var thirdParty = facts.AssemblyReferences
            .Where(name => !name.StartsWith("System.", StringComparison.Ordinal) &&
                           !name.Equals("System", StringComparison.Ordinal) &&
                           !name.Equals("netstandard", StringComparison.Ordinal) &&
                           !name.Equals("mscorlib", StringComparison.Ordinal))
            .ToArray();

        thirdParty.Should().BeEmpty("Domain 只允许引用 BCL");
    }

    /// <summary>Application 只依赖 Domain。</summary>
    [Fact]
    public void Application不依赖实现层与平台库()
    {
        var facts = Read("EasiLoginer.Application");

        AssertNoDependency(
            facts,
            "EasiLoginer.Application",
            [.. PlatformAndUi, .. ImplementationLayers, "EasiLoginer.Presentation", "EasiLoginer.Host"]);
    }

    /// <summary>实现层之间互不引用，也不得反向依赖呈现层。</summary>
    [Fact]
    public void Infrastructure不依赖其他实现层()
    {
        var facts = Read("EasiLoginer.Infrastructure");

        AssertNoDependency(
            facts,
            "EasiLoginer.Infrastructure",
            ["EasiLoginer.Integrations", "EasiLoginer.AutomationKit", "EasiLoginer.Presentation", "EasiLoginer.Host"]);
    }

    /// <summary>希沃集成不得依赖其他实现层。</summary>
    [Fact]
    public void Integrations不依赖其他实现层()
    {
        var facts = Read("EasiLoginer.Integrations.EasiNote");

        AssertNoDependency(
            facts,
            "EasiLoginer.Integrations.EasiNote",
            ["EasiLoginer.Infrastructure", "EasiLoginer.AutomationKit", "EasiLoginer.Presentation", "EasiLoginer.Host"]);
    }

    /// <summary>自动化层不得依赖其他实现层。</summary>
    [Fact]
    public void Automation不依赖其他实现层()
    {
        var facts = Read("EasiLoginer.AutomationKit");

        AssertNoDependency(
            facts,
            "EasiLoginer.AutomationKit",
            ["EasiLoginer.Infrastructure", "EasiLoginer.Integrations", "EasiLoginer.Presentation", "EasiLoginer.Host"]);
    }

    /// <summary>呈现层只通过端口调用实现层，不得直接引用。</summary>
    [Fact]
    public void Presentation不直接引用实现层()
    {
        var facts = Read("EasiLoginer.Presentation");

        AssertNoDependency(
            facts,
            "EasiLoginer.Presentation",
            [.. ImplementationLayers, "EasiLoginer.Host"]);
    }

    /// <summary>宿主是唯一组合根，但不得被任何层反向引用。</summary>
    [Fact]
    public void 任何层都不反向引用Host()
    {
        foreach (var name in new[] { "EasiLoginer.Domain", "EasiLoginer.Application", "EasiLoginer.Infrastructure" })
        {
            Read(name).AssemblyReferences.Should().NotContain(
                reference => reference.Equals("EasiLoginer", StringComparison.OrdinalIgnoreCase),
                $"{name} 不应引用宿主程序集");
        }
    }

    private static AssemblyFacts Read(string assemblyName)
        => MetadataProbe.Read(Path.Combine(AppContext.BaseDirectory, assemblyName + ".dll"));

    private static void AssertNoDependency(AssemblyFacts facts, string assemblyName, string[] forbidden)
    {
        var offenders = facts.Namespaces
            .Where(reference => forbidden.Any(prefix => reference.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            .Order(StringComparer.Ordinal)
            .ToArray();

        offenders.Should().BeEmpty($"{assemblyName} 出现了禁止的依赖");
    }
}
