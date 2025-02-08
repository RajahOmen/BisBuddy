using BisBuddy.Gear;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using Lumina.Excel.Sheets;
using System;
using static FFXIVClientStructs.STD.Helper.IStaticEncoding;

namespace BisBuddy.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Configuration configuration;
    private readonly Plugin plugin;

    public ConfigWindow(Plugin plugin) : base($"{Plugin.PluginName} Config###bisbuddyconfiguration")
    {
        Flags = ImGuiWindowFlags.AlwaysAutoResize
                | ImGuiWindowFlags.NoScrollbar
                | ImGuiWindowFlags.NoScrollWithMouse;

        configuration = plugin.Configuration;
        this.plugin = plugin;
    }

    public void Dispose() { }

    public override void Draw()
    {
        ImGui.Text("Item Highlighting");
        ImGui.Spacing();

        // NEED GREED
        var highlightNeedGreed = configuration.HighlightNeedGreed;
        if (ImGui.Checkbox("Need/Greed Windows", ref highlightNeedGreed))
        {
            configuration.HighlightNeedGreed = highlightNeedGreed;
            plugin.SaveConfiguration(false);
            plugin.NeedGreedEventListener.SetListeningStatus(highlightNeedGreed);
        }
        ImGui.SameLine();
        ImGuiComponents.HelpMarker("Highlights items needed for gearsets in Need/Greed loot windows");

        // SHOPS
        var highlightShops = configuration.HighlightShops;
        if (ImGui.Checkbox("Shops/Exchanges", ref highlightShops))
        {
            configuration.HighlightShops = highlightShops;
            plugin.SaveConfiguration(false);
            plugin.ShopExchangeItemEventListener.SetListeningStatus(highlightShops);
            plugin.ShopExchangeCurrencyEventListener.SetListeningStatus(highlightShops);
        }
        ImGui.SameLine();
        ImGuiComponents.HelpMarker("Highlights items needed for gearsets in NPC shops/item exchanges");

        // MATERIA MELDING
        var highlightMateriaMeld = configuration.HighlightMateriaMeld;
        if (ImGui.Checkbox("Materia Melding", ref highlightMateriaMeld))
        {
            configuration.HighlightMateriaMeld = highlightMateriaMeld;
            plugin.SaveConfiguration(false);
            plugin.MateriaAttachEventListener.SetListeningStatus(highlightMateriaMeld);
        }
        ImGui.SameLine();
        ImGuiComponents.HelpMarker("Highlights gearpieces needing melds and the materia needed for those gearpieces in melding windows");

        // INVENTORIES
        var highlightInventories = configuration.HighlightInventories;
        if (ImGui.Checkbox("Inventories", ref highlightInventories))
        {
            configuration.HighlightInventories = highlightInventories;
            plugin.SaveConfiguration(false);
            plugin.InventoryEventListener.SetListeningStatus(highlightInventories);
            plugin.InventoryLargeEventListener.SetListeningStatus(highlightInventories);
            plugin.InventoryExpansionEventListener.SetListeningStatus(highlightInventories);
            plugin.InventoryRetainerEventListener.SetListeningStatus(highlightInventories);
            plugin.InventoryRetainerLargeEventListener.SetListeningStatus(highlightInventories);
            plugin.InventoryBuddyEventListener.SetListeningStatus(highlightInventories);
        }
        ImGui.SameLine();
        ImGuiComponents.HelpMarker("Highlights items needed for gearsets in inventories (inventory, retainer, saddlebag)");

        // MARKETBOARD
        var highlightMarketboard = configuration.HighlightMarketboard;
        if (ImGui.Checkbox("Marketboard", ref highlightMarketboard))
        {
            configuration.HighlightMarketboard = highlightMarketboard;
            plugin.SaveConfiguration(false);
            plugin.ItemSearchEventListener.SetListeningStatus(highlightMarketboard);
            plugin.ItemSearchResultEventListener.SetListeningStatus(highlightMarketboard);
        }
        ImGui.SameLine();
        ImGuiComponents.HelpMarker("Highlights items needed for gearsets on the marketboard");

        // ITEM TOOLTIPS
        var annotateTooltips = configuration.AnnotateTooltips;
        if (ImGui.Checkbox("Item Tooltips", ref annotateTooltips))
        {
            configuration.AnnotateTooltips = annotateTooltips;
            plugin.SaveConfiguration(false);
            plugin.ItemDetailEventListener.SetListeningStatus(annotateTooltips);
        }
        ImGui.SameLine();
        ImGuiComponents.HelpMarker("Lists what gearsets need the item being hovered over in the item tooltip");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Text("Inventory Updates");
        ImGui.Spacing();

        // ITEM COLLECTION
        var enableAutoComplete = configuration.AutoCompleteItems;
        if (ImGui.Checkbox("Item Changes", ref enableAutoComplete))
        {
            configuration.AutoCompleteItems = enableAutoComplete;
            plugin.SaveConfiguration(true);
            plugin.ItemUpdateEventListener.SetListeningStatus(enableAutoComplete);
        }
        ImGui.SameLine();
        ImGuiComponents.HelpMarker("When a change is detected in character inventories, update gearsets with items in inventories (inventory, armoury chest, equipped)");

        // INVENTORY SCAN ON LOGIN/LOAD
        var enableAutoScan = configuration.AutoScanInventory;
        if (ImGui.Checkbox("Login/Load", ref enableAutoScan))
        {
            configuration.AutoScanInventory = enableAutoScan;
            plugin.SaveConfiguration(true);
            plugin.LoginLoadEventListener.SetListeningStatus(enableAutoScan);
        }
        ImGui.SameLine();
        ImGuiComponents.HelpMarker($"When logging in or loading {Plugin.PluginName}, update gearsets with items in inventories (inventory, armoury chest, equipped)");

        // INVENTORY SCAN ON PLUGIN UPDATES
        var enablePluginUpdateScan = configuration.PluginUpdateInventoryScan;
        if (ImGui.Checkbox("Plugin Changes", ref enablePluginUpdateScan))
        {
            configuration.PluginUpdateInventoryScan = enablePluginUpdateScan;
            plugin.SaveConfiguration(true);
        }
        ImGui.SameLine();
        ImGuiComponents.HelpMarker($"When updating {Plugin.PluginName} gearsets or settings, update gearsets with items in inventories (inventory, armoury chest, equipped)");

        // ASSIGNMENT GROUPING
        var strictMateriaMatching = configuration.StrictMateriaMatching;
        if (ImGui.Checkbox("Strict Materia Matching", ref strictMateriaMatching))
        {
            configuration.StrictMateriaMatching = strictMateriaMatching;
            plugin.SaveConfiguration(true);

            // if auto scanning enabled, rerun assignments with new configuration
            if (configuration.AutoScanInventory)
            {
                plugin.UpdateFromInventory(plugin.Gearsets);
            }
        }
        ImGui.SameLine();
        ImGuiComponents.HelpMarker(
            //$"If assignment should treat gearsets with same item + different melds separately, or if they can share a single item in inventory\n\n"
            //$"If syncing should consider two gearsets requring the same item with different melds satisfied by one item in inventory\n\n"
            //$"Whether auto assignment consideres gearpieces requiring the same item with different melds unqiue or identical\n\n"
            $"Whether auto assignment treats gearpieces with different materia as unique items\n\n"
            + $"When ON: Each gearpiece entry is considered unique based on its materia. So if you have multiple entries for the same gearpiece with different materia, a single inventory item can only satisfy one of them\n"
            + $"When OFF: Auto assignment ignores the materia differences and only looks at the actual gearpiece. One inventory item can satisfy multiple entries for the same gearpiece even if their materia differ\n\n"
            + "Example\n"
            + "Inventory: 1x Archeo Kingdom Ring of Aiming [CRT]\n"
            + "Gearset 1: 1x Archeo Kingdom Ring of Aiming [CRT, DET]\n"
            + "Gearset 2: 1x Archeo Kingdom Ring of Aiming [DET, DET]\n\n"
            + "When ON: Gearset 1 completed, Gearset 2 incomplete\n"
            + "When OFF: Gearset 1 completed, Gearset 2 completed"
            );

    }
}
