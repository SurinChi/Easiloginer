using System.Reflection;
using EasiLoginer.Domain.Versioning;

namespace EasiLoginer.Presentation;

/// <summary>
/// 当前应用版本（来自程序集的 <see cref="AssemblyInformationalVersionAttribute"/>）。
/// </summary>
/// <remarks>
/// 关于页、更新页与公告过滤共用同一份取值，避免三处各自解析出不同的版本字符串。
/// </remarks>
public static class AppVersion
{
    /// <summary>版本文本（不含 <c>+&lt;commit&gt;</c> 后缀）。</summary>
    public static string Text
    {
        get
        {
            var assembly = typeof(AppVersion).Assembly;
            var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

            if (!string.IsNullOrWhiteSpace(informational))
            {
                return informational.Split('+')[0];
            }

            var version = assembly.GetName().Version;
            return version is null ? "0.0.1" : $"{version.Major}.{version.Minor}.{version.Build}";
        }
    }

    /// <summary>解析后的版本；版本号异常时返回 <see langword="null"/>。</summary>
    public static Pep440Version? TryGet()
        => Pep440Version.TryParse(Text, out var version) ? version : null;
}
