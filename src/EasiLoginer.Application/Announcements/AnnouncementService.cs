using EasiLoginer.Domain.Announcements;
using EasiLoginer.Domain.Versioning;
using Microsoft.Extensions.Logging;

namespace EasiLoginer.Application.Announcements;

/// <summary>
/// 公告用例：拉取 → 解析 → 按规则过滤。失败一律静默降级为空列表（文档 §6.6）。
/// </summary>
/// <remarks>
/// 公告属于「有则更好」的能力：网络错误、清单写坏、超时都不能影响启动或登录，
/// 因此这里不向上抛异常，只写一条 warning 日志。
/// </remarks>
public sealed class AnnouncementService
{
    private readonly IAnnouncementSource _source;
    private readonly ILogger<AnnouncementService> _logger;

    /// <summary>创建公告服务。</summary>
    public AnnouncementService(IAnnouncementSource source, ILogger<AnnouncementService> logger)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(logger);

        _source = source;
        _logger = logger;
    }

    /// <summary>取当前应展示的公告（已按时间窗口、版本范围与隐藏记录过滤）。</summary>
    /// <param name="currentVersion">当前应用版本。</param>
    /// <param name="hiddenIds">已隐藏的公告 ID。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public async Task<IReadOnlyList<Announcement>> FetchAsync(
        Pep440Version currentVersion,
        IReadOnlyCollection<string>? hiddenIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(currentVersion);

        try
        {
            var json = await _source.FetchAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(json))
            {
                return [];
            }

            var parsed = AnnouncementManifestParser.Parse(json);
            return AnnouncementPolicy.Select(parsed, currentVersion, DateTimeOffset.Now, hiddenIds);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return [];
        }
        catch (Exception exception) when (exception is IOException or FormatException or System.Text.Json.JsonException)
        {
            _logger.LogWarning(exception, "公告读取失败，本次不显示公告");
            return [];
        }
    }
}
