using System.IO;
using System.Text;

namespace EasiLoginer.Infrastructure.Storage;

/// <summary>
/// 文本文件的原子写入：先写随机命名的临时文件，再整体替换目标文件。
/// </summary>
/// <remarks>
/// <para>
/// 随机临时名避免并发保存时互相覆盖同一个 <c>.tmp</c>；进程内锁把「写临时文件 → 替换」
/// 串成一步。替换使用 <see cref="File.Replace(string, string, string?)"/>，目标被瞬时占用时
/// 短暂重试——这在 Windows 上很常见（杀软扫描、另一个实例正在写）。
/// </para>
/// <para>
/// 这只解决同一进程内的并发与占用；跨进程的「最后写入者胜出」仍需调用方协调。
/// </para>
/// </remarks>
internal static class AtomicFile
{
    private const int ReplaceAttempts = 3;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(50);
    private static readonly object Gate = new();

    /// <summary>原子写入 UTF-8 文本（无 BOM）。</summary>
    public static void WriteAllText(string path, string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(content);

        var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(content);
        var temporary = path + "." + Path.GetRandomFileName() + ".tmp";

        lock (Gate)
        {
            File.WriteAllBytes(temporary, bytes);

            try
            {
                if (File.Exists(path))
                {
                    ReplaceWithRetry(temporary, path);
                }
                else
                {
                    File.Move(temporary, path);
                }
            }
            catch
            {
                TryDelete(temporary);
                throw;
            }
        }
    }

    private static void ReplaceWithRetry(string source, string destination)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                File.Replace(source, destination, destinationBackupFileName: null);
                return;
            }
            catch (IOException) when (attempt < ReplaceAttempts)
            {
                Thread.Sleep(RetryDelay);
            }
        }
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
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // 清理失败不能掩盖真正的写入异常
        }
    }
}
