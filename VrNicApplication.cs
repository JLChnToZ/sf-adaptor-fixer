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

        try {
            if (parsed.Mode == CommandMode.Interactive) return await RunInteractiveAsync();

            if (!IsAdministrator()) {
                if (parsed.Mode is CommandMode.TurnOn or CommandMode.TurnOff) {
                    using var preflightRunner = new WindowsCommandRunner();
                    var delegator = new ScheduledTaskDelegator(preflightRunner);
                    try {
                        if (await delegator.TryRunAsync(parsed.Mode)) {
                            ConsoleOutput.WriteMuted(Localization.Get("DelegatingToTask"));
                            return 0;
                        }
                    } catch (Exception ex) when (ex is InvalidOperationException or TimeoutException) {
                        // A task preflight failure falls back to the existing UAC path.
                    }
                }

                return RelaunchAsAdministrator(args);
            }

            using var runner = new WindowsCommandRunner();
            var devices = new NicDeviceService(runner);
            var wlan = new WlanScanner();
            var logger = new TextLogger(AppPaths.LogFile);

            if (parsed.Mode == CommandMode.Register) {
                var registration = new RegistrationService(devices, runner);
                await registration.RegisterAsync();
                return 0;
            }

            if (parsed.Mode == CommandMode.Unregister) {
                var scheduler = new TaskSchedulerService(runner);
                await scheduler.UnregisterAsync(AppPaths.OnTaskName);
                await scheduler.UnregisterAsync(AppPaths.OffTaskName);
                ConsoleOutput.WriteSuccess(Localization.Get("UnregisterSuccess"));
                ConsoleOutput.PauseForInput();
                return 0;
            }

            var controller = new NicController(devices, logger);
            return parsed.Mode == CommandMode.TurnOn ? await controller.TurnOnAsync() : await controller.TurnOffAsync();
        } catch (Exception ex) {
            ConsoleOutput.WriteError(Localization.Format("Failure", ex.Message));
            if (parsed.Mode is CommandMode.Register or CommandMode.Unregister)
                ConsoleOutput.PauseForInput();
            return 1;
        }
    }

    private static bool IsAdministrator() {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static int RelaunchAsAdministrator(string[] args) {
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
            Console.WriteLine(Localization.Get("MenuRegister"));
            Console.WriteLine(Localization.Get("MenuUnregister"));
            Console.WriteLine(Localization.Get("MenuExit"));
            Console.WriteLine();
            Console.Write(Localization.Get("MenuPrompt"));

            var selection = Console.ReadLine();
            var selectedArguments = ApplicationArgumentParser.ParseInteractiveSelection(selection);
            if (selectedArguments is null) {
                ConsoleOutput.WriteWarning(Localization.Get("MenuInvalidSelection"));
                await Task.Delay(TimeSpan.FromSeconds(1));
                continue;
            }
            if (selectedArguments.Length == 0) return 0;
            return await RunAsync(selectedArguments);
        }
    }

    private static void PrintHelp() {
        Console.WriteLine(Localization.Get("HelpHeader"));
        Console.WriteLine(Localization.Get("HelpRegister"));
        Console.WriteLine(Localization.Get("HelpUnregister"));
        Console.WriteLine(Localization.Get("HelpOn"));
        Console.WriteLine(Localization.Get("HelpOff"));
        Console.WriteLine();
        Console.WriteLine(Localization.Get("AdminRequired"));
    }
}
