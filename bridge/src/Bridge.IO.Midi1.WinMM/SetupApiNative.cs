using System.Runtime.InteropServices;

namespace Bridge.IO.Midi1.WinMM;

internal static class SetupApiNative
{
    public const uint DIGCF_PRESENT = 0x00000002;
    public const uint DIGCF_ALLCLASSES = 0x00000004;

    public const uint SPDRP_DEVICEDESC = 0x00000000;
    public const uint SPDRP_HARDWAREID = 0x00000001;
    public const uint SPDRP_FRIENDLYNAME = 0x0000000C;

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern nint SetupDiGetClassDevsW(
        nint classGuid,
        string? enumerator,
        nint hwndParent,
        uint flags);

    [DllImport("setupapi.dll", SetLastError = true)]
    public static extern bool SetupDiEnumDeviceInfo(
        nint deviceInfoSet,
        uint memberIndex,
        ref SpDevinfoData deviceInfoData);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern bool SetupDiGetDeviceRegistryPropertyW(
        nint deviceInfoSet,
        ref SpDevinfoData deviceInfoData,
        uint property,
        out uint propertyRegDataType,
        byte[] propertyBuffer,
        uint propertyBufferSize,
        out uint requiredSize);

    [DllImport("setupapi.dll", SetLastError = true)]
    public static extern bool SetupDiDestroyDeviceInfoList(nint deviceInfoSet);

    [StructLayout(LayoutKind.Sequential)]
    public struct SpDevinfoData
    {
        public uint cbSize;
        public Guid ClassGuid;
        public uint DevInst;
        public nint Reserved;
    }
}

