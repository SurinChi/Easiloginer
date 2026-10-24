namespace EasiLoginer.Infrastructure.Paths;

/// <summary>
/// 便携式应用路径布局，对应上游 <c>consts.py</c>。
/// </summary>
/// <remarks>
/// 所有可变数据都在 exe 同级的 <c>data/</c> 下，绝不写 <c>%APPDATA%</c>
/// （唯一例外是只读希沃自己的 <c>Configs.fkv</c>）。布局见文档 §7.1。
/// </remarks>
public sealed class AppPaths
{
    /// <summary>以当前进程的可执行文件所在目录构造路径。</summary>
    public static AppPaths Default { get; } = new(AppContext.BaseDirectory);

    /// <summary>以指定目录作为应用根目录构造路径（便于测试）。</summary>
    /// <param name="baseDirectory">exe 所在目录，必须是绝对路径。</param>
    public AppPaths(string baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(baseDirectory) || !Path.IsPathRooted(baseDirectory))
        {
            throw new ArgumentException("应用根目录必须是绝对路径。", nameof(baseDirectory));
        }

        BaseDirectory = Path.GetFullPath(baseDirectory);
    }

    /// <summary>exe 所在目录。</summary>
    public string BaseDirectory { get; }

    /// <summary>可变数据目录。</summary>
    public string DataDirectory => Path.Combine(BaseDirectory, "data");

    /// <summary>配置文件。</summary>
    public string ConfigFile => Path.Combine(DataDirectory, "config.json");

    /// <summary>档案文件。</summary>
    public string ProfileFile => Path.Combine(DataDirectory, "profile.json");

    /// <summary>DPAPI 缓存的派生密钥（隐藏文件）。</summary>
    public string UnlockFile => Path.Combine(DataDirectory, "profile.unlock");

    /// <summary>旧版机器密钥文件（仅迁移时读取）。</summary>
    public string LegacyKeyFile => Path.Combine(DataDirectory, "profile.key");

    /// <summary>日志目录。</summary>
    public string LogsDirectory => Path.Combine(DataDirectory, "logs");

    /// <summary>更新包缓存目录。</summary>
    public string CacheDirectory => Path.Combine(DataDirectory, "cache");

    /// <summary>头像缓存目录。</summary>
    public string AvatarsDirectory => Path.Combine(DataDirectory, "avatars");

    /// <summary>只读资源目录（图标、希沃界面模板）。</summary>
    public string ResourcesDirectory => Path.Combine(BaseDirectory, "resources");

    /// <summary>随包分发的第三方二进制（补丁源文件）。</summary>
    public string VendorsDirectory => Path.Combine(BaseDirectory, "vendors");

    /// <summary>创建全部可变目录（幂等）。</summary>
    public void EnsureWritableDirectories()
    {
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(CacheDirectory);
        Directory.CreateDirectory(AvatarsDirectory);
    }
}
