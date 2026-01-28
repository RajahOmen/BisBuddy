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
    public bool StrictMateriaMatching { get; set; } = false;
    public bool AllowGearpiecesAsPrerequisites { get; set; } = true;

    public bool BrightListItemHighlighting { get; set; } = true;
    public HighlightColor DefaultHighlightColor { get; set; } = new(0.0f, 1.0f, 0.0f, 0.393f);
    public UiTheme UiTheme { get; set; } = new();

    // DEBUGGING
    public bool EnableDebugging { get; set; } = false;
    public FrameworkThreadBehaviorType DebugFrameworkThreadBehavior { get; set; } = FrameworkThreadBehaviorType.Warning;

    public Dictionary<ulong, CharacterInfo> CharactersData { get; set; } = [];
}

public interface IConfigurationProperties : IPluginConfiguration
{
    bool HighlightNeedGreed { get; set; }
    bool HighlightShops { get; set; }
    bool HighlightMateriaMeld { get; set; }
    bool HighlightNextMateria { get; set; }
    bool HighlightUncollectedItemMateria { get; set; }
    bool HighlightPrerequisiteMateria { get; set; }
    bool HighlightInventories { get; set; }
    bool HighlightCollectedInInventory { get; set; }
    bool HighlightMarketboard { get; set; }
    bool AnnotateTooltips { get; set; }
    bool AutoCompleteItems { get; set; }
    bool AutoScanInventory { get; set; }
    bool PluginUpdateInventoryScan { get; set; }
    bool StrictMateriaMatching { get; set; }
    bool AllowGearpiecesAsPrerequisites { get; set; }
    bool BrightListItemHighlighting { get; set; }
    HighlightColor DefaultHighlightColor { get; }
    UiTheme UiTheme { get; set; }

    // DEBUGGING
    bool EnableDebugging { get; set; }
    FrameworkThreadBehaviorType DebugFrameworkThreadBehavior { get; set; }
}

