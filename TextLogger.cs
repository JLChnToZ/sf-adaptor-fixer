using System.Text;

namespace SFAdaptorFixer;

public sealed class TextLogger(string path) {
    static readonly UTF8Encoding Utf8NoBom = new(false);

    public void Write(string message) {
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {message}{Environment.NewLine}";
        File.AppendAllText(path, line, Utf8NoBom);
    }
}
