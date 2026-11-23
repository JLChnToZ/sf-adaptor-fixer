namespace SFAdaptorFixer;

public static class AppPaths {
    public const string OnTaskName = "VRNicOn";
    public const string OffTaskName = "VRNicOff";
    public static string InstallDirectory => Path.Combine(GetProgramFilesDirectory(), "VRNic");
    public static string InstalledExecutable => Path.Combine(InstallDirectory, "SFAdaptorFixer.exe");
    public static string LogFile => Path.Combine(InstallDirectory, "vrnic.log");

    private static string GetProgramFilesDirectory() {
        var programFiles = Environment.GetEnvironmentVariable("ProgramFiles");
        if (string.IsNullOrWhiteSpace(programFiles))
            throw new InvalidOperationException(Localization.Get("ProgramFilesMissing"));

        return Path.GetFullPath(programFiles);
    }
}
