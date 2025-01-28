using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using System;

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
            configuration.Save();
            plugin.NeedGreedEventListener.SetListeningStatus(highlightNeedGreed);
        }
        ImGui.SameLine();
        ImRaii.PushFont(UiBuilder.IconFont);
        ImGui.Text(FontAwesomeIcon.QuestionCircle.ToIconString());
        ImGui.PopFont();
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Highlights items needed for gearsets in Need/Greed loot windows");

        // SHOPS
        var highlightShops = configuration.HighlightShops;
        if (ImGui.Checkbox("Shops/Exchanges", ref highlightShops))
        {
            configuration.HighlightShops = highlightShops;
            configuration.Save();
            plugin.ShopExchangeItemEventListener.SetListeningStatus(highlightShops);
            plugin.ShopExchangeCurrencyEventListener.SetListeningStatus(highlightShops);
        }
        ImGui.SameLine();
        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.Text(FontAwesomeIcon.QuestionCircle.ToIconString());
        ImGui.PopFont();
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Highlights items needed for gearsets in NPC shops/item exchanges");

        // MATERIA MELDING
        var highlightMateriaMeld = configuration.HighlightMateriaMeld;
        if (ImGui.Checkbox("Materia Melding", ref highlightMateriaMeld))
        {
            configuration.HighlightMateriaMeld = highlightMateriaMeld;
            configuration.Save();
            plugin.MateriaAttachEventListener.SetListeningStatus(highlightMateriaMeld);
        }
        ImGui.SameLine();
        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.Text(FontAwesomeIcon.QuestionCircle.ToIconString());
        ImGui.PopFont();
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Highlights gearpieces needing melds and the materia needed for those gearpieces in melding windows");

        // INVENTORIES
        var highlightInventories = configuration.HighlightInventories;
        if (ImGui.Checkbox("Inventories", ref highlightInventories))
        {
            configuration.HighlightInventories = highlightInventories;
            configuration.Save();
            plugin.InventoryEventListener.SetListeningStatus(highlightInventories);
            plugin.InventoryLargeEventListener.SetListeningStatus(highlightInventories);
            plugin.InventoryExpansionEventListener.SetListeningStatus(highlightInventories);
            plugin.InventoryRetainerEventListener.SetListeningStatus(highlightInventories);
            plugin.InventoryRetainerLargeEventListener.SetListeningStatus(highlightInventories);
            plugin.InventoryBuddyEventListener.SetListeningStatus(highlightInventories);
        }
        ImGui.SameLine();
        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.Text(FontAwesomeIcon.QuestionCircle.ToIconString());
        ImGui.PopFont();
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Highlights items needed for gearsets in inventories (inventory, retainer, saddlebag)");

        // MARKETBOARD
        var highlightMarketboard = configuration.HighlightMarketboard;
        if (ImGui.Checkbox("Marketboard", ref highlightMarketboard))
        {
            configuration.HighlightMarketboard = highlightMarketboard;
            configuration.Save();
            plugin.ItemSearchEventListener.SetListeningStatus(highlightMarketboard);
            plugin.ItemSearchResultEventListener.SetListeningStatus(highlightMarketboard);
        }
        ImGui.SameLine();
        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.Text(FontAwesomeIcon.QuestionCircle.ToIconString());
        ImGui.PopFont();
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Highlights items needed for gearsets on the marketboard");

        // ITEM TOOLTIPS
        var annotateTooltips = configuration.AnnotateTooltips;
        if (ImGui.Checkbox("Item Tooltips", ref annotateTooltips))
        {
            configuration.AnnotateTooltips = annotateTooltips;
            configuration.Save();
            plugin.ItemDetailEventListener.SetListeningStatus(annotateTooltips);
        }
        ImGui.SameLine();
        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.Text(FontAwesomeIcon.QuestionCircle.ToIconString());
        ImGui.PopFont();
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Lists what gearsets need the item being hovered over in the item tooltip");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Text("Character Inventory");
        ImGui.Spacing();

        // ITEM COLLECTION
        var enableAutoComplete = configuration.AutoCompleteItems;
        if (ImGui.Checkbox("Update on Change", ref enableAutoComplete))
        {
            configuration.AutoCompleteItems = enableAutoComplete;
            configuration.Save();
            plugin.ItemUpdateEventListener.SetListeningStatus(enableAutoComplete);
        }
        ImGui.SameLine();
        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.Text(FontAwesomeIcon.QuestionCircle.ToIconString());
        ImGui.PopFont();
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("When a change is detected in character inventories, update gearsets with items in inventories (inventory, armoury chest, equipped)");

        // INVENTORY SCAN
        var enableAutoScan = configuration.AutoScanInventory;
        if (ImGui.Checkbox("Update on Login/Load", ref enableAutoScan))
        {
            configuration.AutoScanInventory = enableAutoScan;
            configuration.Save();
            plugin.LoginLoadEventListener.SetListeningStatus(enableAutoScan);
        }
        ImGui.SameLine();
        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.Text(FontAwesomeIcon.QuestionCircle.ToIconString());
        ImGui.PopFont();
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip($"When logging in, adding new gearsets, or loading {Plugin.PluginName}, update gearsets with items in inventories (inventory, armoury chest, equipped)");

        ImGui.Spacing();
    }
}
