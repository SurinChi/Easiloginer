using System.IO;
using System.Security.Cryptography;
using EasiLoginer.Application.Update;
using EasiLoginer.Domain.Update;

namespace EasiLoginer.Infrastructure.Update;

/// <summary>
/// 用 HTTP 下载更新资产：写入临时文件、边下边算 SHA-256、完成后校验大小与摘要。
/// </summary>
public sealed class UpdateDownloader : IUpdateDownloader
{
    private readonly HttpClient _http;
    private readonly string _directory;

    /// <summary>创建下载器。</summary>
    /// <param name="http">HTTP 客户端（由调用方管理生命周期）。</param>
    /// <param name="directory">保存目录；默认 <c>%TEMP%\EasiLoginer-updates</c>。</param>
    public UpdateDownloader(HttpClient http, string? directory = null)
    {
        ArgumentNullException.ThrowIfNull(http);

        _http = http;
        _directory = directory ?? Path.Combine(Path.GetTempPath(), "EasiLoginer-updates");
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

        var fileName = GetSafeFileName(asset.Url);
        var finalPath = Path.Combine(_directory, fileName);
        var temporaryPath = finalPath + ".part";

        try
        {
            using var response = await _http
                .GetAsync(asset.Url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            var total = response.Content.Headers.ContentLength ?? asset.Size;

            using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

            string digest;
            long received = 0;

            // 先写完并关闭文件，再改名，避免 Windows 上的占用错误
            await using (var destination = File.Create(temporaryPath))
            {
                using var sha = SHA256.Create();
                var buffer = new byte[81920];
                int read;

                while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                {
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
                throw new IOException($"下载大小不一致：期望 {asset.Size} 字节，实际 {received} 字节。");
            }

            if (!string.IsNullOrWhiteSpace(asset.Sha256) &&
                !string.Equals(digest, asset.Sha256.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException("下载文件的 SHA-256 与清单不一致，已丢弃。");
            }

            File.Move(temporaryPath, finalPath, overwrite: true);
            return new UpdateDownloadResult(finalPath, received);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // 用户取消：如实上抛（调用方据此回到可重试状态），同时清掉半成品
            TryDelete(temporaryPath);
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            // 非用户取消的 TaskCanceledException 是 HttpClient 超时
            TryDelete(temporaryPath);
            throw new IOException($"下载失败：{exception.Message}", exception);
        }
        catch
        {
            TryDelete(temporaryPath);
            throw;
        }
    }

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
