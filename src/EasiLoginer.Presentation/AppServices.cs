using EasiLoginer.Application.Abstractions;
using EasiLoginer.Application.Automation;
using EasiLoginer.Application.Profiles;
using EasiLoginer.Application.Update;

namespace EasiLoginer.Presentation;

/// <summary>呈现层依赖的服务集合（由宿主组合根注入）。</summary>
/// <param name="Config">配置服务。</param>
/// <param name="Profiles">档案仓储。</param>
/// <param name="Login">登录执行器。</param>
/// <param name="Display">屏幕度量（覆盖层按物理像素定位后换算 DIP）。</param>
/// <param name="Update">更新清单来源（GitHub Releases）。</param>
public sealed record AppServices(
    IConfigService Config,
    IProfileRepository Profiles,
    ILoginRunner Login,
    IDisplayMetrics Display,
    IUpdateSource Update);
