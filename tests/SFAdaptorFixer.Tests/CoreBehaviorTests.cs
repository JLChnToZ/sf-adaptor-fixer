using System.Globalization;
using System.Text;
using Xunit;

namespace SFAdaptorFixer.Tests;

public sealed class CoreBehaviorTests {
    [Theory]
    [InlineData("en-US", "Usage:")]
    [InlineData("zh-TW", "用法:")]
    [InlineData("zh-Hant-HK", "用法:")]
    [InlineData("zh-CN", "用法:")]
    [InlineData("zh-Hans-SG", "用法:")]
    [InlineData("ja-JP", "使い方:")]
    public void Localization_SelectsExpectedLanguage(string cultureName, string expected) {
        var originalCulture = CultureInfo.CurrentUICulture;
        try {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(cultureName);
            Assert.Equal(expected, Localization.Get("HelpHeader"));
        } finally {
            CultureInfo.CurrentUICulture = originalCulture;
        }
    }

    [Fact]
    public void Localization_AllSupportedCulturesHaveTheSameKeys() {
        var baseKeys = Localization.GetKeys(CultureInfo.GetCultureInfo("en"))
            .Order()
            .ToArray();

        foreach (var cultureName in new[] { "zh-TW", "zh-CN", "ja-JP" }) {
            var localizedKeys = Localization.GetKeys(CultureInfo.GetCultureInfo(cultureName))
                .Order()
                .ToArray();
            Assert.Equal(baseKeys, localizedKeys);
        }
    }

    [Fact]
    public void AppPaths_UseProgramFilesEnvironmentVariable() {
        var programFiles = Environment.GetEnvironmentVariable("ProgramFiles");
        Assert.False(string.IsNullOrWhiteSpace(programFiles));
        Assert.Equal(Path.Combine(programFiles, "VRNic"), AppPaths.InstallDirectory);
        Assert.Equal(
            Path.Combine(programFiles, "VRNic", "SFAdaptorFixer.exe"),
            AppPaths.InstalledExecutable);
    }

    [Fact]
    public void Parse_NoArguments_OpensInteractiveMenu() {
        var result = ApplicationArgumentParser.Parse(Array.Empty<string>());

        Assert.NotNull(result);
        Assert.Equal(CommandMode.Interactive, result.Mode);
    }

