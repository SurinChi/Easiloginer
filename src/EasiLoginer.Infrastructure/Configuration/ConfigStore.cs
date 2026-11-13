using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using EasiLoginer.Domain.Configuration;
using EasiLoginer.Infrastructure.Paths;
using EasiLoginer.Infrastructure.Storage;

namespace EasiLoginer.Infrastructure.Configuration;

/// <summary>
/// <c>config.json</c> 的读写实现。
/// </summary>
/// <remarks>
/// <para>与上游 <c>Config.load/save</c> 的差异（均为文档要求的有意改进）：</para>
/// <list type="bullet">
///   <item>解析失败不抛 <c>RuntimeError</c> 崩溃，而是备份 <c>.bak</c> 后用默认值启动（§7.2）；</item>
///   <item>越界数值钳制到允许范围并记日志，而不是拒绝整个文件；</item>
///   <item>写入先落临时文件再 <see cref="File.Replace(string, string, string?)"/>，避免半写损坏。</item>
/// </list>
/// </remarks>
public sealed class ConfigStore
{
    /// <summary>读取被瞬时占用时的重试次数与间隔。</summary>
    private const int ReadAttempts = 4;
    private static readonly TimeSpan ReadRetryDelay = TimeSpan.FromMilliseconds(50);

    private readonly AppPaths _paths;
    private readonly ILogger<ConfigStore> _logger;

    /// <summary>创建配置存储。</summary>
    public ConfigStore(AppPaths paths, ILogger<ConfigStore> logger)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(logger);

        _paths = paths;
        _logger = logger;
    }

    /// <summary>读取配置；文件不存在时写入默认配置。</summary>
    public Config Load()
    {
        var configPath = _paths.ConfigFile;

        if (!File.Exists(configPath))
        {
            var defaults = new Config();
            Save(defaults);
            _logger.LogInformation("配置文件不存在，已写入默认配置：{Path}", configPath);
            return defaults;
        }

        // 读取失败大多是瞬时的（编辑器占用、杀软扫描、另一实例正在 File.Replace），
        // 先重试；仍然失败时只以默认值启动，绝不覆盖原文件，否则一次占用就清空用户全部设置。
        string? text = null;
        Exception? readFailure = null;
        for (var attempt = 1; attempt <= ReadAttempts && text is null; attempt++)
        {
            try
            {
                text = File.ReadAllText(configPath, Encoding.UTF8);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                readFailure = exception;
                if (attempt < ReadAttempts)
                {
                    Thread.Sleep(ReadRetryDelay);
                }
            }
        }

        if (text is null)
        {
            _logger.LogError(readFailure, "读取配置失败（已重试 {Attempts} 次），本次以默认值运行，原文件保持不变", ReadAttempts);
            return new Config();
        }

        try
        {
            var config = Deserialize(text);
            ConfigBounds.Normalize(config, _logger);
            return config;
        }
        catch (JsonException exception)
        {
            return RecoverFromFailure("配置解析失败", exception);
        }
        catch (FormatException exception)
        {
            return RecoverFromFailure("配置格式非法", exception);
        }
    }

    /// <summary>原子写入配置。</summary>
    public void Save(Config config)
    {
        ArgumentNullException.ThrowIfNull(config);

        Directory.CreateDirectory(_paths.DataDirectory);
        var json = JsonSerializer.Serialize(config, ConfigJson.Options);
        AtomicFile.WriteAllText(_paths.ConfigFile, json);
    }

    private static Config Deserialize(string text)
    {
        var node = JsonNode.Parse(text, nodeOptions: null, documentOptions: ConfigJson.DocumentOptions);

        if (node is not JsonObject root)
        {
            throw new JsonException("配置根节点必须是 JSON 对象。");
        }

        ConfigNormalizer.Normalize(root);

        return root.Deserialize<Config>(ConfigJson.Options)
               ?? throw new JsonException("配置反序列化结果为空。");
    }

    private Config RecoverFromFailure(string reason, Exception exception)
    {
        var backup = _paths.ConfigFile + ".bak";

        try
        {
            File.Copy(_paths.ConfigFile, backup, overwrite: true);
            _logger.LogError(exception, "{Reason}，已备份到 {Backup} 并使用默认配置启动", reason, backup);
        }
        catch (Exception backupException)
        {
            _logger.LogError(backupException, "{Reason}，且备份失败，将直接使用默认配置启动", reason);
        }

        var defaults = new Config();
        try
        {
            Save(defaults);
        }
        catch (Exception saveException) when (saveException is IOException or UnauthorizedAccessException)
        {
            // 恢复动作本身失败时仍要能启动：默认值只留在内存里
            _logger.LogError(saveException, "写入默认配置失败，本次以内存中的默认值运行");
        }

        return defaults;
    }
}

