namespace SFAdaptorFixer;

public static class ConsoleOutput {
    public static void HorizontalRule() => Console.WriteLine("====================================================");

    public static void WriteSuccess(string text) => WriteColor(text, ConsoleColor.Green);

    public static void WriteWarning(string text) => WriteColor(text, ConsoleColor.Yellow);

    public static void WriteError(string text) => WriteColor(text, ConsoleColor.Red);

    public static void WriteMuted(string text) => WriteColor(text, ConsoleColor.DarkGray);

    public static void PauseForInput() {
        if (Console.IsInputRedirected) return;
        Console.Write(Localization.Get("PressEnter"));
        _ = Console.ReadLine();
    }

    public static Task DelayAsync(TimeSpan delay) => Console.IsInputRedirected ? Task.CompletedTask : Task.Delay(delay);

    private static void WriteColor(string text, ConsoleColor color) {
        var original = Console.ForegroundColor;
        try {
            Console.ForegroundColor = color;
            Console.WriteLine(text);
        } finally {
            Console.ForegroundColor = original;
        }
    }
}
