using BisBuddy.Gear;
using Dalamud.Configuration;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace BisBuddy;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public static readonly int CurrentVersion = 2;
    public int Version { get; set; } = 1;

    public bool HighlightNeedGreed { get; set; } = true;
    public bool HighlightShops { get; set; } = true;
    public bool HighlightMateriaMeld { get; set; } = true;
    public bool HighlightInventories { get; set; } = true;
    public bool HighlightMarketboard { get; set; } = true;
    public bool AnnotateTooltips { get; set; } = true;
    public bool AutoCompleteItems { get; set; } = true;
    public bool AutoScanInventory { get; set; } = true;

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
        Services.PluginInterface.SavePluginConfig(this);
    }
}
