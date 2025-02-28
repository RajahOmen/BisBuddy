using BisBuddy.Gear;
using Dalamud.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text.Json;

namespace BisBuddy;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public static readonly int CurrentVersion = 3;
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        IncludeFields = true,
    };
    public int Version { get; set; } = 1;

    public bool HighlightNeedGreed { get; set; } = true;
    public bool HighlightShops { get; set; } = true;
    public bool HighlightMateriaMeld { get; set; } = true;
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

    public Dictionary<ulong, CharacterInfo> CharactersData { get; set; } = [];

    public List<Gearset> GetCharacterGearsets(ulong characterId)
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
        Save();
        return newCharacterInfo.Gearsets;
    }

    public void Save()
    {
        var configText = JsonSerializer.Serialize(this, JsonOptions);
        File.WriteAllText(Services.PluginInterface.ConfigFile.FullName, configText);
    }

    public static Configuration LoadConfig()
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
                    configText = migrateConfig(configJson, configText, configVersion);
            }
            return JsonSerializer.Deserialize<Configuration>(configText, JsonOptions) ?? new Configuration();
        }
        catch (FileNotFoundException)
        {
            return new Configuration();
        }
        catch (JsonException)
        {
            // maybe not ideal
            return new Configuration();
        }
        catch (Exception ex)
        {
            Services.Log.Fatal(ex, $"Error loading {Plugin.PluginName} configuration");
            throw;
        }
    }

    private static string migrateConfig(JsonDocument configJson, string configText, int configVersion)
    {
        // todo
        return configText;
    }
}
