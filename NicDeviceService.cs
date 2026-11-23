using System.Management;
using System.Runtime.Versioning;

namespace SFAdaptorFixer;

[SupportedOSPlatform("windows")]
public sealed class NicDeviceService(WindowsCommandRunner commandRunner) {
    public const string ValveVidPidPrefix = @"USB\VID_28DE&PID_2432\";

    public bool HasValveDevice => FindDevice() is not null;

    public static NicDevice? FindDevice() {
        var devices = Query("SELECT PNPDeviceID, Present, Status, ConfigManagerErrorCode FROM Win32_PnPEntity")
            .Select(ToNicDevice)
            .Where(device => device is not null)
            .Cast<NicDevice>()
            .Where(device => device.InstanceId.StartsWith(ValveVidPidPrefix, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(device => device.Present)
            .ThenBy(device => device.InstanceId, StringComparer.OrdinalIgnoreCase);

        return devices.FirstOrDefault();
    }

    public static NetworkAdapter? FindAdapter(string? instanceId) {
        if (string.IsNullOrWhiteSpace(instanceId)) {
            return null;
        }

        foreach (var adapter in Query("SELECT PNPDeviceID, NetConnectionID, Name FROM Win32_NetworkAdapter")) {
            if (!string.Equals(Property<string>(adapter, "PNPDeviceID"), instanceId, StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            var name = Property<string>(adapter, "NetConnectionID");
            var description = Property<string>(adapter, "Name") ?? "8832CU";
            if (!string.IsNullOrWhiteSpace(name)) {
                return new NetworkAdapter(name, description);
            }
        }

        return null;
    }

    public static NicState GetState(string? instanceId) {
        if (string.IsNullOrWhiteSpace(instanceId)) {
            return new NicState(NicStateKind.NotFound, "NotFound");
        }

        var item = Query("SELECT PNPDeviceID, Present, Status, ConfigManagerErrorCode FROM Win32_PnPEntity")
            .FirstOrDefault(candidate =>
                string.Equals(
                    Property<string>(candidate, "PNPDeviceID"),
                    instanceId,
                    StringComparison.OrdinalIgnoreCase));

        if (item is null) {
            return new NicState(NicStateKind.NotFound, "NotFound");
        }

        return ToNicDevice(item)?.State ?? new NicState(NicStateKind.NotFound, "NotFound");
    }

    public async Task SetInterfaceEnabledAsync(string? instanceId, bool enabled) {
        var adapter = FindAdapter(instanceId);
        if (adapter is null) {
            return;
        }

        var result = await commandRunner.RunAsync(
            Path.Combine(Environment.SystemDirectory, "netsh.exe"),
            ["interface", "set", "interface", adapter.Name, enabled ? "enable" : "disable"],
            TimeSpan.FromSeconds(30));

        if (!result.Succeeded) {
            ConsoleOutput.WriteMuted(Localization.Format(
                "CommandExitCode",
                "netsh",
                enabled ? "enable" : "disable",
                result.ExitCode));
        }
    }

    public async Task SetDeviceEnabledAsync(string instanceId, bool enabled) {
        var result = await commandRunner.RunAsync(
            Path.Combine(Environment.SystemDirectory, "pnputil.exe"),
            [enabled ? "/enable-device" : "/disable-device", instanceId],
            TimeSpan.FromSeconds(45));

        if (!result.Succeeded) {
            ConsoleOutput.WriteMuted(Localization.Format(
                "CommandExitCode",
                "pnputil",
                enabled ? "enable" : "disable",
                result.ExitCode));
        }
    }

    public async Task RescanDevicesAsync() {
        var result = await commandRunner.RunAsync(
            Path.Combine(Environment.SystemDirectory, "pnputil.exe"),
            ["/scan-devices"],
            TimeSpan.FromSeconds(45));

        if (!result.Succeeded) {
            ConsoleOutput.WriteMuted(Localization.Format(
                "CommandExitCode",
                "pnputil",
                "scan-devices",
                result.ExitCode));
        }
    }

    private static NicDevice? ToNicDevice(ManagementObject item) {
        var instanceId = Property<string>(item, "PNPDeviceID");
        if (string.IsNullOrWhiteSpace(instanceId)) {
            return null;
        }

        return new NicDevice(
            instanceId,
            Property<string>(item, "Status"),
            (int)(Property<uint?>(item, "ConfigManagerErrorCode") ?? 0),
            Property<bool?>(item, "Present") ?? true);
    }

    private static List<ManagementObject> Query(string query) {
        using var searcher = new ManagementObjectSearcher(@"root\cimv2", query);
        return searcher.Get().Cast<ManagementObject>().ToList();
    }

    private static T? Property<T>(ManagementBaseObject item, string name) {
        var property = item.Properties[name];
        if (property?.Value is null) {
            return default;
        }

        return (T?)property.Value;
    }
}
