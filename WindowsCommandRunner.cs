using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace SFAdaptorFixer;

public sealed class WindowsCommandRunner : IDisposable {
    private bool _disposed;

    public async Task<CommandResult> RunAsync(string fileName, IEnumerable<string> arguments, TimeSpan timeout, bool throwOnTimeout = true) {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var startInfo = new ProcessStartInfo(fileName) {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = startInfo };
        try {
            if (!process.Start()) throw new Win32Exception(Localization.Format("CannotStart", fileName));
        } catch (Exception ex) when (ex is Win32Exception or InvalidOperationException) {
            throw new InvalidOperationException(Localization.Format("CannotExecute", fileName, ex.Message), ex);
        }

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        using var cancellation = new CancellationTokenSource(timeout);

        try {
            await process.WaitForExitAsync(cancellation.Token);
        } catch (OperationCanceledException) when (throwOnTimeout) {
            try {
                process.Kill(entireProcessTree: true);
            } catch (InvalidOperationException) { }
            throw new TimeoutException(Localization.Format("CommandTimeout", fileName, timeout.TotalSeconds));
        }

        return new CommandResult(process.HasExited ? process.ExitCode : -1, await outputTask, await errorTask);
    }

    public async Task<CommandResult> RunRequiredAsync(string fileName, IEnumerable<string> arguments, TimeSpan timeout, string operation) {
        var result = await RunAsync(fileName, arguments, timeout);
        if (!result.Succeeded) {
            var details = FirstNonEmptyLine(result.StandardError, result.StandardOutput);
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(details) ?
                Localization.Format("OperationFailedCode", operation, result.ExitCode) :
                Localization.Format("OperationFailedDetails", operation, details));
        }
        return result;
    }

    private static string FirstNonEmptyLine(string first, string second) {
        foreach (var value in new[] { first, second }) {
            var line = value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Select(item => item.Trim())
                .FirstOrDefault(item => item.Length > 0);
            if (line is not null) return line;
        }
        return "";
    }

    public void Dispose() => _disposed = true;
}
