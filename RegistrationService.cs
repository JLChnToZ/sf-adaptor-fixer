using System.Management;
using System.Runtime.Versioning;
using System.Security.Principal;

namespace SFAdaptorFixer;

[SupportedOSPlatform("windows")]
public static class RegistrationService {
    public static async Task RegisterAsync() {
        using var identity = WindowsIdentity.GetCurrent();
        var userName = identity.Name;
        var consoleUser = GetConsoleUserName();
        if (!string.IsNullOrWhiteSpace(consoleUser) && !string.Equals(consoleUser, userName, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(Localization.Format("ConsoleUserMismatch", userName, consoleUser));

        if (!NicDeviceService.HasValveDevice)
            ConsoleOutput.WriteWarning(Localization.Get("RegisterDeviceMissing"));

        _ = Directory.CreateDirectory(AppPaths.InstallDirectory);
        InstallExecutable();

        var definitions = new TaskDefinition[] {
            new(AppPaths.OnTaskName, "--on", Localization.Get("OnTaskDescription")),
            new(AppPaths.OffTaskName, "--off", Localization.Get("OffTaskDescription"))
        };

        foreach (var definition in definitions) {
            await TaskSchedulerService.RegisterAsync(definition.TaskName, definition.Arguments, definition.Description, userName);
            ConsoleOutput.WriteSuccess(Localization.Format("TaskRegistered", definition.TaskName));
        }

        Console.WriteLine();
        foreach (var definition in definitions)
            Console.WriteLine(await TaskSchedulerService.QueryAsync(definition.TaskName));

        ConsoleOutput.WriteSuccess(Localization.Get("RegisterSuccess"));
        ConsoleOutput.WriteMuted(Localization.Get("TaskRunHint"));
        ConsoleOutput.WriteMuted(Localization.Format("LogPath", AppPaths.LogFile));
        ConsoleOutput.PauseForInput();
    }

    private static void InstallExecutable() {
        var source = Environment.ProcessPath ?? throw new InvalidOperationException(Localization.Get("ProcessPathMissing"));
        var installed = AppPaths.InstalledExecutable;
        if (string.Equals(Path.GetFullPath(source), Path.GetFullPath(installed), StringComparison.OrdinalIgnoreCase)) {
            ConsoleOutput.WriteMuted(Localization.Format("ProgramAlreadyInstalled", installed));
            return;
        }
        File.Copy(source, installed, overwrite: true);
        ConsoleOutput.WriteSuccess(Localization.Format("ProgramInstalled", installed));
    }

    private static string? GetConsoleUserName() {
        const string query = "SELECT UserName FROM Win32_ComputerSystem";
        using var searcher = new ManagementObjectSearcher(@"root\cimv2", query);
        return searcher.Get()
            .Cast<ManagementObject>()
            .Select(static item => item.Properties["UserName"]?.Value as string)
            .FirstOrDefault(static item => !string.IsNullOrWhiteSpace(item));
    }

    private sealed record TaskDefinition(
        string TaskName,
        string Arguments,
        string Description);
}
