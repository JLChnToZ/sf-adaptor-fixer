using System.Runtime.Versioning;
using System.Xml;
using System.Xml.Linq;

namespace SFAdaptorFixer;

[SupportedOSPlatform("windows")]
public static class ScheduledTaskDelegator {
    public static async Task<bool> TryRunAsync(CommandMode mode) {
        var (taskName, arguments) = mode switch {
            CommandMode.TurnOn => (AppPaths.OnTaskName, "on"),
            CommandMode.TurnOff => (AppPaths.OffTaskName, "off"),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        };

        var currentExecutable = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(currentExecutable)) return false;

        var schtasks = Path.Combine(Environment.SystemDirectory, "schtasks.exe");
        var query = await WindowsCommandRunner.RunAsync(schtasks, ["/Query", "/TN", taskName, "/XML", "ONE"], TimeSpan.FromSeconds(15));
        if (!query.Succeeded) return false;
        if (!MatchesRegisteredAction(query.StandardOutput, currentExecutable, arguments)) {
            var installedExecutable = AppPaths.InstalledExecutable;
            if (!MatchesRegisteredAction(query.StandardOutput, installedExecutable, arguments)) return false;
            var currentBytes = await File.ReadAllBytesAsync(currentExecutable);
            var installedBytes = await File.ReadAllBytesAsync(installedExecutable);
            if (!currentBytes.SequenceEqual(installedBytes)) return false;
        }
        var run = await WindowsCommandRunner.RunAsync(schtasks, ["/Run", "/TN", taskName], TimeSpan.FromSeconds(15));
        return run.Succeeded;
    }

    internal static bool MatchesRegisteredAction(string taskXml, string currentExecutable, string expectedArguments) {
        try {
            var document = XDocument.Parse(taskXml);
            var action = document.Descendants().FirstOrDefault(static element => element.Name.LocalName == "Exec");
            var command = action?.Elements().FirstOrDefault(static element => element.Name.LocalName == "Command")?.Value;
            var arguments = action?.Elements().FirstOrDefault(static element => element.Name.LocalName == "Arguments")?.Value;
            var runLevel = document.Descendants().FirstOrDefault(static element => element.Name.LocalName == "RunLevel")?.Value;
            return string.Equals(runLevel, "HighestAvailable", StringComparison.OrdinalIgnoreCase) &&
                PathsEqual(command, currentExecutable) &&
                string.Equals(arguments, expectedArguments, StringComparison.Ordinal);
        } catch (XmlException) {
            return false;
        }
    }

    private static bool PathsEqual(string? first, string? second) {
        if (string.IsNullOrWhiteSpace(first) || string.IsNullOrWhiteSpace(second))
            return false;
        try {
            return string.Equals(Path.GetFullPath(first), Path.GetFullPath(second), StringComparison.OrdinalIgnoreCase);
        } catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) {
            return false;
        }
    }
}
