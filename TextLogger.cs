using System.Text;

namespace SFAdaptorFixer;

public sealed class TextLogger(string path) {
    static readonly Encoding Encoding = new UTF8Encoding(false);

    public void Write(string message) {
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var line = FormattableString.Invariant($"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {message}{Environment.NewLine}");
        File.AppendAllText(path, line, Encoding);
    }
}
