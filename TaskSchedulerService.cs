using System.Runtime.Versioning;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace SFAdaptorFixer;

[SupportedOSPlatform("windows")]
public static class TaskSchedulerService {
    private static readonly Encoding Encoding = Encoding.Unicode;
    private static readonly XNamespace TaskNamespace = "http://schemas.microsoft.com/windows/2004/02/mit/task";
    private const string TRUE = "true";
    private const string FALSE = "false";

    public static async Task RegisterAsync(string taskName, string arguments, string description, string userName) {
        var xmlPath = Path.Combine(Path.GetTempPath(), $"VRNic-{Guid.NewGuid():N}.xml");
        try {
            var document = BuildTaskDocument(arguments, description, userName);
            SaveTaskDocument(document, xmlPath);

            _ = await WindowsCommandRunner.RunRequiredAsync(
                Path.Combine(Environment.SystemDirectory, "schtasks.exe"),
                ["/Create", "/TN", taskName, "/XML", xmlPath, "/F"],
                TimeSpan.FromSeconds(45),
                Localization.Format("RegisterTaskOperation", taskName));
        } finally {
            File.Delete(xmlPath);
        }
    }

    public static async Task<string> QueryAsync(string taskName) {
        var result = await WindowsCommandRunner.RunRequiredAsync(
            Path.Combine(Environment.SystemDirectory, "schtasks.exe"),
            ["/Query", "/TN", taskName, "/FO", "LIST", "/V"],
            TimeSpan.FromSeconds(30),
            Localization.Format("QueryTaskOperation", taskName));
        return result.StandardOutput;
    }

    public static async Task UnregisterAsync(string taskName) {
        var storedTaskPath = Path.Combine(
            Environment.SystemDirectory,
            "Tasks",
            taskName);
        if (!File.Exists(storedTaskPath)) {
            ConsoleOutput.WriteMuted(Localization.Format("TaskNotRegistered", taskName));
            return;
        }

        _ = await WindowsCommandRunner.RunRequiredAsync(
            Path.Combine(Environment.SystemDirectory, "schtasks.exe"),
            ["/Delete", "/TN", taskName, "/F"],
            TimeSpan.FromSeconds(45),
            Localization.Format("UnregisterTaskOperation", taskName));
        ConsoleOutput.WriteSuccess(Localization.Format("TaskRemoved", taskName));
    }

    internal static XDocument BuildTaskDocument(string arguments, string description, string userName) {
        var principals = new XElement(
            TaskNamespace + "Principals",
            new XElement(
                TaskNamespace + "Principal",
                new XAttribute("id", "Author"),
                new XElement(TaskNamespace + "UserId", userName),
                new XElement(TaskNamespace + "LogonType", "InteractiveToken"),
                new XElement(TaskNamespace + "RunLevel", "HighestAvailable")));

        var settings = new XElement(
            TaskNamespace + "Settings",
            new XElement(TaskNamespace + "MultipleInstancesPolicy", "IgnoreNew"),
            new XElement(TaskNamespace + "DisallowStartIfOnBatteries", FALSE),
            new XElement(TaskNamespace + "StopIfGoingOnBatteries", FALSE),
            new XElement(TaskNamespace + "AllowHardTerminate", TRUE),
            new XElement(TaskNamespace + "StartWhenAvailable", TRUE),
            new XElement(TaskNamespace + "RunOnlyIfNetworkAvailable", FALSE),
            new XElement(TaskNamespace + "IdleSettings",
                new XElement(TaskNamespace + "StopOnIdleEnd", TRUE),
                new XElement(TaskNamespace + "RestartOnIdle", FALSE)),
            new XElement(TaskNamespace + "AllowStartOnDemand", TRUE),
            new XElement(TaskNamespace + "Enabled", TRUE),
            new XElement(TaskNamespace + "Hidden", FALSE),
            new XElement(TaskNamespace + "RunOnlyIfIdle", FALSE),
            new XElement(TaskNamespace + "WakeToRun", FALSE),
            new XElement(TaskNamespace + "ExecutionTimeLimit", "PT5M"),
            new XElement(TaskNamespace + "Priority", "4"));

        var action = new XElement(
            TaskNamespace + "Actions",
            new XAttribute("Context", "Author"),
            new XElement(
                TaskNamespace + "Exec",
                new XElement(TaskNamespace + "Command", AppPaths.InstalledExecutable),
                new XElement(TaskNamespace + "Arguments", arguments),
                new XElement(TaskNamespace + "WorkingDirectory", AppPaths.InstallDirectory)));

        return new XDocument(
            new XDeclaration("1.0", "UTF-16", null),
            new XElement(
                TaskNamespace + "Task",
                new XAttribute("version", "1.2"),
                new XElement(
                    TaskNamespace + "RegistrationInfo",
                    new XElement(TaskNamespace + "Author", userName),
                    new XElement(TaskNamespace + "Description", description)),
                new XElement(TaskNamespace + "Triggers"),
                principals,
                settings,
                action));
    }

    internal static void SaveTaskDocument(XDocument document, string path) {
        var writerSettings = new XmlWriterSettings {
            Encoding = Encoding,
            Indent = true
        };
        using var writer = XmlWriter.Create(path, writerSettings);
        document.Save(writer);
    }
}
