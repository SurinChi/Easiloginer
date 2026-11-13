using System.Net;
using System.Text;
using EasiLoginer.Application.Announcements;

namespace EasiLoginer.Infrastructure.Announcements;

/// <summary>
/// 从固定地址读取公告清单；若安装目录下存在 <c>announcements/announcements.json</c> 则优先用它。
/// </summary>
/// <remarks>
/// <para>
/// 公开地址是本仓库 <c>main</c> 分支上的 <c>announcements/announcements.json</c>，
/// 维护者只需要提交这个文件即可投放公告，不需要重新打包。规则见仓库根目录
/// <c>announcements/README.md</c>。
/// </para>
/// <para>
/// 本地文件优先是为了离线预览与测试：把同样的 JSON 放到程序目录的 <c>announcements/</c> 下即可，
/// 发布包默认不包含该文件（否则会永远盖住远程清单）。
/// </para>
/// </remarks>
public sealed class HttpAnnouncementSource : IAnnouncementSource
{
    /// <summary>默认公告清单地址。</summary>
    public const string DefaultEndpoint =
        "https://raw.githubusercontent.com/SurinChi/EasiLoginer/main/announcements/announcements.json";

    /// <summary>清单大小上限（256 KB）：公告是纯文本，超过这个量级说明文件出了问题。</summary>
    public const int MaxPayloadBytes = 256 * 1024;

    /// <summary>单次读取超时（文档 §6.6 的 3–5 秒区间）。</summary>
    public static readonly TimeSpan FetchTimeout = TimeSpan.FromSeconds(5);

    private readonly HttpClient _http;
    private readonly string _endpoint;
    private readonly string? _localFilePath;

    /// <summary>创建公告来源。</summary>
    /// <param name="http">HTTP 客户端（由调用方管理生命周期）。</param>
    /// <param name="endpoint">清单地址；默认 <see cref="DefaultEndpoint"/>。</param>
    /// <param name="localFilePath">可选的本地清单路径；存在时优先读取。</param>
    public HttpAnnouncementSource(HttpClient http, string? endpoint = null, string? localFilePath = null)
    {
        ArgumentNullException.ThrowIfNull(http);

        _http = http;
        _endpoint = string.IsNullOrWhiteSpace(endpoint) ? DefaultEndpoint : endpoint;
        _localFilePath = localFilePath;
    }

    /// <inheritdoc />
    public async Task<string?> FetchAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_localFilePath) && File.Exists(_localFilePath))
        {
            var local = await File.ReadAllTextAsync(_localFilePath, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
            if (local.Length > MaxPayloadBytes)
            {
                throw new IOException("本地公告文件超过大小上限。");
            }

            return local;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(FetchTimeout);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, _endpoint);
            request.Headers.UserAgent.ParseAdd("EasiLoginer-Announcements");

            using var response = await _http
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token)
                .ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();

            if (response.Content.Headers.ContentLength is > MaxPayloadBytes)
            {
                throw new IOException("公告清单超过大小上限。");
            }

            var text = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
            if (text.Length > MaxPayloadBytes)
            {
                throw new IOException("公告清单超过大小上限。");
            }

            return text;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException exception)
        {
            throw new IOException("公告服务响应超时。", exception);
        }
        catch (HttpRequestException exception)
        {
            throw new IOException($"公告服务不可用：{exception.Message}", exception);
        }
    }
}
