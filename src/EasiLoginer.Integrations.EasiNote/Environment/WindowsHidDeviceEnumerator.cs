using System.Runtime.InteropServices;

namespace EasiLoginer.Integrations.EasiNote.Environment;

/// <summary>
/// 用 SetupAPI 枚举 HID 设备，对应上游 <c>env.enum_hid_devices</c>。
/// </summary>
/// <remarks>
/// 结构体布局与调用顺序逐项照抄上游 ctypes 版本：
/// <c>SP_DEVICE_INTERFACE_DETAIL_DATA</c> 的 <c>cbSize</c> 是固定值（x64 为 8），
/// 不能用 <c>sizeof</c>；设备路径是 <c>cbSize</c> 之后的 WCHAR 数组。
/// </remarks>
public sealed class WindowsHidDeviceEnumerator : IHidDeviceEnumerator
{
    private const uint DigcfPresent = 0x02;
    private const uint DigcfDeviceInterface = 0x10;

    /// <inheritdoc />
    public IReadOnlyList<string> EnumerateDevicePaths()
    {
        var results = new List<string>();

        try
        {
            HidD_GetHidGuid(out var hidGuid);

            var deviceInfoSet = SetupDiGetClassDevsW(ref hidGuid, null, IntPtr.Zero, DigcfPresent | DigcfDeviceInterface);
            if (deviceInfoSet == IntPtr.Zero || deviceInfoSet == new IntPtr(-1))
            {
                return results;
            }

            try
            {
                var interfaceData = new SpDeviceInterfaceData
                {
                    Size = (uint)Marshal.SizeOf<SpDeviceInterfaceData>(),
                };

                for (uint index = 0;
                     SetupDiEnumDeviceInterfaces(deviceInfoSet, IntPtr.Zero, ref hidGuid, index, ref interfaceData);
                     index++)
                {
                    // 第一次调用故意传 0 大小：按约定会返回 false 并回填所需大小，
                    // 因此不能把返回值当错误（上游同样只看 requiredSize）。
                    _ = SetupDiGetDeviceInterfaceDetailW(
                        deviceInfoSet,
                        ref interfaceData,
                        IntPtr.Zero,
                        0,
                        out var requiredSize,
                        IntPtr.Zero);

                    if (requiredSize == 0)
                    {
                        continue;
                    }

                    var buffer = Marshal.AllocHGlobal((int)requiredSize);
                    try
                    {
                        // cbSize 固定为指针大小：x64 = 8，x86 = 6
                        Marshal.WriteInt32(buffer, IntPtr.Size == 8 ? 8 : 6);

                        if (SetupDiGetDeviceInterfaceDetailW(deviceInfoSet, ref interfaceData, buffer, requiredSize, out _, IntPtr.Zero))
                        {
                            var path = Marshal.PtrToStringUni(buffer + sizeof(uint));
                            if (!string.IsNullOrEmpty(path))
                            {
                                results.Add(path);
                            }
                        }
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(buffer);
                    }
                }
            }
            finally
            {
                _ = SetupDiDestroyDeviceInfoList(deviceInfoSet);
            }
        }
        catch (DllNotFoundException)
        {
            return results;
        }
        catch (EntryPointNotFoundException)
        {
            return results;
        }

        return results;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SpDeviceInterfaceData
    {
        public uint Size;
        public Guid InterfaceClassGuid;
        public uint Flags;
        public IntPtr Reserved;
    }

    [DllImport("hid.dll", SetLastError = true)]
    private static extern void HidD_GetHidGuid(out Guid hidGuid);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SetupDiGetClassDevsW(ref Guid classGuid, string? enumerator, IntPtr hwndParent, uint flags);

    [DllImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiEnumDeviceInterfaces(
        IntPtr deviceInfoSet,
        IntPtr deviceInfoData,
        ref Guid interfaceClassGuid,
        uint memberIndex,
        ref SpDeviceInterfaceData deviceInterfaceData);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiGetDeviceInterfaceDetailW(
        IntPtr deviceInfoSet,
        ref SpDeviceInterfaceData deviceInterfaceData,
        IntPtr deviceInterfaceDetailData,
        uint deviceInterfaceDetailDataSize,
        out uint requiredSize,
        IntPtr deviceInfoData);

    [DllImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);
}
