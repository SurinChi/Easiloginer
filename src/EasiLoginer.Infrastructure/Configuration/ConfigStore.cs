using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using EasiLoginer.Domain.Configuration;
using EasiLoginer.Infrastructure.Paths;

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

        string text;
        try
        {
            text = File.ReadAllText(configPath, Encoding.UTF8);
        }
        catch (IOException exception)
        {
            return RecoverFromFailure("读取配置失败", exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            return RecoverFromFailure("读取配置被拒绝", exception);
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

        var temporary = _paths.ConfigFile + ".tmp";
        File.WriteAllText(temporary, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        if (File.Exists(_paths.ConfigFile))
        {
            File.Replace(temporary, _paths.ConfigFile, destinationBackupFileName: null);
        }
        else
        {
            File.Move(temporary, _paths.ConfigFile);
        }
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
        Save(defaults);
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
            login.Method = LoginMethod.Fixed;
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
