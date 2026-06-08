using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Klakr.Core.Persistence;

/// <summary>
/// Loads and saves <see cref="Profile"/> JSON files in a single directory.
/// </summary>
/// <remarks>
/// The directory is injected, never derived here: choosing an OS-specific location
/// (<c>%APPDATA%</c> vs <c>~/.config</c>) is platform code and belongs in Klakr.App. This keeps
/// the store pure and unit-testable against a temp directory.
/// </remarks>
public sealed class ProfileStore(string directory)
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    /// <summary>Directory holding the <c>*.json</c> profile files.</summary>
    public string Directory { get; } = directory;

    /// <summary>
    /// The <see cref="JsonSerializerOptions"/> used for all profile (de)serialization.
    /// Exposed so callers and tests can reuse the exact same configuration.
    /// </summary>
    public static JsonSerializerOptions CreateOptions() => new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        // Profiles are hand-edited; don't force the polymorphic "$type" to be the first property.
        AllowOutOfOrderMetadataProperties = true,
        // Hotkey is a plain record struct - System.Text.Json handles it as { "key": "..." }.
        Converters =
        {
            new JsonStringEnumConverter(),
        },
    };

    public string Serialize(Profile profile) => JsonSerializer.Serialize(profile, Options);

    public Profile Deserialize(string json)
        => JsonSerializer.Deserialize<Profile>(json, Options)
           ?? throw new JsonException("Profile JSON deserialized to null.");

    /// <summary>Names (without extension) of every profile file present, sorted.</summary>
    public IReadOnlyList<string> ListProfiles()
    {
        if (!System.IO.Directory.Exists(Directory))
            return [];

        return System.IO.Directory.EnumerateFiles(Directory, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => !string.IsNullOrEmpty(name))
            .Select(name => name!)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public bool Exists(string name) => File.Exists(PathFor(name));

    public Profile Load(string name) => Deserialize(File.ReadAllText(PathFor(name)));

    public void Save(Profile profile)
    {
        System.IO.Directory.CreateDirectory(Directory);
        File.WriteAllText(PathFor(profile.Name), Serialize(profile));
    }

    public void Delete(string name)
    {
        string path = PathFor(name);
        if (File.Exists(path))
            File.Delete(path);
    }

    private string PathFor(string name) => Path.Combine(Directory, Sanitize(name) + ".json");

    /// <summary>Replaces characters that are illegal in a filename with underscores.</summary>
    private static string Sanitize(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Profile name must not be blank.", nameof(name));

        Span<char> buffer = stackalloc char[name.Length];
        char[] invalid = Path.GetInvalidFileNameChars();
        for (int i = 0; i < name.Length; i++)
            buffer[i] = Array.IndexOf(invalid, name[i]) >= 0 ? '_' : name[i];
        return new string(buffer);
    }
}
