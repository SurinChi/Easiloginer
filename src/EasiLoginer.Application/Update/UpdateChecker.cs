using System.Text.Json;
using EasiLoginer.Domain.Configuration;
using EasiLoginer.Domain.Update;
using EasiLoginer.Domain.Versioning;

namespace EasiLoginer.Application.Update;

/// <summary>更新清单来源端口（实现为 HTTP 或本地文件，便于测试）。</summary>
/// <remarks>
/// 网络失败请由实现方包装成 <see cref="IOException"/>：Application 层不引用
/// <c>System.Net.Http</c>（文档 §4.3 的分层规则）。
/// </remarks>
public interface IUpdateSource
{
    /// <summary>取指定通道的清单 JSON；未配置时返回 <see langword="null"/>。</summary>
    Task<string?> FetchManifestJsonAsync(UpdateChannel channel, CancellationToken cancellationToken);
}

/// <summary>
/// 更新检查：拉取自有清单 → 解析 → 版本与通道比对。
/// </summary>
/// <remarks>
/// 只读取 EasiLoginer 自己的清单格式（文档 §11.1 / §12.4），不消费上游 EasiAuto 的清单。
/// 版本比较使用 <see cref="Pep440Version"/>，因此 <c>0.0.2b1</c> 这类预发布号也能正确排序。
/// </remarks>
public sealed class UpdateChecker
{
    private readonly IUpdateSource _source;

    /// <summary>创建检查器。</summary>
    public UpdateChecker(IUpdateSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
    }

    /// <summary>执行一次检查。</summary>
    public async Task<UpdateCheckResult> CheckAsync(
        Pep440Version currentVersion,
        UpdateChannel channel,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(currentVersion);

        string? json;
        try
        {
            json = await _source.FetchManifestJsonAsync(channel, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or TaskCanceledException)
        {
            return new UpdateCheckResult(UpdateAvailability.Failed, null, $"更新源不可用：{exception.Message}");
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            return new UpdateCheckResult(UpdateAvailability.NotConfigured, null, "尚未配置更新源");
        }

        UpdateManifest manifest;
        try
        {
            manifest = Parse(json);
        }
        catch (Exception exception) when (exception is JsonException or FormatException or InvalidOperationException or KeyNotFoundException)
        {
            return new UpdateCheckResult(UpdateAvailability.Failed, null, $"更新清单格式非法：{exception.Message}");
        }

        if (manifest.Channel != channel)
        {
            return new UpdateCheckResult(
                UpdateAvailability.Unsupported,
                null,
                $"清单通道为 {manifest.Channel}，与当前通道 {channel} 不一致");
        }

        if (manifest.Version.CompareTo(currentVersion) <= 0)
        {
            return new UpdateCheckResult(UpdateAvailability.UpToDate, null, $"已是最新版本（{currentVersion}）");
        }

        return new UpdateCheckResult(
            UpdateAvailability.Available,
            manifest,
            $"发现新版本 {manifest.Version}（当前 {currentVersion}）");
    }

    private static UpdateManifest Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new FormatException("清单根节点必须是对象");
        }

        var versionText = root.TryGetProperty("version", out var version) ? version.GetString() : null;
        if (string.IsNullOrWhiteSpace(versionText))
        {
            throw new FormatException("缺少 version");
        }

        var channelText = root.TryGetProperty("channel", out var channelElement) ? channelElement.GetString() : null;
        var channel = string.Equals(channelText, "dev", StringComparison.OrdinalIgnoreCase)
            ? UpdateChannel.Dev
            : UpdateChannel.Release;

        var notes = root.TryGetProperty("notes", out var notesElement) && notesElement.ValueKind == JsonValueKind.String
            ? notesElement.GetString()
            : null;

        var assets = new List<UpdateAsset>();
        if (root.TryGetProperty("assets", out var assetsElement) && assetsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in assetsElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var source = item.TryGetProperty("source", out var sourceElement) ? sourceElement.GetString() ?? string.Empty : string.Empty;
                var url = item.TryGetProperty("url", out var urlElement) ? urlElement.GetString() ?? string.Empty : string.Empty;
                var sha = item.TryGetProperty("sha256", out var shaElement) ? shaElement.GetString() ?? string.Empty : string.Empty;
                var size = item.TryGetProperty("size", out var sizeElement) && sizeElement.TryGetInt64(out var parsed) ? parsed : 0L;

                if (url.Length > 0)
                {
                    assets.Add(new UpdateAsset(source, url, sha, size));
                }
            }
        }

        return new UpdateManifest(Pep440Version.Parse(versionText), channel, notes, assets);
    }
}
