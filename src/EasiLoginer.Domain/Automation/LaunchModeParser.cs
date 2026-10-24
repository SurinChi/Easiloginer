namespace EasiLoginer.Domain.Automation;

/// <summary>解析希沃启动参数中的显式启动模式（<c>-m</c> / <c>--mode</c>）。</summary>
/// <remarks>
/// 纯字符串解析，上游位于 <c>integrations/easinote/env.py::parse_start_mode</c>；
/// 因为登录策略与集成层都要用，下沉到 Domain 以避免重复实现。
/// </remarks>
public static class LaunchModeParser
{
    /// <summary>启动模式参数名。</summary>
    public static readonly IReadOnlySet<string> ModeOptions = new HashSet<string>(StringComparer.Ordinal)
    {
        "-m",
        "--mode",
    };

    /// <summary>解析显式启动模式（小写）；未指定返回 <see langword="null"/>。</summary>
    public static string? Parse(string args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var tokens = args.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        for (var index = 0; index < tokens.Length; index++)
        {
            if (!ModeOptions.Contains(tokens[index].ToLowerInvariant()))
            {
                continue;
            }

            return index + 1 < tokens.Length ? tokens[index + 1].ToLowerInvariant() : null;
        }

        return null;
    }
}
