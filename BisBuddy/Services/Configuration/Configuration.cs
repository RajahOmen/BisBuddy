using BisBuddy.Gear;
using Dalamud.Configuration;
using System;
using System.Collections.Generic;

namespace BisBuddy.Services.Configuration;

[Serializable]
public class Configuration : IConfigurationProperties
{
    public static readonly int CurrentVersion = 4;

    public int Version { get; set; } = 4;

    public bool HighlightNeedGreed { get; set; } = true;
    public bool HighlightShops { get; set; } = true;
    public bool HighlightMateriaMeld { get; set; } = true;
    public bool HighlightNextMateria { get; set; } = false;
    public bool HighlightUncollectedItemMateria { get; set; } = true;
    public bool HighlightPrerequisiteMateria { get; set; } = true;
    public bool HighlightInventories { get; set; } = true;
    public bool HighlightCollectedInInventory { get; set; } = true;
    public bool HighlightMarketboard { get; set; } = true;
    public bool AnnotateTooltips { get; set; } = true;
    public bool AutoCompleteItems { get; set; } = true;
    public bool AutoScanInventory { get; set; } = true;
    public bool PluginUpdateInventoryScan { get; set; } = true;
    public bool StrictMateriaMatching { get; set; } = true;

    public bool BrightListItemHighlighting { get; set; } = true;
    public HighlightColor DefaultHighlightColor { get; set; } = new(0.0f, 1.0f, 0.0f, 0.393f);
    public UiTheme UiTheme { get; set; } = new();

    // DEBUGGING
    public bool EnableDebugging { get; set; } = false;
    public bool DebugFrameworkAsserts { get; set; } = false;

    public Dictionary<ulong, CharacterInfo> CharactersData { get; set; } = [];
}

public interface IConfigurationProperties : IPluginConfiguration
{
    public bool HighlightNeedGreed { get; set; }
    public bool HighlightShops { get; set; }
    public bool HighlightMateriaMeld { get; set; }
    public bool HighlightNextMateria { get; set; }
    public bool HighlightUncollectedItemMateria { get; set; }
    public bool HighlightPrerequisiteMateria { get; set; }
    public bool HighlightInventories { get; set; }
    public bool HighlightCollectedInInventory { get; set; }
    public bool HighlightMarketboard { get; set; }
    public bool AnnotateTooltips { get; set; }
    public bool AutoCompleteItems { get; set; }
    public bool AutoScanInventory { get; set; }
    public bool PluginUpdateInventoryScan { get; set; }
    public bool StrictMateriaMatching { get; set; }
    public bool BrightListItemHighlighting { get; set; }
    public HighlightColor DefaultHighlightColor { get; }
    public UiTheme UiTheme { get; set; }

    // DEBUGGING
    public bool EnableDebugging { get; set; }
    public bool DebugFrameworkAsserts { get; set; }
}

