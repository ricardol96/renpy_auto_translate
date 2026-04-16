using System.Reflection;
using System.Text.Json;

namespace RenPyAutoTranslate.Core;

/// <summary>Maps lowercase English language name (pycountry) to ISO 639-1.</summary>
public static class LanguageNames
{
    private static IReadOnlyDictionary<string, string>? _map;

    public static IReadOnlyDictionary<string, string> Map =>
        _map ??= Load();

    private static Dictionary<string, string> Load()
    {
        var assembly = typeof(LanguageNames).Assembly;
        var name = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("language_names_to_iso.json", StringComparison.Ordinal));
        if (name is null)
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using var stream = assembly.GetManifestResourceStream(name);
        if (stream is null)
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();
        var raw = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
        return raw is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(raw, StringComparer.OrdinalIgnoreCase);
    }

    public static bool TryGetIso(string folderNameLower, out string iso)
    {
        iso = "";
        return Map.TryGetValue(folderNameLower.Trim().ToLowerInvariant(), out iso!);
    }
}
