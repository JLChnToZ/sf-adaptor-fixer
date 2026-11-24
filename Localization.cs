using System.Collections;
using System.Globalization;
using System.Resources;

namespace SFAdaptorFixer;

public static class Localization {
    private static readonly ResourceManager Resources =
        new("SFAdaptorFixer.Resources.Messages", typeof(Localization).Assembly);

    public static string Get(string name) =>
        Resources.GetString(name, ResolveResourceCulture(CultureInfo.CurrentUICulture)) ??
        throw new MissingManifestResourceException($"Missing localized string '{name}' for culture '{CultureInfo.CurrentUICulture.Name}'.");

    public static string Format(string name, params scoped ReadOnlySpan<object?> arguments) =>
        string.Format(CultureInfo.CurrentCulture, Get(name), arguments);

    internal static IEnumerable<string> GetKeys(CultureInfo culture) => [..(
        Resources.GetResourceSet(ResolveResourceCulture(culture), createIfNotExists: true, tryParents: true) ??
        throw new MissingManifestResourceException($"Missing localized resources for culture '{culture.Name}'.")
    ).Cast<DictionaryEntry>().Select(static entry => (string)entry.Key)];

    private static CultureInfo ResolveResourceCulture(CultureInfo culture) {
        var name = culture.Name;
        return name.StartsWith("zh-Hant", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("zh-TW", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("zh-HK", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("zh-MO", StringComparison.OrdinalIgnoreCase) ?
            CultureInfo.GetCultureInfo("zh-TW") :
            name.StartsWith("zh-Hans", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("zh-CN", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("zh-SG", StringComparison.OrdinalIgnoreCase) ?
            CultureInfo.GetCultureInfo("zh-CN") :
            culture;
    }
}
