namespace SFAdaptorFixer;

public enum CommandMode : byte {
    Interactive,
    Register,
    Unregister,
    TurnOn,
    TurnOff,
}

public sealed record ApplicationArguments(CommandMode Mode);

public static class ApplicationArgumentParser {
    public static ApplicationArguments? Parse(string[] args) => args switch {
        [] => new ApplicationArguments(CommandMode.Interactive),
        ["register"] => new ApplicationArguments(CommandMode.Register),
        ["unregister"] => new ApplicationArguments(CommandMode.Unregister),
        ["--on"] => new ApplicationArguments(CommandMode.TurnOn),
        ["--off"] => new ApplicationArguments(CommandMode.TurnOff),
        _ => null
    };

    public static string[]? ParseInteractiveSelection(string? selection) => selection?.Trim() switch {
        "1" => ["--on"],
        "2" => ["--off"],
        "3" => ["register"],
        "4" => ["unregister"],
        "0" => [],
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
