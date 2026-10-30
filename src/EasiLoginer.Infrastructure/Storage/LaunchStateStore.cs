using System.Text.Json;
using System.Text.Json.Serialization;
using EasiLoginer.Infrastructure.Paths;

namespace EasiLoginer.Infrastructure.Storage;

/// <summary>上次启动时间。</summary>
/// <param name="LastLaunchUtc">上次启动的 UTC 时间；首次启动时为 <see langword="null"/>。</param>
public sealed record LaunchState(DateTimeOffset? LastLaunchUtc);

/// <summary>
/// 启动状态的读写：<c>data/state.json</c>。
/// </summary>
/// <remarks>
/// 独立于 <c>config.json</c>：启动时间每次运行都会更新，放在配置里会持续改写用户配置，
/// 也会被"数据清理"一并删除（这正是我们想要的）。
/// </remarks>
public sealed class LaunchStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly AppPaths _paths;

    /// <summary>创建存储。</summary>
    public LaunchStateStore(AppPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        _paths = paths;
    }

    /// <summary>状态文件路径。</summary>
    public string FilePath => Path.Combine(_paths.DataDirectory, "state.json");

    /// <summary>读取状态；文件缺失或损坏时返回空状态。</summary>
    public LaunchState Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return new LaunchState(null);
            }

            var state = JsonSerializer.Deserialize<LaunchState>(File.ReadAllText(FilePath), JsonOptions);
            return state ?? new LaunchState(null);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return new LaunchState(null);
        }
    }

    /// <summary>写入状态。</summary>
    public void Save(LaunchState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        Directory.CreateDirectory(_paths.DataDirectory);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(state, JsonOptions));
    }
}
