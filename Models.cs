namespace SFAdaptorFixer;

public enum CommandMode : byte {
    Interactive,
    Register,
    Unregister,
    TurnOn,
    TurnOff,
    Status,
}

public sealed record ApplicationArguments(CommandMode Mode);

public static class ApplicationArgumentParser {
    public static ApplicationArguments? Parse(ReadOnlySpan<string> args) => args switch {
        [] => new ApplicationArguments(CommandMode.Interactive),
        ["register"] => new ApplicationArguments(CommandMode.Register),
        ["unregister"] => new ApplicationArguments(CommandMode.Unregister),
        ["on"] => new ApplicationArguments(CommandMode.TurnOn),
        ["off"] => new ApplicationArguments(CommandMode.TurnOff),
        ["status"] => new ApplicationArguments(CommandMode.Status),
        _ => null
    };

    public static CommandMode? ParseInteractiveSelection(ConsoleKey selection) => selection switch {
        ConsoleKey.D1 or ConsoleKey.NumPad1 => CommandMode.TurnOn,
        ConsoleKey.D2 or ConsoleKey.NumPad2 => CommandMode.TurnOff,
        ConsoleKey.D3 or ConsoleKey.NumPad3 => CommandMode.Status,
        ConsoleKey.D4 or ConsoleKey.NumPad4 => CommandMode.Register,
        ConsoleKey.D5 or ConsoleKey.NumPad5 => CommandMode.Unregister,
        ConsoleKey.D0 or ConsoleKey.NumPad0 => CommandMode.Interactive,
        _ => null
    };
}

public enum NicStateKind : byte {
    NotFound,
    NotPresent,
    Enabled,
    Disabled,
    Unexpected,
}

public sealed record NicDevice(string InstanceId, string? Status, int ConfigManagerErrorCode, bool Present) {
    public NicState State => StateClassifier.Classify(Status, ConfigManagerErrorCode, Present);
}

public sealed record NetworkAdapter(string Name, string InterfaceDescription);

public sealed record CommandResult(int ExitCode, string StandardOutput, string StandardError) {
    public bool Succeeded => ExitCode == 0;
}

public static class StateClassifier {
    public static NicState Classify(string? status, int configManagerErrorCode, bool present) {
        if (!present)
            return new NicState(NicStateKind.NotPresent, "NotPresent");

        if (configManagerErrorCode == 22)
            return new NicState(NicStateKind.Disabled, "Disabled");
    
        if (status?.Equals("OK", StringComparison.OrdinalIgnoreCase) == true &&
            configManagerErrorCode == 0)
            return new NicState(NicStateKind.Enabled, "Enabled");

        if (status?.Equals("Unknown", StringComparison.OrdinalIgnoreCase) == true)
            return new NicState(NicStateKind.NotPresent, "NotPresent");

        var description = string.IsNullOrWhiteSpace(status) ? "Unknown" : status;
        if (configManagerErrorCode != 0)
            description += $"/{configManagerErrorCode}";

        return new NicState(NicStateKind.Unexpected, description);
    }
}

public sealed record NicState(NicStateKind Kind, string DisplayName);

public static class FrequencyClassifier {
    public static bool Is6GHz(int frequencyKHz) => frequencyKHz is >= 5_925_000 and <= 7_125_000;
}
