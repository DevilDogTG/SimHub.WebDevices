using System;
using Newtonsoft.Json;

namespace DevDogs.WebDevices.Core.Config;

/// <summary>
/// Reads and writes the JSON export format of <see cref="RouterSettings"/>.
/// </summary>
public static class RouterSettingsJson
{
    private static readonly JsonSerializerSettings Options = new()
    {
        Formatting = Formatting.Indented,
        // Skipping nulls both ways keeps exports short and lets a null in an import fall back to the default.
        NullValueHandling = NullValueHandling.Ignore,
        ObjectCreationHandling = ObjectCreationHandling.Replace,
        MissingMemberHandling = MissingMemberHandling.Ignore,
    };

    /// <summary>
    /// Writes <paramref name="settings"/> as indented JSON.
    /// </summary>
    /// <param name="settings">The settings to write.</param>
    /// <returns>The JSON text.</returns>
    public static string Serialize(RouterSettings settings) => JsonConvert.SerializeObject(settings, Options);

    /// <summary>
    /// Reads settings from JSON. Missing fields keep their defaults; the result is not validated
    /// (see <see cref="RouterSettingsValidator"/>).
    /// </summary>
    /// <param name="json">The JSON text.</param>
    /// <returns>The settings.</returns>
    /// <exception cref="FormatException">The text is not a JSON settings object.</exception>
    public static RouterSettings Deserialize(string json)
    {
        RouterSettings? settings;
        try
        {
            settings = JsonConvert.DeserializeObject<RouterSettings>(json, Options);
        }
        catch (JsonException ex)
        {
            throw new FormatException($"Not a valid settings file: {ex.Message}", ex);
        }

        return settings ?? throw new FormatException("Not a valid settings file: it is empty.");
    }
}
