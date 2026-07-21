using System.Text.Json;
using System.Text.Json.Serialization;

namespace Klakr.App.Services;

/// <summary>Loads and saves <see cref="AppSettings"/> as a single hand-editable JSON file.</summary>
public sealed class SettingsStore(string filePath)
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>
    /// Peek at <see cref="AppSettings.DiagnosticsSidecarOpen"/> without instantiating a store.
    /// Used before <see cref="AppHost"/> exists so we can enable <see cref="DiagLog"/> in time
    /// to capture startup log events (settings migration, profile reload, etc).
    /// </summary>
    public static bool TryPeekSidecarOpen(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
                return false;
            AppSettings? s = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(filePath), Options);
            return s?.DiagnosticsSidecarOpen ?? false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Reads the settings file, or returns defaults if it is missing or unreadable.</summary>
    public AppSettings Load()
    {
        try
        {
            if (File.Exists(filePath))
            {
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(filePath), Options)
                       ?? new AppSettings();
            }
        }
        catch
        {
            // A corrupt settings file must not block startup - fall back to defaults.
        }

        return new AppSettings();
    }

    /// <summary>
    /// Load settings, immediately re-save. Fills in any newly-added properties with their
    /// defaults while preserving every existing value the user has set. Non-destructive:
    /// System.Text.Json ignores unknown JSON fields on read but they are dropped on re-save.
    /// Returns true if the on-disk file was actually rewritten (i.e. content differed).
    /// </summary>
    public bool MigrateSchemaIfNeeded()
    {
        AppSettings current = Load();
        string desired = JsonSerializer.Serialize(current, Options);

        string existing = File.Exists(filePath) ? File.ReadAllText(filePath) : "";
        if (string.Equals(existing, desired, StringComparison.Ordinal))
            return false;

        string? directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllText(filePath, desired);
        return true;
    }

    public void Save(AppSettings settings)
    {
        string? directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(filePath, JsonSerializer.Serialize(settings, Options));
    }
}
