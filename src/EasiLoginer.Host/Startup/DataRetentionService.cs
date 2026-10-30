using System.IO;
using EasiLoginer.Domain.Retention;
using EasiLoginer.Host.Logging;
using EasiLoginer.Infrastructure.Configuration;
using EasiLoginer.Infrastructure.Paths;
using EasiLoginer.Infrastructure.Storage;

namespace EasiLoginer.Host.Startup;

/// <summary>
/// 启动时的数据保留检查：超过配置天数未启动则清空数据，恢复初始状态。
/// </summary>
/// <remarks>
/// 必须在读取配置、打开日志**之前**执行：清理会删掉配置与日志，
/// 之后的正常启动流程会重新生成默认值。因此这里只返回清理结果，
/// 由调用方在日志就绪后再记录。
/// </remarks>
internal static class DataRetentionService
{
    /// <summary>按需清理，并记录本次启动时间。</summary>
    /// <returns>发生清理时返回清理信息；否则返回 <see langword="null"/>。</returns>
    public static RetentionWipe? ApplyIfExpired(AppPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        var now = DateTimeOffset.UtcNow;
        var configuredDays = ReadConfiguredDays(paths);
        var store = new LaunchStateStore(paths);
        var state = store.Load();

        RetentionWipe? wipe = null;

        if (DataRetentionPolicy.ShouldReset(state.LastLaunchUtc, now, configuredDays))
        {
            var days = DataRetentionPolicy.Normalize(configuredDays);
            var removed = DataWipeService.Wipe(paths);
            wipe = new RetentionWipe(days, removed);
        }

        try
        {
            store.Save(new LaunchState(now));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // 写不了状态只是下次判断不准，不影响本次启动
        }

        return wipe;
    }

    private static int ReadConfiguredDays(AppPaths paths)
    {
        try
        {
            var config = new ConfigStore(paths, new SerilogLogger<ConfigStore>()).Load();
            return config.App.ResetDataAfterDays;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return DataRetentionPolicy.DefaultDays;
        }
    }

    /// <summary>一次数据清理的结果。</summary>
    /// <param name="Days">触发清理的天数阈值。</param>
    /// <param name="Files">被删除的文件数。</param>
    internal sealed record RetentionWipe(int Days, int Files);
}
