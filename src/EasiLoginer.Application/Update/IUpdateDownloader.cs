using EasiLoginer.Domain.Update;

namespace EasiLoginer.Application.Update;

/// <summary>下载进度。</summary>
/// <param name="Received">已接收字节数。</param>
/// <param name="Total">总字节数（未知时为 0）。</param>
public sealed record UpdateDownloadProgress(long Received, long Total);

/// <summary>下载结果。</summary>
/// <param name="FilePath">下载到本地的文件路径。</param>
/// <param name="Size">文件字节数。</param>
public sealed record UpdateDownloadResult(string FilePath, long Size);

/// <summary>
/// 更新包下载端口：把清单里的资产下载到本地，并校验大小与 SHA-256。
/// </summary>
/// <remarks>
/// 网络实现放在 Infrastructure（Application 不引用 <c>System.Net.Http</c>）。
/// 校验失败按约定抛 <see cref="IOException"/>。
/// </remarks>
public interface IUpdateDownloader
{
    /// <summary>下载指定资产。</summary>
    /// <param name="asset">清单中的资产。</param>
    /// <param name="progress">进度回调。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<UpdateDownloadResult> DownloadAsync(
        UpdateAsset asset,
        IProgress<UpdateDownloadProgress>? progress,
        CancellationToken cancellationToken);
}