/// <summary>
/// 配置数值与枚举的越界修正。
/// </summary>
/// <remarks>
/// 上游用 pydantic 的 <c>ge/le</c> 直接拒绝整个文件（<c>RuntimeError</c>）；
/// 本项目按文档 §7.2 改为"钳制到合法区间 + 记日志"，保证用户不会因为一个越界值丢失全部配置。
/// </remarks>
public static class ConfigBounds
{
    /// <summary>就地修正一个已反序列化的配置对象。</summary>
    public static void Normalize(Config config, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(logger);

        // 手写/半损坏的 config.json 可能把某个段显式写成 null；先兜住，避免整份配置因为
        // 一次空值解引用而让应用起不来（文档 §7.2 要求「不因一个坏值丢失全部配置」）。
        config.Login ??= new LoginConfig();
        config.Login.EasiNote ??= new EasiNoteConfig();
        config.Login.Timeout ??= new TimeoutConfig();
        config.Login.Position ??= new PositionConfig();
        config.Warning ??= new WarningConfig();
        config.Banner ??= new BannerConfig();
        config.Banner.Style ??= new BannerStyleConfig();
        config.StatusOverlay ??= new StatusOverlayConfig();
        config.App ??= new AppConfig();
        config.Experimental ??= new ExperimentalConfig();
        config.Experimental.PrivacyMask ??= new PrivacyMaskConfig();
        config.Profile ??= new ProfileConfig();
        config.Update ??= new UpdateConfig();
        config.Debug ??= new DebugConfig();
        config.Internal ??= new InternalConfig();
        config.Statistics ??= new StatisticsConfig();

        var login = config.Login;
        login.Timeout.Terminate = Clamp(logger, "Login.Timeout.Terminate", login.Timeout.Terminate, 0, 60);
        login.Timeout.LaunchPollingTimeout = Clamp(logger, "Login.Timeout.LaunchPollingTimeout", login.Timeout.LaunchPollingTimeout, 0, 60);
        login.Timeout.LaunchPollingInterval = Clamp(logger, "Login.Timeout.LaunchPollingInterval", login.Timeout.LaunchPollingInterval, 0.1, 60);
        login.Timeout.AfterLaunch = Clamp(logger, "Login.Timeout.AfterLaunch", login.Timeout.AfterLaunch, 0, 60);
        login.Timeout.EnterLoginUI = Clamp(logger, "Login.Timeout.EnterLoginUI", login.Timeout.EnterLoginUI, 0, 60);
        login.Timeout.SwitchTab = Clamp(logger, "Login.Timeout.SwitchTab", login.Timeout.SwitchTab, 0, 60);

        if (!Enum.IsDefined(login.Method))
        {
            logger.LogWarning("配置项 Login.Method 取值非法（{Value}），已回退为默认值", (int)login.Method);
            login.Method = new LoginConfig().Method;
        }

        var warning = config.Warning;
        warning.Timeout = Clamp(logger, "Warning.Timeout", warning.Timeout, 5, 600);
        warning.MaxDelays = Clamp(logger, "Warning.MaxDelays", warning.MaxDelays, 0, 3);
        warning.DelayTime = Clamp(logger, "Warning.DelayTime", warning.DelayTime, 5, 300);

        var style = config.Banner.Style;
        style.Fps = Clamp(logger, "Banner.Style.Fps", style.Fps, 1, 120);
        style.TextSpeed = Clamp(logger, "Banner.Style.TextSpeed", style.TextSpeed, -8, 8);

        config.App.MaxRetries = Clamp(logger, "App.MaxRetries", config.App.MaxRetries, 0, 5);

        // 遗留的明文账号键必须迁移为脱敏哈希（§7.3），否则会随配置落盘泄露账号
        config.Statistics.NormalizeAccountKeys();

        if (!Enum.IsDefined(config.Update.Mode))
        {
            logger.LogWarning("配置项 Update.Mode 取值非法（{Value}），已回退为默认值", (int)config.Update.Mode);
            config.Update.Mode = UpdateMode.Never;
        }
    }

    private static int Clamp(ILogger logger, string path, int value, int min, int max)
    {
        var clamped = Math.Clamp(value, min, max);
        if (clamped != value)
        {
            logger.LogWarning(
                "配置项 {Path} 超出范围 [{Min}, {Max}]（实际 {Value}），已修正为 {Clamped}",
                path,
                min,
                max,
                value,
                clamped);
        }

        return clamped;
    }

    private static double Clamp(ILogger logger, string path, double value, double min, double max)
    {
        if (double.IsNaN(value))
        {
            logger.LogWarning("配置项 {Path} 不是有效数字，已回退为 {Min}", path, min);
            return min;
        }

        var clamped = Math.Clamp(value, min, max);
        if (Math.Abs(clamped - value) > double.Epsilon)
        {
            logger.LogWarning(
                "配置项 {Path} 超出范围 [{Min}, {Max}]（实际 {Value}），已修正为 {Clamped}",
                path,
                min.ToString(CultureInfo.InvariantCulture),
                max.ToString(CultureInfo.InvariantCulture),
                value.ToString(CultureInfo.InvariantCulture),
                clamped.ToString(CultureInfo.InvariantCulture));
        }

        return clamped;
    }
}