    [Theory]
    [InlineData(ConsoleKey.D1, CommandMode.TurnOn)]
    [InlineData(ConsoleKey.D2, CommandMode.TurnOff)]
    [InlineData(ConsoleKey.D3, CommandMode.Register)]
    [InlineData(ConsoleKey.D4, CommandMode.Unregister)]
    public void ParseInteractiveSelection_ReturnsRequestedCommand(ConsoleKey selection, CommandMode expected) {
        var result = ApplicationArgumentParser.ParseInteractiveSelection(selection);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ParseInteractiveSelection_ExitReturnsNoCommand() {
        Assert.Null(ApplicationArgumentParser.ParseInteractiveSelection(ConsoleKey.Escape));
    }

    [Fact]
    public void ParseInteractiveSelection_RejectsUnknownInput() {
        Assert.Null(ApplicationArgumentParser.ParseInteractiveSelection(ConsoleKey.D9));
    }

    [Theory]
    [InlineData("register", CommandMode.Register)]
    [InlineData("unregister", CommandMode.Unregister)]
    [InlineData("--on", CommandMode.TurnOn)]
    [InlineData("--off", CommandMode.TurnOff)]
    public void Parse_SupportedArgument_SelectsMode(string argument, CommandMode expected) {
        var result = ApplicationArgumentParser.Parse([argument]);

        Assert.NotNull(result);
        Assert.Equal(expected, result.Mode);
    }

    [Theory]
    [InlineData("on")]
    [InlineData("Register")]
    [InlineData("--unknown")]
    [InlineData("")]
    public void Parse_UnsupportedArgument_IsRejected(string argument) {
        Assert.Null(ApplicationArgumentParser.Parse([argument]));
    }

    [Fact]
    public void Classify_DisabledPnPDevice_IsDisabled() {
        var result = StateClassifier.Classify("Error", 22, true);

        Assert.Equal(NicStateKind.Disabled, result.Kind);
        Assert.Equal("Disabled", result.DisplayName);
    }

    [Fact]
    public void Classify_UnpluggedPresentDevice_IsNotPresent() {
        var result = StateClassifier.Classify("Unknown", 45, false);

        Assert.Equal(NicStateKind.NotPresent, result.Kind);
    }

    [Fact]
    public void Classify_HealthyDevice_IsEnabled() {
        Assert.Equal(
            NicStateKind.Enabled,
            StateClassifier.Classify("OK", 0, true).Kind);
    }

    [Theory]
    [InlineData(5_924_999, false)]
    [InlineData(5_925_000, true)]
    [InlineData(6_000_000, true)]
    [InlineData(7_125_000, true)]
    [InlineData(7_125_001, false)]
    public void Is6GHz_UsesInclusivePowerShellRange(int frequencyKHz, bool expected) {
        Assert.Equal(expected, FrequencyClassifier.Is6GHz(frequencyKHz));
    }

    [Fact]
    public void BuildTaskDocument_UsesHighestPrivilegeAndFiveMinuteLimit() {
        var document = TaskSchedulerService.BuildTaskDocument(
            "--off",
            "Test task",
            @"DESKTOP\User");
        var ns = document.Root!.Name.Namespace;

        Assert.Equal("UTF-16", document.Declaration?.Encoding);
        Assert.Equal("HighestAvailable", document.Descendants(ns + "RunLevel").Single().Value);
        Assert.Equal("InteractiveToken", document.Descendants(ns + "LogonType").Single().Value);
        Assert.Equal("PT5M", document.Descendants(ns + "ExecutionTimeLimit").Single().Value);
        Assert.Equal("IgnoreNew", document.Descendants(ns + "MultipleInstancesPolicy").Single().Value);
        Assert.Equal("--off", document.Descendants(ns + "Arguments").Single().Value);
        Assert.Equal(AppPaths.InstalledExecutable, document.Descendants(ns + "Command").Single().Value);
    }

    [Fact]
    public void SaveTaskDocument_WritesUtf16WithBom() {
        var path = Path.Combine(Path.GetTempPath(), $"vrnic-task-{Guid.NewGuid():N}.xml");
        try {
            TaskSchedulerService.SaveTaskDocument(
                TaskSchedulerService.BuildTaskDocument("--on", "Test task", @"DESKTOP\User"),
                path);

            var bytes = File.ReadAllBytes(path);
            Assert.True(bytes.Length >= 2);
            Assert.Equal(new byte[] { 0xff, 0xfe }, bytes[..2]);
            Assert.Contains(
                "encoding=\"utf-16\"",
                File.ReadAllText(path, Encoding.Unicode),
                StringComparison.OrdinalIgnoreCase);
        } finally {
            File.Delete(path);
        }
    }

    [Fact]
    public void MatchesRegisteredAction_AcceptsMatchingExecutableAndArgument() {
        var xml = BuildTaskXml(@"C:\Program Files\VRNic\SFAdaptorFixer.exe", "--on");

        Assert.True(ScheduledTaskDelegator.MatchesRegisteredAction(
            xml,
            @"c:\program files\vrnic\sfadaptorfixer.exe",
            "--on"));
    }

    [Theory]
    [InlineData(@"C:\Other\SFAdaptorFixer.exe", "--on")]
    [InlineData(@"C:\Program Files\VRNic\SFAdaptorFixer.exe", "--off")]
    [InlineData(@"", "--on")]
    public void MatchesRegisteredAction_RejectsMismatchedTask(
        string command,
        string arguments) {
        var xml = BuildTaskXml(command, arguments);

        Assert.False(ScheduledTaskDelegator.MatchesRegisteredAction(
            xml,
            @"C:\Program Files\VRNic\SFAdaptorFixer.exe",
            "--on"));
    }

    [Fact]
    public void MatchesRegisteredAction_RejectsMalformedXml() {
        Assert.False(ScheduledTaskDelegator.MatchesRegisteredAction(
            "<Task",
            @"C:\Program Files\VRNic\SFAdaptorFixer.exe",
            "--on"));
    }

    [Fact]
    public void MatchesRegisteredAction_RejectsLimitedTask() {
        Assert.False(ScheduledTaskDelegator.MatchesRegisteredAction(
            BuildTaskXml(
                @"C:\Program Files\VRNic\SFAdaptorFixer.exe",
                "--on",
                "Limited"),
            @"C:\Program Files\VRNic\SFAdaptorFixer.exe",
            "--on"));
    }

    private static string BuildTaskXml(
        string command,
        string arguments,
        string runLevel = "HighestAvailable") =>
        $"""
        <Task xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
          <Principals>
            <Principal>
              <RunLevel>{runLevel}</RunLevel>
            </Principal>
          </Principals>
          <Actions>
            <Exec>
              <Command>{command}</Command>
              <Arguments>{arguments}</Arguments>
            </Exec>
          </Actions>
        </Task>
        """;
}
