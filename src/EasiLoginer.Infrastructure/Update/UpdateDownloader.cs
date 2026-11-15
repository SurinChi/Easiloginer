using System.IO;
using System.Security.Cryptography;
using EasiLoginer.Application.Update;
using EasiLoginer.Domain.Configuration;
using EasiLoginer.Domain.Update;

namespace EasiLoginer.Infrastructure.Update;

/// <summary>
/// 用 HTTP 下载更新资产：写入临时文件、边下边算 SHA-256、完成后校验大小与摘要。
/// </summary>
/// <remarks>
/// <para>
/// GitHub 的发布资产会重定向到 <c>objects.githubusercontent.com</c>，该域名在部分网络
/// 环境完全不可达（连接一直无响应）。因此下载器支持镜像回退：<see cref="DownloadSource.Auto"/>
/// 先尝试直连，失败后依次尝试 ghfast / ghproxy 镜像；指定镜像时只走指定镜像。
/// </para>
/// <para>
/// <see cref="HttpClient.Timeout"/> 不应设置成 20 秒之类的短值——大文件下载会正常超过
/// 这个时间。这里要求外部传入 <see cref="Timeout.InfiniteTimeSpan"/> 的客户端，由本类用
/// 连接超时和“无数据停滞”超时控制，避免慢速但仍在下载的连接被误杀。
/// </para>
/// </remarks>
public sealed class UpdateDownloader : IUpdateDownloader
{
    private const string GhFastPrefix = "https://ghfast.top/";
    private const string GhProxyPrefix = "https://ghproxy.net/";

    private readonly HttpClient _http;
    private readonly string _directory;
    private readonly Func<DownloadSource> _source;
    private readonly TimeSpan _headerTimeout;
    private readonly TimeSpan _inactivityTimeout;

    /// <summary>创建下载器。</summary>
    /// <param name="http">HTTP 客户端（建议 <see cref="HttpClient.Timeout"/> 设为无限）。</param>
    /// <param name="directory">保存目录；默认 <c>%TEMP%\EasiLoginer-updates</c>。</param>
    /// <param name="sourceProvider">读取当前下载镜像源；默认自动。</param>
    /// <param name="headerTimeout">等待响应头的超时；默认 10 秒。</param>
    /// <param name="inactivityTimeout">连续无数据的最长等待；默认 60 秒。</param>
    public UpdateDownloader(
        HttpClient http,
        string? directory = null,
        Func<DownloadSource>? sourceProvider = null,
        TimeSpan? headerTimeout = null,
        TimeSpan? inactivityTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(http);

        _http = http;
        _directory = directory ?? Path.Combine(Path.GetTempPath(), "EasiLoginer-updates");
        _source = sourceProvider ?? (() => DownloadSource.Auto);
        _headerTimeout = headerTimeout ?? TimeSpan.FromSeconds(10);
        _inactivityTimeout = inactivityTimeout ?? TimeSpan.FromSeconds(60);
    }

    /// <inheritdoc />
    public async Task<UpdateDownloadResult> DownloadAsync(
        UpdateAsset asset,
        IProgress<UpdateDownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(asset);

        if (string.IsNullOrWhiteSpace(asset.Url))
        {
            throw new IOException("更新资产没有下载地址。");
        }

        Directory.CreateDirectory(_directory);

        var failures = new List<string>();
        foreach (var url in ResolveCandidates(asset, _source()))
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                return await DownloadFromAsync(url, asset, progress, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception) when (exception is IOException or HttpRequestException or TaskCanceledException)
            {
                failures.Add($"{Describe(url)}：{exception.Message}");
            }
        }

