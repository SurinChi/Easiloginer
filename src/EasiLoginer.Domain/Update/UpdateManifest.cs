using EasiLoginer.Domain.Configuration;
using EasiLoginer.Domain.Versioning;

namespace EasiLoginer.Domain.Update;

/// <summary>更新检查结果分类。</summary>
public enum UpdateAvailability
{
    /// <summary>尚未配置更新源（首发默认状态）。</summary>
    NotConfigured,

    /// <summary>已是最新。</summary>
    UpToDate,

    /// <summary>有新版本可用。</summary>
    Available,

    /// <summary>清单与当前通道/版本不兼容。</summary>
    Unsupported,

    /// <summary>检查失败（网络或清单格式问题）。</summary>
    Failed,
}

/// <summary>清单中的一个下载资产。</summary>
/// <param name="Source">镜像源标识（github / ghproxy / ghfast）。</param>
/// <param name="Url">下载地址。</param>
/// <param name="Sha256">SHA-256（小写十六进制），用于校验。</param>
/// <param name="Size">字节数。</param>
public sealed record UpdateAsset(string Source, string Url, string Sha256, long Size);

/// <summary>自有更新清单（只描述 EasiLoginer 自己的资产，不消费上游清单）。</summary>
/// <param name="Version">版本号（PEP 440）。</param>
/// <param name="Channel">所属通道。</param>
/// <param name="Notes">发布说明。</param>
/// <param name="Assets">下载资产。</param>
public sealed record UpdateManifest(
    Pep440Version Version,
    UpdateChannel Channel,
    string? Notes,
    IReadOnlyList<UpdateAsset> Assets);

/// <summary>检查结果。</summary>
/// <param name="Availability">分类。</param>
/// <param name="Manifest">有新版本时的清单。</param>
/// <param name="Message">面向用户的说明。</param>
public sealed record UpdateCheckResult(
    UpdateAvailability Availability,
    UpdateManifest? Manifest,
    string Message);
