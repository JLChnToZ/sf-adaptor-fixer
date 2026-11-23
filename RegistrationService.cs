using System.Management;
using System.Runtime.Versioning;
using System.Security.Principal;
using System.Text;

namespace SFAdaptorFixer;

[SupportedOSPlatform("windows")]
public sealed class RegistrationService(NicDeviceService devices, WindowsCommandRunner commandRunner) {
    public async Task RegisterAsync() {
        using var identity = WindowsIdentity.GetCurrent();
        var userName = identity.Name;
        var consoleUser = GetConsoleUserName();
        if (!string.IsNullOrWhiteSpace(consoleUser) &&
            !string.Equals(consoleUser, userName, StringComparison.OrdinalIgnoreCase)) {
            throw new InvalidOperationException(
                Localization.Format("ConsoleUserMismatch", userName, consoleUser));
        }

        if (!devices.HasValveDevice) {
            ConsoleOutput.WriteWarning(Localization.Get("RegisterDeviceMissing"));
        }

        _ = Directory.CreateDirectory(AppPaths.InstallDirectory);
        InstallExecutable();

        var definitions = new TaskDefinition[] {
            new(AppPaths.OnTaskName, "--on", Localization.Get("OnTaskDescription")),
            new(AppPaths.OffTaskName, "--off", Localization.Get("OffTaskDescription"))
        };

        var scheduler = new TaskSchedulerService(commandRunner);
        foreach (var definition in definitions) {
            await scheduler.RegisterAsync(definition.TaskName, definition.Arguments, definition.Description, userName);
            ConsoleOutput.WriteSuccess(Localization.Format("TaskRegistered", definition.TaskName));
        }

        Console.WriteLine();
        foreach (var definition in definitions)
            Console.WriteLine(await scheduler.QueryAsync(definition.TaskName));

        ConsoleOutput.WriteSuccess(Localization.Get("RegisterSuccess"));
        ConsoleOutput.WriteMuted(Localization.Get("TaskRunHint"));
        ConsoleOutput.WriteMuted(Localization.Format("LogPath", AppPaths.LogFile));
        ConsoleOutput.PauseForInput();
    }

    private static void InstallExecutable() {
        var source = Environment.ProcessPath
            ?? throw new InvalidOperationException(Localization.Get("ProcessPathMissing"));
        if (!string.Equals(
                Path.GetFullPath(source),
                Path.GetFullPath(AppPaths.InstalledExecutable),
                StringComparison.OrdinalIgnoreCase)) {
            File.Copy(source, AppPaths.InstalledExecutable, overwrite: true);
            ConsoleOutput.WriteSuccess(Localization.Format("ProgramInstalled", AppPaths.InstalledExecutable));
        } else {
            ConsoleOutput.WriteMuted(Localization.Format("ProgramAlreadyInstalled", AppPaths.InstalledExecutable));
        }
    }

    private static string? GetConsoleUserName() {
        const string query = "SELECT UserName FROM Win32_ComputerSystem";
        using var searcher = new ManagementObjectSearcher(@"root\cimv2", query);
        return searcher.Get()
            .Cast<ManagementObject>()
            .Select(item => item.Properties["UserName"]?.Value as string)
            .FirstOrDefault(item => !string.IsNullOrWhiteSpace(item));
    }

    private sealed record TaskDefinition(
        string TaskName,
        string Arguments,
        string Description);
}
