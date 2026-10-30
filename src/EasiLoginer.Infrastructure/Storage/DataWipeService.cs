using System.IO;
using EasiLoginer.Infrastructure.Paths;

namespace EasiLoginer.Infrastructure.Storage;

/// <summary>
/// 数据清理：删除 <c>data/</c> 下的所有内容，恢复"首次安装"状态。
/// </summary>
/// <remarks>
/// 目录本身保留（后续写入需要它）；任何删除失败都只跳过，不影响启动。
/// </remarks>
public static class DataWipeService
{
    /// <summary>清空数据目录。</summary>
    /// <returns>被删除的条目数。</returns>
    public static int Wipe(AppPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        var directory = paths.DataDirectory;
        if (!Directory.Exists(directory))
        {
            return 0;
        }

        var removed = 0;

        foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
        {
            try
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
                removed++;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // 文件被占用时跳过，尽力而为
            }
        }

        foreach (var subdirectory in Directory.EnumerateDirectories(directory, "*", SearchOption.AllDirectories))
        {
            try
            {
                if (!Directory.EnumerateFileSystemEntries(subdirectory).Any())
                {
                    Directory.Delete(subdirectory);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
            }
        }

        return removed;
    }
}
