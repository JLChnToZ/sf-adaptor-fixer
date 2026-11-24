using System.Diagnostics.CodeAnalysis;
using System.Management;
using System.Runtime.Versioning;

namespace SFAdaptorFixer;

[SupportedOSPlatform("windows")]
public static class NicDeviceService {
    public const string ValveVidPidPrefix = @"USB\VID_28DE&PID_2432\";
    public const string DefaultAdapterDescription = "8832CU";

    public static bool HasValveDevice => FindDevice() is not null;

    public static NicDevice? FindDevice() => Query("SELECT PNPDeviceID, Present, Status, ConfigManagerErrorCode FROM Win32_PnPEntity")
        .Select(ToNicDevice)
        .Where(static device => device is not null)
        .Cast<NicDevice>()
        .Where(static device => device.InstanceId.StartsWith(ValveVidPidPrefix, StringComparison.OrdinalIgnoreCase))
        .OrderByDescending(static device => device.Present)
        .ThenBy(static device => device.InstanceId, StringComparer.OrdinalIgnoreCase).FirstOrDefault();

    public static NetworkAdapter? FindAdapter(string? instanceId) {
        if (string.IsNullOrWhiteSpace(instanceId)) return null;
        foreach (var adapter in Query("SELECT PNPDeviceID, NetConnectionID, Name FROM Win32_NetworkAdapter")) {
            if (!string.Equals(Property<string>(adapter, "PNPDeviceID"), instanceId, StringComparison.OrdinalIgnoreCase))
                continue;
            var name = Property<string>(adapter, "NetConnectionID");
            if (!string.IsNullOrWhiteSpace(name)) return new NetworkAdapter(name, Property(adapter, "Name", DefaultAdapterDescription));
        }
        return null;
    }

    public static NicState GetState(string? instanceId) => !string.IsNullOrWhiteSpace(instanceId) &&
        Query("SELECT PNPDeviceID, Present, Status, ConfigManagerErrorCode FROM Win32_PnPEntity").FirstOrDefault(
            candidate => string.Equals(Property<string>(candidate, "PNPDeviceID"), instanceId, StringComparison.OrdinalIgnoreCase)
        ) is { } item && ToNicDevice(item)?.State is { } state ? state :
        new NicState(NicStateKind.NotFound, "NotFound");

    public static async Task SetInterfaceEnabledAsync(string? instanceId, bool enabled) {
        var adapter = FindAdapter(instanceId);
        if (adapter is null) return;
        var result = await WindowsCommandRunner.RunAsync(
            Path.Combine(Environment.SystemDirectory, "netsh.exe"),
            ["interface", "set", "interface", adapter.Name, enabled ? "enable" : "disable"],
            TimeSpan.FromSeconds(30)
        );
        if (!result.Succeeded)
            ConsoleOutput.WriteMuted(Localization.Format("CommandExitCode", "netsh", enabled ? "enable" : "disable", result.ExitCode));
    }

    public static async Task SetDeviceEnabledAsync(string instanceId, bool enabled) {
        var result = await WindowsCommandRunner.RunAsync(
            Path.Combine(Environment.SystemDirectory, "pnputil.exe"),
            [enabled ? "/enable-device" : "/disable-device", instanceId],
            TimeSpan.FromSeconds(45)
        );
        if (!result.Succeeded)
            ConsoleOutput.WriteMuted(Localization.Format("CommandExitCode", "pnputil", enabled ? "enable" : "disable", result.ExitCode));
    }

    public static async Task RescanDevicesAsync() {
        var result = await WindowsCommandRunner.RunAsync(
            Path.Combine(Environment.SystemDirectory, "pnputil.exe"),
            ["/scan-devices"],
            TimeSpan.FromSeconds(45)
        );
        if (!result.Succeeded)
            ConsoleOutput.WriteMuted(Localization.Format("CommandExitCode", "pnputil", "scan-devices", result.ExitCode));
    }

    private static NicDevice? ToNicDevice(ManagementObject item) {
        var instanceId = Property<string>(item, "PNPDeviceID");
        return string.IsNullOrWhiteSpace(instanceId) ? null :
            new NicDevice(instanceId, Property<string>(item, "Status"), (int)Property(item, "ConfigManagerErrorCode", 0u), Property(item, "Present", true));
    }

    private static IEnumerable<ManagementObject> Query(string query) {
        using var searcher = new ManagementObjectSearcher(@"root\cimv2", query);
        return [..searcher.Get().Cast<ManagementObject>()];
    }

    [return: NotNullIfNotNull(nameof(defaultValue))]
    private static T? Property<T>(ManagementBaseObject item, string name, T? defaultValue = default) =>
        item.Properties[name]?.Value is T result ? result : defaultValue;
}
