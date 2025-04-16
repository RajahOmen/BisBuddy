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
    public static readonly int CurrentVersion = 2;
    public static readonly string DefaultGearsetName = "New Gearset";

    public int Version { get; set; } = 2;

    public bool HighlightNeedGreed { get; set; } = true;
    public bool HighlightShops { get; set; } = true;
    public bool HighlightMateriaMeld { get; set; } = true;
    public bool HighlightNextMateria { get; set; } = false;
    public bool HighlightCollectedItemMateria { get; set; } = false;
    public bool HighlightPrerequisiteMateria { get; set; } = true;
    public bool HighlightInventories { get; set; } = true;
    public bool HighlightMarketboard { get; set; } = true;
    public bool AnnotateTooltips { get; set; } = true;
    public bool AutoCompleteItems { get; set; } = true;
    public bool AutoScanInventory { get; set; } = true;
    public bool PluginUpdateInventoryScan { get; set; } = true;
    public bool StrictMateriaMatching { get; set; } = true;

    // default equivalent to R:0, B:255, G:0, A:100, which which translates to
    // normal nodes: (0, 100, 0)
    // custom nodes: (-255, 255, -255)
    public Vector4 HighlightColor { get; set; } = new Vector4(0.0f, 1.0f, 0.0f, 0.392f);
    // scale between -1.0f and 1.0f. (ex: 0.0f -> -1.0f, 0.5f -> 0.0f, 1.0f -> 1.0f)
    public Vector3 CustomNodeAddColor => new(
        (HighlightColor.X * 2) - 1,
        (HighlightColor.Y * 2) - 1,
        (HighlightColor.Z * 2) - 1
        );
    public readonly float CustomNodeAlpha = 1.0f;
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
                    return migrateOldConfig(configJson, configText, configVersion, itemData);
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

    private static Configuration migrateOldConfig(JsonDocument configJson, string configText, int configVersion, ItemData itemData)
    {
        var newConfig = new Configuration();
        for (var version = configVersion; version < CurrentVersion; version++)
        {
            newConfig = version switch
            {
                1 => migrate1To2(configText, itemData),
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
}
