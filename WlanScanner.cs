using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace SFAdaptorFixer;

[SupportedOSPlatform("windows")]
public sealed partial class WlanScanner {
    public static int Count6GHz(string descriptionMatch, int waitMilliseconds = 4_000) {
        if (WlanOpenHandle(2, default, out _, out var handle) != 0) return -1;

        try {
            if (WlanEnumInterfaces(handle, default, out var interfaceList) != 0) return -1;

            Guid target = Guid.Empty;
            try {
                var count = Marshal.ReadInt32(interfaceList, 0);
                for (var i = 0; i < count; i++) {
                    var entry = nint.Add(interfaceList, 8 + (i * 532));
                    var description = Marshal.PtrToStringUni(nint.Add(entry, 16)) ?? "";
                    if (description.Contains(descriptionMatch, StringComparison.OrdinalIgnoreCase))
                        target = Marshal.PtrToStructure<Guid>(entry);
                }
            } finally {
                WlanFreeMemory(interfaceList);
            }

            if (target == Guid.Empty) return -1;

            _ = WlanScan(handle, ref target, default, default, default);
            Thread.Sleep(waitMilliseconds);

            if (WlanGetNetworkBssList(handle, ref target, default, 3, false, default, out var bssList) != 0)
                return 0;

            try {
                var entryCount = Marshal.ReadInt32(bssList, 4);
                var count6GHz = 0;
                for (var i = 0; i < entryCount; i++) {
                    var frequencyKHz = Marshal.ReadInt32(bssList, 8 + (i * 360) + 92);
                    if (FrequencyClassifier.Is6GHz(frequencyKHz))
                        count6GHz++;
                }
                return count6GHz;
            } finally {
                WlanFreeMemory(bssList);
            }
        } finally {
            _ = WlanCloseHandle(handle, default);
        }
    }

    [LibraryImport("wlanapi.dll")]
    private static partial int WlanOpenHandle(uint clientVersion, nint reserved, out uint negotiatedVersion, out nint clientHandle);

    [LibraryImport("wlanapi.dll")]
    private static partial int WlanCloseHandle(nint clientHandle, nint reserved);

    [LibraryImport("wlanapi.dll")]
    private static partial int WlanEnumInterfaces(nint clientHandle, nint reserved, out nint interfaceList);

    [LibraryImport("wlanapi.dll")]
    private static partial int WlanScan(nint clientHandle, ref Guid interfaceGuid, nint dot11Ssid, nint vendorSpecificIe, nint reserved);

    [LibraryImport("wlanapi.dll")]
    private static partial int WlanGetNetworkBssList(
        nint clientHandle,
        ref Guid interfaceGuid,
        nint dot11Ssid,
        int bssType,
        [MarshalAs(UnmanagedType.Bool)] bool securityEnabled,
        nint reserved,
        out nint bssList);

    [LibraryImport("wlanapi.dll")]
    private static partial void WlanFreeMemory(nint memory);
}
