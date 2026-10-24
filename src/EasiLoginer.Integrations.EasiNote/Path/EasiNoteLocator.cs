using EasiLoginer.Domain.Configuration;

namespace EasiLoginer.Integrations.EasiNote.Path;

/// <summary>路径来源，对应上游的 <c>registry</c> / <c>fallback</c> / <c>manual</c>。</summary>
public enum EasiNotePathSource
{
    /// <summary>从注册表取得。</summary>
    Registry,

    /// <summary>注册表读取失败，使用默认路径。</summary>
    Fallback,

    /// <summary>使用用户手动配置的路径。</summary>
    Manual,
}

/// <summary>解析结果。</summary>
/// <param name="Path">可执行文件绝对路径；文件不存在时为 <see langword="null"/>。</param>
/// <param name="Source">路径来源。</param>
public sealed record EasiNoteLocation(string? Path, EasiNotePathSource Source);

/// <summary>
/// 希沃白板启动器路径解析，对应上游 <c>integrations/easinote/path.py</c>。
/// </summary>
public sealed class EasiNoteLocator
{
    /// <summary>注册表子键。</summary>
    public const string RegistrySubKey = @"SOFTWARE\WOW6432Node\Seewo\EasiNote5";

    /// <summary>注册表值名。</summary>
    public const string RegistryValueName = "ExePath";

    /// <summary>注册表失败时的回退路径。</summary>
    public const string FallbackPath = @"C:\Program Files (x86)\Seewo\EasiNote5\swenlauncher\swenlauncher.exe";

    private readonly IRegistryReader _registry;

    /// <summary>创建解析器。</summary>
    public EasiNoteLocator(IRegistryReader registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        _registry = registry;
    }

    /// <summary>按配置解析希沃启动器路径。</summary>
    /// <remarks>
    /// 与上游一致：<c>AutoPath=false</c> 时直接用配置路径（不校验存在性之前先取绝对路径）；
    /// 返回的 <c>Path</c> 为 <see langword="null"/> 表示解析出的文件不存在。
    /// </remarks>
    public EasiNoteLocation Resolve(EasiNoteConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        string pathText;
        EasiNotePathSource source;

        if (config.AutoPath)
        {
            var registryValue = _registry.ReadLocalMachineString(RegistrySubKey, RegistryValueName);
            if (!string.IsNullOrWhiteSpace(registryValue))
            {
                pathText = registryValue;
                source = EasiNotePathSource.Registry;
            }
            else
            {
                pathText = FallbackPath;
                source = EasiNotePathSource.Fallback;
            }
        }
        else
        {
            pathText = config.Path;
            source = EasiNotePathSource.Manual;
        }

        string fullPath;
        try
        {
            fullPath = System.IO.Path.GetFullPath(pathText);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return new EasiNoteLocation(null, source);
        }

        var exists = File.Exists(fullPath) || Directory.Exists(fullPath);
        return new EasiNoteLocation(exists ? fullPath : null, source);
    }
}
