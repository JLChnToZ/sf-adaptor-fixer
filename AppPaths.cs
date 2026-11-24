namespace SFAdaptorFixer;

public static class AppPaths {
    public const string OnTaskName = "VRNicOn";
    public const string OffTaskName = "VRNicOff";
    public static string InstallDirectory => Path.Combine(GetProgramFilesDirectory(), "VRNic");
    public static string InstalledExecutable => Path.Combine(InstallDirectory, "SFAdaptorFixer.exe");
    public static string LogFile => Path.Combine(GetUserDataDirectory(), "VRNIC.log");

    private static string GetProgramFilesDirectory() {
        var programFiles = Environment.GetEnvironmentVariable("ProgramFiles");
        if (string.IsNullOrWhiteSpace(programFiles))
            throw new InvalidOperationException(Localization.Get("ProgramFilesMissing"));

        return Path.GetFullPath(programFiles);
    }

    private static string GetUserDataDirectory() {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
            localAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");

        if (!string.IsNullOrWhiteSpace(localAppData))
            return Path.Combine(Path.GetFullPath(localAppData), "SFAdaptorFixer");

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(userProfile))
            userProfile = Environment.GetFolderPath(Environment.SpecialFolder.Personal);

        if (string.IsNullOrWhiteSpace(userProfile))
            throw new InvalidOperationException(Localization.Get("ProgramFilesMissing"));

        return Path.Combine(Path.GetFullPath(userProfile), ".SFAdaptorFixer");
    }
}
