using System.Runtime.Versioning;

namespace SFAdaptorFixer;

[SupportedOSPlatform("windows")]
public sealed class NicController(TextLogger logger) {

    public async Task<int> InspectStatusAsync() {
        Console.Title = Localization.Get("StatusTitle");
        ConsoleOutput.HorizontalRule();

        var device = NicDeviceService.FindDevice();
        var state = device is null ? new NicState(NicStateKind.NotFound, "NotFound") : device.State;
        logger.Write($"STATUS -> {state.DisplayName}");

        Console.WriteLine(Localization.Format("StatusDevice", NicDeviceService.ValveVidPidPrefix));
        var label = state.Kind switch {
            NicStateKind.Enabled => Localization.Get("StatusEnabled"),
            NicStateKind.Disabled => Localization.Get("StatusDisabled"),
            NicStateKind.NotPresent => Localization.Get("StatusNotPresent"),
            NicStateKind.NotFound => Localization.Get("StatusNotFound"),
            _ => Localization.Format("StatusUnexpected", state.DisplayName),
        };

        Console.WriteLine(Localization.Format("StatusResult", label));
        ConsoleOutput.HorizontalRule();
        ConsoleOutput.PauseForInput();
        return state.Kind is NicStateKind.Enabled or NicStateKind.Disabled or NicStateKind.NotPresent or NicStateKind.NotFound ? 0 : 1;
    }

    public async Task<int> TurnOnAsync() {
        Console.Title = Localization.Get("OnTitle");
        ConsoleOutput.HorizontalRule();

        var device = NicDeviceService.FindDevice();
        var state = device is null ? new NicState(NicStateKind.NotFound, "NotFound") : device.State;
        var bssCount = -1;
        const int maxTry = 3;

        for (var attempt = 1; attempt <= maxTry; attempt++) {
            Console.WriteLine(Localization.Format("ResetAttempt", attempt, maxTry));
            await ResetDeviceAsync(device);
            state = NicDeviceService.GetState(device?.InstanceId);
            if (state.Kind != NicStateKind.Enabled) break;

            Console.WriteLine(Localization.Get("Scanning6GHz"));
            var adapter = NicDeviceService.FindAdapter(device?.InstanceId);
            var description = adapter?.InterfaceDescription ?? NicDeviceService.DefaultAdapterDescription;
            bssCount = -1;
            for (var scan = 0; scan < 4 && bssCount <= 0; scan++)
                bssCount = WlanScanner.Count6GHz(description);

            logger.Write($"ON   -> try {attempt}  {state.DisplayName}  6GHz BSS={bssCount}");
            if (bssCount > 0) break;
        }

        WriteResultForTurnOn(state, bssCount, maxTry);
        await ConsoleOutput.DelayAsync(state.Kind == NicStateKind.Enabled && bssCount > 0 ? TimeSpan.FromSeconds(3) : TimeSpan.FromSeconds(15));

        return state.Kind == NicStateKind.Enabled && bssCount > 0 ? 0 : 1;
    }

    public async Task<int> TurnOffAsync() {
        Console.Title = Localization.Get("OffTitle");
        ConsoleOutput.HorizontalRule();
        Console.WriteLine(Localization.Get("OffStep1"));

        var device = NicDeviceService.FindDevice();
        await NicDeviceService.SetInterfaceEnabledAsync(device?.InstanceId, false);

        Console.WriteLine(Localization.Get("OffStep2"));
        if (device is not null) await NicDeviceService.SetDeviceEnabledAsync(device.InstanceId, false);

        await Task.Delay(TimeSpan.FromSeconds(1));
        var state = NicDeviceService.GetState(device?.InstanceId);
        logger.Write($"OFF  -> {state.DisplayName}");

        Console.WriteLine();
        ConsoleOutput.HorizontalRule();
        switch (state.Kind) {
            case NicStateKind.NotFound:
                ConsoleOutput.WriteWarning(Localization.Get("OffDeviceMissing"));
                break;
            case NicStateKind.Disabled:
            case NicStateKind.NotPresent:
                ConsoleOutput.WriteSuccess(Localization.Get("OffSuccess"));
                ConsoleOutput.WriteSuccess(Localization.Get("OffPowerSafe"));
                break;
            default:
                ConsoleOutput.WriteError(Localization.Format("OffUnconfirmed", state.DisplayName));
                break;
        }

        ConsoleOutput.HorizontalRule();
        await Task.Delay(TimeSpan.FromSeconds(5));
        return state.Kind is NicStateKind.NotFound or NicStateKind.Disabled or NicStateKind.NotPresent ? 0 : 1;
    }

    private static async Task ResetDeviceAsync(NicDevice? device) {
        if (device is null) return;

        if (NicDeviceService.GetState(device.InstanceId).Kind == NicStateKind.Enabled) {
            await NicDeviceService.SetInterfaceEnabledAsync(device.InstanceId, false);
            await NicDeviceService.SetDeviceEnabledAsync(device.InstanceId, false);
            await Task.Delay(TimeSpan.FromSeconds(2));
        }

        await NicDeviceService.SetDeviceEnabledAsync(device.InstanceId, true);
        await NicDeviceService.RescanDevicesAsync();
        await Task.Delay(TimeSpan.FromSeconds(2));
        await NicDeviceService.SetInterfaceEnabledAsync(device.InstanceId, true);
    }

    private void WriteResultForTurnOn(NicState state, int bssCount, int maxTry) {
        Console.WriteLine();
        ConsoleOutput.HorizontalRule();
        if (state.Kind == NicStateKind.Enabled && bssCount > 0) {
            ConsoleOutput.WriteSuccess(Localization.Get("OnSuccess"));
        } else if (state.Kind == NicStateKind.Enabled) {
            logger.Write($"ON   -> FAIL no 6GHz after {maxTry} tries");
            ConsoleOutput.WriteError(Localization.Format("OnNo6GHz", maxTry));
            ConsoleOutput.WriteWarning(Localization.Get("OnNo6GHzTip1"));
            ConsoleOutput.WriteWarning(Localization.Get("OnNo6GHzTip2"));
        } else if (state.Kind == NicStateKind.NotPresent) {
            logger.Write($"ON   -> {state.DisplayName}");
            ConsoleOutput.WriteWarning(Localization.Get("OnNotPresent"));
            ConsoleOutput.WriteWarning(Localization.Get("OnWillRecover"));
        } else if (state.Kind == NicStateKind.NotFound) {
            logger.Write($"ON   -> {state.DisplayName}");
            ConsoleOutput.WriteError(Localization.Format("OnDeviceMissing", NicDeviceService.ValveVidPidPrefix));
        } else {
            logger.Write($"ON   -> {state.DisplayName}");
            ConsoleOutput.WriteError(Localization.Format("OnStateUnexpected", state.DisplayName));
        }

        ConsoleOutput.HorizontalRule();
    }
}
