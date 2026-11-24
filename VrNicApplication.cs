using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security.Principal;

namespace SFAdaptorFixer;

[SupportedOSPlatform("windows")]
public static class VrNicApplication {
    public static async Task<int> RunAsync(string[] args) {
        if (!OperatingSystem.IsWindows()) {
            Console.Error.WriteLine(Localization.Get("WindowsOnly"));
            return 2;
        }

        if (args is ["--help"] or ["-h"] or ["/?"]) {
            PrintHelp();
            return 0;
        }

        var parsed = ApplicationArgumentParser.Parse(args);
        if (parsed is null) {
            Console.Error.WriteLine(Localization.Get("InvalidArguments"));
            PrintHelp();
            return 2;
        }

        if (parsed.Mode == CommandMode.Interactive) {
            if (Console.IsInputRedirected) {
                PrintHelp();
                return 2;
            }
            return await RunInteractiveAsync();
        }

        if (!IsAdministrator() && parsed.Mode != CommandMode.Status) {
            if (parsed.Mode is CommandMode.TurnOn or CommandMode.TurnOff) {
                try {
                    if (await ScheduledTaskDelegator.TryRunAsync(parsed.Mode)) {
                        ConsoleOutput.WriteMuted(Localization.Get("DelegatingToTask"));
                        return 0;
                    }
                } catch (InvalidOperationException) {
                } catch (TimeoutException) {
                }
            }

            return RelaunchAsAdministrator(args);
        }

        return await RunAsync(parsed.Mode);
    }

    public static async Task<int> RunAsync(CommandMode mode) {
        try {
            var runner = new WindowsCommandRunner();
            var logger = new TextLogger(AppPaths.LogFile);
            switch (mode) {
                case CommandMode.Register:
                    await RegistrationService.RegisterAsync();
                    return 0;
                case CommandMode.Unregister:
                    await TaskSchedulerService.UnregisterAsync(AppPaths.OnTaskName);
                    await TaskSchedulerService.UnregisterAsync(AppPaths.OffTaskName);
                    ConsoleOutput.WriteSuccess(Localization.Get("UnregisterSuccess"));
                    ConsoleOutput.PauseForInput();
                    return 0;
                case CommandMode.TurnOn:
                    return await new NicController(logger).TurnOnAsync();
                case CommandMode.TurnOff:
                    return await new NicController(logger).TurnOffAsync();
                case CommandMode.Status:
                    return await new NicController(logger).InspectStatusAsync();
            }
        } catch (Exception ex) {
            ConsoleOutput.WriteError(Localization.Format("Failure", ex.Message));
            if (mode is CommandMode.Register or CommandMode.Unregister)
                ConsoleOutput.PauseForInput();
        }
        return 1;
    }

    private static bool IsAdministrator() {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static int RelaunchAsAdministrator(ReadOnlySpan<string> args) {
        try {
            var startInfo = new ProcessStartInfo(Environment.ProcessPath ?? throw new InvalidOperationException(Localization.Get("ProcessPathMissing"))) {
                UseShellExecute = true,
                Verb = "runas"
            };
            foreach (var arg in args) startInfo.ArgumentList.Add(arg);
            _ = Process.Start(startInfo);
            return 0;
        } catch (Exception ex) {
            ConsoleOutput.WriteError(Localization.Format("ElevationFailed", ex.Message));
            ConsoleOutput.PauseForInput();
            return 1;
        }
    }

    private static async Task<int> RunInteractiveAsync() {
        while (true) {
            if (!Console.IsOutputRedirected) Console.Clear();

            ConsoleOutput.HorizontalRule();
            Console.WriteLine(Localization.Get("MenuTitle"));
            ConsoleOutput.HorizontalRule();
            Console.WriteLine(Localization.Get("MenuOn"));
            Console.WriteLine(Localization.Get("MenuOff"));
            Console.WriteLine(Localization.Get("MenuStatus"));
            Console.WriteLine(Localization.Get("MenuRegister"));
            Console.WriteLine(Localization.Get("MenuUnregister"));
            Console.WriteLine(Localization.Get("MenuExit"));
            Console.WriteLine();
            Console.Write(Localization.Get("MenuPrompt"));

            if (ApplicationArgumentParser.ParseInteractiveSelection(Console.ReadKey().Key) is not { } selectedMode) {
                ConsoleOutput.WriteWarning(Localization.Get("MenuInvalidSelection"));
                await Task.Delay(TimeSpan.FromSeconds(1));
                continue;
            }

            return await RunAsync(selectedMode);
        }
    }

    private static void PrintHelp() {
        Console.WriteLine(Localization.Get("HelpHeader"));
        Console.WriteLine(Localization.Get("HelpRegister"));
        Console.WriteLine(Localization.Get("HelpUnregister"));
        Console.WriteLine(Localization.Get("HelpOn"));
        Console.WriteLine(Localization.Get("HelpOff"));
        Console.WriteLine(Localization.Get("HelpStatus"));
        Console.WriteLine();
        Console.WriteLine(Localization.Get("AdminRequired"));
    }
}
