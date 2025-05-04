using BisBuddy.Gear;
using BisBuddy.Items;
using Dalamud.Configuration;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text.Json;
using JsonException = System.Text.Json.JsonException;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace BisBuddy;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public static readonly int CurrentVersion = 3;
    public static readonly string DefaultGearsetName = "New Gearset";

    public int Version { get; set; } = 2;

    public bool HighlightNeedGreed { get; set; } = true;
    public bool HighlightShops { get; set; } = true;
    public bool HighlightMateriaMeld { get; set; } = true;
    public bool HighlightNextMateria { get; set; } = false;
    public bool HighlightUncollectedItemMateria { get; set; } = true;
    public bool HighlightPrerequisiteMateria { get; set; } = true;
    public bool HighlightInventories { get; set; } = true;
    public bool HighlightMarketboard { get; set; } = true;
    public bool AnnotateTooltips { get; set; } = true;
    public bool AutoCompleteItems { get; set; } = true;
    public bool AutoScanInventory { get; set; } = true;
    public bool PluginUpdateInventoryScan { get; set; } = true;
    public bool StrictMateriaMatching { get; set; } = true;

    //public HighlightColor HighlightColor { get; set; } = new(0.0f, 1.0f, 0.0f, 0.393f);
    public bool BrightListItemHighlighting { get; set; } = true;
    public static readonly float BrightListItemAlpha = 1.0f;
    public HighlightColor DefaultHighlightColor { get; set; } = new(0.0f, 1.0f, 0.0f, 0.393f);
    public readonly Vector3 CustomNodeMultiplyColor = new(0.393f, 0.393f, 0.393f);

    public Dictionary<ulong, CharacterInfo> CharactersData { get; set; } = [];

    public List<Gearset> GetCharacterGearsets(ulong characterId, JsonSerializerOptions jsonOptions)
    {
        // if not logged in, return a dummy list
        if (!Services.ClientState.IsLoggedIn)
            return [];

        if (CharactersData.TryGetValue(characterId, out var charInfo))
            return charInfo.Gearsets;

        Services.Log.Debug($"No existing gearsets found for character {characterId}, creating new one");
        // no existing gearsets found for character, creating new one
        var newCharacterInfo = new CharacterInfo(characterId, []);
        CharactersData.Add(characterId, newCharacterInfo);
        Save(jsonOptions);
        return newCharacterInfo.Gearsets;
    }

    public void Save(JsonSerializerOptions jsonOptions)
    {
        var configText = JsonSerializer.Serialize(this, jsonOptions);
        File.WriteAllText(Services.PluginInterface.ConfigFile.FullName, configText);
    }

    public static Configuration LoadConfig(ItemData itemData, JsonSerializerOptions jsonOptions)
    {
        try
        {
            var configText = File.ReadAllText(Services.PluginInterface.ConfigFile.FullName);
            using (var configJson = JsonDocument.Parse(configText))
            {
                var configVersion = configJson
                    .RootElement
                    .GetProperty(nameof(Version))
                    .GetInt32();

                if (configVersion != CurrentVersion)
                {
                    Services.Log.Warning($"Config version {configVersion} found, current {CurrentVersion}. Attempting migration");
                    return migrateOldConfig(configJson, configText, configVersion, itemData, jsonOptions);
                }
            }

            Services.Log.Verbose($"Loading config...");
            return JsonSerializer.Deserialize<Configuration>(configText, jsonOptions) ?? new Configuration();
        }
        catch (FileNotFoundException)
        {
            return new Configuration();
        }
        catch (JsonException ex)
        {
            Services.Log.Error(ex, "Error loading/converting config file, creating new");
            return new Configuration();
        }
        catch (Exception ex)
        {
            Services.Log.Fatal(ex, $"Error loading {Plugin.PluginName} configuration");
            throw;
        }
    }

    private static Configuration migrateOldConfig(
        JsonDocument configJson,
        string configText,
        int configVersion,
        ItemData itemData,
        JsonSerializerOptions jsonOptions
        )
    {
        var newConfig = new Configuration();
        for (var version = configVersion; version < CurrentVersion; version++)
        {
            newConfig = version switch
            {
                1 => migrate1To2(configText, itemData),
                2 => migrate2To3(configJson, jsonOptions),
                _ => throw new JsonException($"Invalid version number \"{configVersion}\"")
            };
        }

        Services.Log.Info("Config migration success");

        return newConfig;
    }

    private static Configuration migrate1To2(string configText, ItemData itemData)
    {
        /// <summary>
        /// migrate from newtonsoft to stj
        /// migrate from PrerequisiteItems to PrerequisiteTree
        /// </summary>
        try
        {
            var config = JsonConvert.DeserializeObject<Configuration>(configText)
                ?? throw new JsonException("Old config result is null");

            foreach (var charData in config.CharactersData)
            {
                foreach (var gearset in charData.Value.Gearsets)
                {
                    foreach (var gearpiece in gearset.Gearpieces)
                    {
                        // rebuild prereqs with new system
                        gearpiece.PrerequisiteTree = itemData.BuildGearpiecePrerequisiteTree(gearpiece.ItemId);

                        // if gearpiece is collected, this will collect prereqs as well
                        gearpiece.SetCollected(gearpiece.IsCollected, gearpiece.IsManuallyCollected);
                    }
                }
            }

            config.Version = 2;
            return config;
        }
        catch (Newtonsoft.Json.JsonException ex)
        {
            throw new JsonException(ex.Message);
        }
    }

    /// <summary>
    /// Migrate old Vector4 HighlightColor to new HighlightColor type
    /// </summary>
    /// <param name="configJson">JsonDocument config</param>
    /// <param name="jsonOptions">options instance for deserializing</param>
    /// <returns>The migrated and deserialized config</returns>
    /// <exception cref="JsonException">If the config cannot be migrated or deserialized</exception>
    private static Configuration migrate2To3(JsonDocument configJson, JsonSerializerOptions jsonOptions)
    {
        HighlightColor? highlightColor = null;
        if (configJson.RootElement.TryGetProperty("HighlightColor", out var highlightColorProperty))
        {
            var highlightColorVector = JsonSerializer.Deserialize<Vector4>(highlightColorProperty, jsonOptions);
            highlightColor = new HighlightColor(highlightColorVector);
        }

        var config = JsonSerializer.Deserialize<Configuration>(configJson, jsonOptions) ?? new Configuration();
        if (highlightColor is not null)
            config.DefaultHighlightColor = highlightColor;

        config.Version = 3;
        return config;
    }
}
