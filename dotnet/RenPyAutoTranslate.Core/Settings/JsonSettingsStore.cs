using System.Text.Json;
using System.Text.Json.Serialization;

namespace RenPyAutoTranslate.Core.Settings;

public sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly string _filePath;

    public JsonSettingsStore(string? appDataFolder = null)
    {
        var folder = appDataFolder ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RenPyAutoTranslate");
        Directory.CreateDirectory(folder);
        _filePath = Path.Combine(folder, "settings.json");
    }

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
            return new AppSettings();
        await using var fs = File.OpenRead(_filePath);
        var s = await JsonSerializer.DeserializeAsync<AppSettings>(fs, JsonOptions, cancellationToken)
                .ConfigureAwait(false);
        return s ?? new AppSettings();
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        await using var fs = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(fs, settings, JsonOptions, cancellationToken).ConfigureAwait(false);
    }
}
