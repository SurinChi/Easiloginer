namespace EasiLoginer.Integrations.EasiNote.Environment;

/// <summary>在线的 HID 设备接口路径枚举端口（等价于 <c>Cvte.Mcu.HidDevices.GetDevices</c>）。</summary>
public interface IHidDeviceEnumerator
{
    /// <summary>枚举当前在线的 HID 设备接口路径。</summary>
    IReadOnlyList<string> EnumerateDevicePaths();
}
