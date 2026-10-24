using System.Security.Cryptography;
using EasiLoginer.Infrastructure.Paths;

namespace EasiLoginer.Infrastructure.Security;

/// <summary>
/// 解锁密钥的本机缓存：用 DPAPI 按当前用户加密后写入 <c>data/profile.unlock</c>。
/// </summary>
/// <remarks>
/// 对应上游 <c>cache_master_key / read_cached_key / clear_cached_key</c>：
/// 缓存文件带隐藏属性；读取失败（换用户、文件损坏）一律返回 <see langword="null"/>，
/// 由上层回退到手动输入主密码。
/// </remarks>
public sealed class DpapiKeyCache
{
    private readonly AppPaths _paths;

    /// <summary>创建缓存访问器。</summary>
    public DpapiKeyCache(AppPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        _paths = paths;
    }

    /// <summary>缓存路径。</summary>
    public string CacheFile => _paths.UnlockFile;

    /// <summary>写入缓存（DPAPI 当前用户加密，文件隐藏）。</summary>
    public void Save(ReadOnlySpan<byte> key)
    {
        var blob = ProtectedData.Protect(key.ToArray(), optionalEntropy: null, DataProtectionScope.CurrentUser);

        Directory.CreateDirectory(_paths.DataDirectory);
        ClearHiddenAttribute();
        File.WriteAllBytes(CacheFile, blob);
        File.SetAttributes(CacheFile, FileAttributes.Hidden);
    }

    /// <summary>读取缓存；不存在、损坏或非当前用户加密时返回 <see langword="null"/>。</summary>
    public byte[]? TryRead()
    {
        try
        {
            if (!File.Exists(CacheFile))
            {
                return null;
            }

            var blob = File.ReadAllBytes(CacheFile);
            if (blob.Length == 0)
            {
                return null;
            }

            return ProtectedData.Unprotect(blob, optionalEntropy: null, DataProtectionScope.CurrentUser);
        }
        catch (CryptographicException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>删除缓存（关闭加密时调用）。</summary>
    public void Clear()
    {
        ClearHiddenAttribute();

        try
        {
            File.Delete(CacheFile);
        }
        catch (IOException)
        {
            // 删除失败不影响调用方（下次写入会覆盖）
        }
        catch (UnauthorizedAccessException)
        {
            // 同上
        }
    }

    private void ClearHiddenAttribute()
    {
        if (!File.Exists(CacheFile))
        {
            return;
        }

        try
        {
            File.SetAttributes(CacheFile, FileAttributes.Normal);
        }
        catch (IOException)
        {
            // 忽略
        }
        catch (UnauthorizedAccessException)
        {
            // 忽略
        }
    }
}
