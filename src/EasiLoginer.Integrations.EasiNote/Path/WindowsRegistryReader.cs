using Microsoft.Win32;

namespace EasiLoginer.Integrations.EasiNote.Path;

/// <summary>基于 <see cref="Registry"/> 的实现。</summary>
/// <remarks>
/// 上游 <c>winreg.OpenKey</c> 在 64 位进程下访问 64 位视图，路径里显式带
/// <c>WOW6432Node</c>，这里保持同样语义：使用默认视图，不去改 <see cref="RegistryView"/>。
/// </remarks>
public sealed class WindowsRegistryReader : IRegistryReader
{
    /// <inheritdoc />
    public string? ReadLocalMachineString(string subKey, string valueName)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(subKey, writable: false);
            return key?.GetValue(valueName) as string;
        }
        catch (Exception exception) when (exception is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return null;
        }
    }
}