        throw new IOException($"所有下载源均不可用：{string.Join("；", failures)}");
    }

    /// <summary>
    /// 根据清单资产和用户选择的下载源生成候选地址。
    /// </summary>
    /// <remarks>
    /// 只对 GitHub Release 资产做镜像改写；Gitee 或自定义清单的地址保持原样。
    /// <see cref="DownloadSource.Auto"/> 的顺序是直连 → ghfast → ghproxy，直连失败时才回退。
    /// </remarks>
    internal static IReadOnlyList<string> ResolveCandidates(UpdateAsset asset, DownloadSource source)
    {
        ArgumentNullException.ThrowIfNull(asset);

        if (!IsGitHubReleaseAsset(asset))
        {
            return [asset.Url];
        }

        return source switch
        {
            DownloadSource.GitHub => [asset.Url],
            DownloadSource.GhProxy => [GhProxyPrefix + asset.Url],
            DownloadSource.GhFast => [GhFastPrefix + asset.Url],
            _ => [asset.Url, GhFastPrefix + asset.Url, GhProxyPrefix + asset.Url],
        };
    }

    private static bool IsGitHubReleaseAsset(UpdateAsset asset)
    {
        if (!string.Equals(asset.Source, "github", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return Uri.TryCreate(asset.Url, UriKind.Absolute, out var uri) &&
               (string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(uri.Host, "www.github.com", StringComparison.OrdinalIgnoreCase));
    }

    private async Task<UpdateDownloadResult> DownloadFromAsync(
        string url,
        UpdateAsset asset,
        IProgress<UpdateDownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        var fileName = GetSafeFileName(url);
        var finalPath = Path.Combine(_directory, fileName);
        var temporaryPath = finalPath + ".part";

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.UserAgent.ParseAdd("EasiLoginer-Updater");

            using var headerTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            headerTimeout.CancelAfter(_headerTimeout);

            HttpResponseMessage response;
            try
            {
                response = await _http
                    .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, headerTimeout.Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new IOException($"连接超时（{_headerTimeout.TotalSeconds:0} 秒）");
            }

            using (response)
            {
                response.EnsureSuccessStatusCode();

                var total = response.Content.Headers.ContentLength ?? asset.Size;

                using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

                string digest;
                long received = 0;

                await using (var destination = File.Create(temporaryPath))
                {
                    using var sha = SHA256.Create();
                    var buffer = new byte[81920];

                    while (true)
                    {
                        int read;
                        try
                        {
                            // 每次读取单独计时：只要还在持续收到数据就一直下载，
                            // 超过 60 秒一个字节都没有才判定为停滞。
                            using var readTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                            readTimeout.CancelAfter(_inactivityTimeout);
                            read = await source.ReadAsync(buffer, readTimeout.Token).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                        {
                            throw new IOException($"下载停滞超过 {_inactivityTimeout.TotalSeconds:0} 秒");
                        }

                        if (read == 0)
                        {
                            break;
                        }

                        await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                        sha.TransformBlock(buffer, 0, read, null, 0);
                        received += read;
                        progress?.Report(new UpdateDownloadProgress(received, total));
                    }

                    sha.TransformFinalBlock([], 0, 0);
                    digest = Convert.ToHexString(sha.Hash!).ToLowerInvariant();
                }

                if (asset.Size > 0 && received != asset.Size)
                {
                    throw new IOException($"文件大小不一致：期望 {asset.Size} 字节，实际 {received} 字节。");
                }

                if (!string.IsNullOrWhiteSpace(asset.Sha256) &&
                    !string.Equals(digest, asset.Sha256.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    throw new IOException("文件校验失败：SHA-256 与清单不一致，已丢弃。");
                }

                File.Move(temporaryPath, finalPath, overwrite: true);
                return new UpdateDownloadResult(finalPath, received);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            TryDelete(temporaryPath);
            throw;
        }
        catch
        {
            TryDelete(temporaryPath);
            throw;
        }
    }

    private static string Describe(string url)
        => Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : url;

    private static string GetSafeFileName(string url)
    {
        var name = Path.GetFileName(new Uri(url).AbsolutePath);
        if (string.IsNullOrWhiteSpace(name))
        {
            name = "EasiLoginer-update.bin";
        }

        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalid, '_');
        }

        return name;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
