using BisBuddy.Gear;
using Dalamud.Configuration;
using System;
using System.Collections.Generic;

namespace BisBuddy;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public bool HighlightNeedGreed { get; set; } = true;
    public bool HighlightShops { get; set; } = true;
    public bool HighlightMateriaMeld { get; set; } = true;
    public bool HighlightInventories { get; set; } = true;
    public bool HighlightMarketboard { get; set; } = true;
    public bool AnnotateTooltips { get; set; } = true;
    public bool AutoCompleteItems { get; set; } = true;
    public bool AutoScanInventory { get; set; } = true;

    public List<CharacterInfo> CharactersData { get; set; } = [];

    public List<Gearset> GetCharacterGearsets(ulong characterId)
    {
        foreach (var character in CharactersData)
        {
            if (character.CharacterId == characterId)
            {
                return character.Gearsets;
            }
        }

        Services.Log.Debug($"No existing gearsets found for character {characterId}, creating new one");

        // no existing gearsets found for character, creating new one
        var newCharacterInfo = new CharacterInfo(characterId, []);
        CharactersData.Add(newCharacterInfo);
        Save();
        return newCharacterInfo.Gearsets;
    }

    public void Save()
    {
        Services.PluginInterface.SavePluginConfig(this);
    }
}
