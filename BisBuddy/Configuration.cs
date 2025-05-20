using BisBuddy.Gear;
using BisBuddy.Items;
using Dalamud.Configuration;
using Newtonsoft.Json;
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
}
